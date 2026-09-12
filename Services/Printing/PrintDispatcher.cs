using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;
using System.Security.Cryptography;
using System.Text;

namespace RestaurantPos.Api.Services.Printing
{
    public class PrintDispatcher : IPrintDispatcher
    {
        private const string ResendBatchPrefix = "resend:";
        private const string ReasonNoKitchensSelected = "no_kitchens_selected";
        private const string ReasonOrderNotFound = "order_not_found";
        private const string ReasonNoMatchingItemsOrPrinter = "no_matching_items_or_printer";

        private readonly PosDbContext _context;
        private readonly IKitchenRoutingService _routing;
        private readonly IKitchenTicketBuilder _ticketBuilder;
        private readonly IPrintQueueService _queue;
        private readonly IBranchContext _branchContext;
        private readonly IBranchConfigurationService _branchConfigurationService;
        private readonly ILogger<PrintDispatcher> _logger;

        public PrintDispatcher(
            PosDbContext context,
            IKitchenRoutingService routing,
            IKitchenTicketBuilder ticketBuilder,
            IPrintQueueService queue,
            IBranchContext branchContext,
            IBranchConfigurationService branchConfigurationService,
            ILogger<PrintDispatcher> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _routing = routing ?? throw new ArgumentNullException(nameof(routing));
            _ticketBuilder = ticketBuilder ?? throw new ArgumentNullException(nameof(ticketBuilder));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task DispatchOrderCreatedAsync(Guid orderId, Guid tenantId, Guid? cashierUserId, CancellationToken ct)
        {
            var order = await LoadOrderAsync(orderId, ct);
            if (order == null)
            {
                _logger.LogWarning("DispatchOrderCreated: order {OrderId} not found.", orderId);
                return;
            }

            // ── Per-kitchen tickets — server-rendered (no design
            //    coupling to the cashier UI). Continue to fire automatically.
            var groups = await _routing.RouteAsync(orderId, itemIdFilter: null, ct);
            await EnqueueKitchenTicketsAsync(order, groups, tenantId, batchKey: "create", ct);

            // ── Customer receipt is intentionally NOT enqueued here.
            //    The cashier UI renders its own HTML receipt design via
            //    html2canvas and POSTs the resulting PNG to
            //    /api/printing/orders/{id}/receipt-image immediately after a
            //    successful order create. That guarantees the printed
            //    receipt is byte-identical to the on-screen preview — no
            //    second "design" maintained server-side.
            //
            //    Manual reprint endpoints (/orders/{id}/receipt and
            //    /jobs/{id}/reprint) still exist as fallbacks for cases
            //    where the cashier UI isn't available.
        }

        public async Task DispatchItemsAddedAsync(Guid orderId, Guid tenantId, IReadOnlyCollection<Guid> addedItemIds, CancellationToken ct)
        {
            if (addedItemIds == null || addedItemIds.Count == 0) return;
            var distinctItemIds = addedItemIds.Where(id => id != Guid.Empty).Distinct().ToList();
            if (distinctItemIds.Count == 0) return;

            var order = await LoadOrderAsync(orderId, ct);
            if (order == null)
            {
                _logger.LogWarning("DispatchItemsAdded: order {OrderId} not found.", orderId);
                return;
            }

            // Per-kitchen tickets only — receipt does not reprint on item-add.
            var groups = await _routing.RouteAsync(orderId, distinctItemIds, ct);

            // Use a stable batch key derived from the added item ids so a duplicate
            // event delivery (network blip) collapses into a single enqueue.
            var batchKey = "items-" + ComputeStableHash(distinctItemIds);

            await EnqueueKitchenTicketsAsync(order, groups, tenantId, batchKey, ct);
        }

        public async Task<bool> DispatchReceiptOnDemandAsync(Guid orderId, Guid tenantId, Guid? cashierUserId, CancellationToken ct)
        {
            var order = await LoadOrderAsync(orderId, ct);
            if (order == null)
            {
                _logger.LogWarning("DispatchReceiptOnDemand: order {OrderId} not found.", orderId);
                return false;
            }

            // Each on-demand call produces a fresh idempotency key suffix —
            // staff that click "print receipt" twice get two physical copies,
            // which matches their intent.
            var batchKey = "ondemand:" + DateTime.UtcNow.Ticks;

            // Resolve the printer up-front so we can return false (so the
            // caller can fall back) when nothing is configured at all.
            var printer = await ResolveReceiptPrinterAsync(cashierUserId, order.BranchId, ct);
            if (printer == null)
            {
                _logger.LogInformation(
                    "DispatchReceiptOnDemand: no active receipt printer for tenant {Tenant}; caller may fall back.",
                    tenantId);
                return false;
            }

            await EnqueueReceiptAsync(order, tenantId, cashierUserId, batchKey, ct);
            return true;
        }

        public async Task<bool> DispatchReceiptFromImageAsync(
            Guid orderId, Guid tenantId, Guid? cashierUserId, byte[] imageBytes, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(imageBytes);

            var order = await LoadOrderAsync(orderId, ct);
            if (order == null)
            {
                _logger.LogWarning("DispatchReceiptFromImage: order {OrderId} not found.", orderId);
                return false;
            }

            var printer = await ResolveReceiptPrinterAsync(cashierUserId, order.BranchId, ct);
            if (printer == null)
            {
                _logger.LogInformation(
                    "DispatchReceiptFromImage: no active receipt printer for tenant {Tenant}; rejected.",
                    tenantId);
                return false;
            }

            // Convert frontend-rendered PNG to ESC/POS raster bytes. The
            // resulting payload IS the print job — no further server-side
            // text formatting is applied. The cashier UI's design is the
            // source of truth.
            byte[] payload;
            try
            {
                payload = EscPosBitmapEncoder.Encode(imageBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "DispatchReceiptFromImage: failed to encode bitmap for order {OrderId}.",
                    orderId);
                throw;
            }

            var batchKey = "image:" + DateTime.UtcNow.Ticks;
            var key = $"order:{order.Id}:receipt:{batchKey}";

            await _queue.EnqueueAsync(
                tenantId,
                order.Id,
                kitchenId: null,
                printer.Id,
                PrintJobType.CustomerReceipt,
                payload,
                key,
                ResolvePrintOrderNumber(order),
                kitchenNameSnapshot: null,
                ct);

            return true;
        }

        public async Task<bool> DispatchReceiptFromHtmlAsync(
            Guid orderId, Guid tenantId, Guid? cashierUserId, string htmlContent, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
                throw new ArgumentException("HtmlContent is required.", nameof(htmlContent));

            var order = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => new { o.Id, o.BranchId, o.OrderNumber, o.DisplayOrderNumber, o.PublicOrderNumber })
                .FirstOrDefaultAsync(ct);
            if (order == null)
            {
                _logger.LogWarning("DispatchReceiptFromHtml: order {OrderId} not found.", orderId);
                return false;
            }

            var printer = await ResolveReceiptPrinterAsync(cashierUserId, order.BranchId, ct);
            if (printer == null)
            {
                _logger.LogInformation(
                    "DispatchReceiptFromHtml: no active receipt printer for tenant {Tenant}; rejected.",
                    tenantId);
                return false;
            }

            var batchKey = "html:" + DateTime.UtcNow.Ticks;
            var key = $"order:{order.Id}:receipt:{batchKey}";
            var payload = Encoding.UTF8.GetBytes(htmlContent);

            await _queue.EnqueueAsync(
                tenantId,
                order.Id,
                kitchenId: null,
                printer.Id,
                PrintJobType.CustomerReceipt,
                payload,
                key,
                ResolvePrintOrderNumber(order.DisplayOrderNumber, order.PublicOrderNumber, order.OrderNumber),
                kitchenNameSnapshot: null,
                ct,
                PrintPayloadType.Html);

            return true;
        }

        public async Task<DTOs.MyReceiptPrinterDto?> GetMyReceiptPrinterConfigAsync(
            Guid? cashierUserId, CancellationToken ct)
        {
            var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
            var printer = await ResolveReceiptPrinterAsync(cashierUserId, branchId, ct);
            if (printer == null) return null;
            return new DTOs.MyReceiptPrinterDto
            {
                PrinterType = (int)printer.Type,
                WindowsPrinterName = printer.WindowsPrinterName,
                IpAddress = printer.IpAddress,
                Port = printer.Port,
                Copies = Math.Max(1, printer.CopiesPerJob),
            };
        }

        public async Task<Guid> ReprintAsync(Guid jobId, Guid tenantId, CancellationToken ct)
        {
            var existing = await _context.PrintJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId && j.TenantId == tenantId, ct);

            if (existing == null)
                throw new InvalidOperationException($"PrintJob {jobId} not found.");

            var payload = Convert.FromBase64String(existing.PayloadBase64);
            var newKey = existing.IdempotencyKey + ":reprint:" + DateTime.UtcNow.Ticks;

            return await _queue.EnqueueAsync(
                tenantId,
                existing.OrderId,
                existing.KitchenId,
                existing.PrinterId,
                existing.JobType,
                payload,
                newKey,
                existing.OrderNumberSnapshot,
                existing.KitchenNameSnapshot,
                ct,
                existing.PayloadType);
        }

        public async Task<ResendKitchenTicketResponse> ResendKitchenTicketsAsync(
            Guid orderId,
            Guid tenantId,
            IReadOnlyCollection<Guid> kitchenIds,
            CancellationToken ct)
        {
            var selectedKitchenIds = kitchenIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToHashSet();

            if (selectedKitchenIds.Count == 0)
                return new ResendKitchenTicketResponse { Enqueued = false, Reason = ReasonNoKitchensSelected };

            var order = await LoadOrderAsync(orderId, ct);
            if (order == null)
                return new ResendKitchenTicketResponse { Enqueued = false, Reason = ReasonOrderNotFound };

            var routeGroups = await _routing.RouteAsync(orderId, itemIdFilter: null, ct);
            var selectedGroups = routeGroups
                .Where(group => group.Kitchen != null && group.Printer != null && selectedKitchenIds.Contains(group.Kitchen.Id))
                .ToList();

            var response = new ResendKitchenTicketResponse();
            var batchKey = ResendBatchPrefix + DateTime.UtcNow.Ticks;

            foreach (var group in selectedGroups)
            {
                var kitchen = group.Kitchen!;
                var printer = group.Printer!;

                var hasArabic = HasArabicTicketText(order, kitchen, group.Items);
                var useHtmlFallback = hasArabic && UsesLocalAgent(printer);
                var payload = BuildKitchenPayload(
                    order,
                    kitchen,
                    group.Items,
                    printer,
                    hasArabic,
                    useHtmlFallback,
                    isReprint: true);
                var key = $"order:{order.Id}:kitchen:{kitchen.Id}:printer:{printer.Id}:{batchKey}";

                var jobId = await _queue.EnqueueAsync(
                    tenantId,
                    order.Id,
                    kitchen.Id,
                    printer.Id,
                    PrintJobType.KitchenTicket,
                    payload,
                    key,
                    ResolvePrintOrderNumber(order),
                    kitchen.Name,
                    ct,
                    useHtmlFallback ? PrintPayloadType.Html : PrintPayloadType.EscPos,
                    isReprint: true);

                response.Jobs.Add(new ResendKitchenTicketJobDto
                {
                    JobId = jobId,
                    KitchenId = kitchen.Id,
                    KitchenName = kitchen.Name,
                    ItemCount = group.Items.Count
                });
            }

            var queuedKitchenIds = response.Jobs.Select(job => job.KitchenId).ToHashSet();
            foreach (var kitchenId in selectedKitchenIds.Where(id => !queuedKitchenIds.Contains(id)))
            {
                if (!response.SkippedKitchenIds.Contains(kitchenId))
                    response.SkippedKitchenIds.Add(kitchenId);
            }

            response.Enqueued = response.Jobs.Count > 0;
            response.Reason = response.Enqueued ? null : ReasonNoMatchingItemsOrPrinter;
            return response;
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private async Task<Order?> LoadOrderAsync(Guid orderId, CancellationToken ct)
        {
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        }

        private async Task EnqueueReceiptAsync(Order order, Guid tenantId, Guid? cashierUserId, string batchKey, CancellationToken ct)
        {
            var receiptPrinter = await ResolveReceiptPrinterAsync(cashierUserId, order.BranchId, ct);
            if (receiptPrinter == null)
            {
                _logger.LogInformation("No receipt printer configured for tenant {Tenant}; receipt skipped.", tenantId);
                return;
            }

            var settings = await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

            var payload = _ticketBuilder.BuildCustomerReceipt(
                order,
                order.OrderItems?.ToList() ?? new List<OrderItem>(),
                receiptPrinter.CodePage,
                settings);

            var key = $"order:{order.Id}:receipt:{batchKey}";

            await _queue.EnqueueAsync(
                tenantId,
                order.Id,
                kitchenId: null,
                receiptPrinter.Id,
                PrintJobType.CustomerReceipt,
                payload,
                key,
                ResolvePrintOrderNumber(order),
                kitchenNameSnapshot: null,
                ct);
        }

        /// <summary>
        /// Three-tier receipt printer resolution:
        ///   1. Cashier-specific (User.ReceiptPrinterId) — if set + active + network.
        ///   2. Tenant default (Printer.IsDefault) — if active + receipt + network.
        ///   3. Any active receipt printer — last-resort fallback (preserves
        ///      pre-existing behavior so a tenant that never configures a
        ///      default still prints).
        /// Returns null only when literally no eligible printer exists.
        /// </summary>
        private async Task<Printer?> ResolveReceiptPrinterAsync(Guid? cashierUserId, Guid branchId, CancellationToken ct)
        {
            var enabledPrinterIds = await _branchConfigurationService.GetEnabledPrinterIdsAsync(branchId, ct);
            if (enabledPrinterIds.Count == 0)
                return null;

            // Tier 1 — cashier-specific.
            // Type filter intentionally allows BOTH Network and USB: the cloud
            // worker filters by Type=Network elsewhere, and the in-store agent
            // happily handles USB. So either type is a valid enqueue target —
            // routing to the right worker happens downstream, not here.
            if (cashierUserId.HasValue && cashierUserId.Value != Guid.Empty)
            {
                var cashierPrinter = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == cashierUserId.Value && u.ReceiptPrinterId != null)
                    .Join(_context.Printers.AsNoTracking(),
                        u => u.ReceiptPrinterId,
                        p => p.Id,
                        (u, p) => p)
                    .Where(p => p.IsActive && p.IsReceiptPrinter && enabledPrinterIds.Contains(p.Id))
                    .FirstOrDefaultAsync(ct);

                if (cashierPrinter != null) return cashierPrinter;

                _logger.LogDebug(
                    "Cashier {Cashier} has no usable assigned printer; falling back to default.",
                    cashierUserId);
            }

            // Tier 2 — tenant default
            var defaultPrinter = await _context.Printers
                .AsNoTracking()
                .Where(p => p.IsDefault && p.IsReceiptPrinter && p.IsActive && enabledPrinterIds.Contains(p.Id))
                .FirstOrDefaultAsync(ct);

            if (defaultPrinter != null) return defaultPrinter;

            // Tier 3 — any active receipt printer (last-resort fallback)
            return await _context.Printers
                .AsNoTracking()
                .Where(p => p.IsReceiptPrinter && p.IsActive && enabledPrinterIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .FirstOrDefaultAsync(ct);
        }

        private async Task EnqueueKitchenTicketsAsync(
            Order order,
            IReadOnlyList<KitchenRouteGroup> groups,
            Guid tenantId,
            string batchKey,
            CancellationToken ct)
        {
            foreach (var group in groups)
            {
                if (group.Kitchen == null || group.Printer == null)
                {
                    _logger.LogWarning(
                        "Skipping {Count} unrouted item(s) for order {OrderId} — no resolvable Kitchen/Printer.",
                        group.Items.Count, order.Id);
                    continue;
                }

                var printer = group.Printer;
                var hasArabic = HasArabicTicketText(order, group.Kitchen, group.Items);
                var useHtmlFallback = hasArabic && UsesLocalAgent(printer);
                var payload = BuildKitchenPayload(order, group.Kitchen, group.Items, printer, hasArabic, useHtmlFallback);
                var key = $"order:{order.Id}:kitchen:{group.Kitchen.Id}:printer:{printer.Id}:{batchKey}";

                await _queue.EnqueueAsync(
                    tenantId,
                    order.Id,
                    group.Kitchen.Id,
                    printer.Id,
                    PrintJobType.KitchenTicket,
                    payload,
                    key,
                    ResolvePrintOrderNumber(order),
                    group.Kitchen.Name,
                    ct,
                    useHtmlFallback ? PrintPayloadType.Html : PrintPayloadType.EscPos);
            }
        }

        private byte[] BuildKitchenPayload(
            Order order,
            Kitchen kitchen,
            IReadOnlyList<OrderItem> items,
            Printer printer,
            bool hasArabic,
            bool useHtmlFallback,
            bool isReprint = false)
        {
            if (useHtmlFallback)
                return Encoding.UTF8.GetBytes(_ticketBuilder.BuildKitchenTicketHtml(order, kitchen, items, isReprint));

            var codePage = EscPosFormatter.UseArabicCodePageIfNeeded(printer.CodePage, hasArabic);
            var ticket = _ticketBuilder.BuildKitchenTicket(order, kitchen, items, codePage, isReprint);
            return EscPosFormatter.FinalizeKitchenTicketCopy(ticket);
        }

        private static bool UsesLocalAgent(Printer printer)
            => printer.UseLocalAgent || printer.Type == PrinterType.Usb;

        // Single source of truth for the identifier that gets stamped onto print
        // jobs (so receipts and kitchen tickets print the same value the cashier
        // sees on the order card). Mirrors OrdersController.ResolveOrderNumber.
        private static string ResolvePrintOrderNumber(Order order)
            => ResolvePrintOrderNumber(order.DisplayOrderNumber, order.PublicOrderNumber, order.OrderNumber);

        private static string ResolvePrintOrderNumber(string? displayOrderNumber, string? publicOrderNumber, string orderNumber)
        {
            if (!string.IsNullOrWhiteSpace(displayOrderNumber))
                return displayOrderNumber!;
            if (!string.IsNullOrWhiteSpace(publicOrderNumber))
                return publicOrderNumber!;
            return orderNumber;
        }

        private static bool HasArabicTicketText(Order order, Kitchen kitchen, IReadOnlyList<OrderItem> items)
        {
            return EscPosFormatter.ContainsArabic(kitchen.Name)
                || EscPosFormatter.ContainsArabic(kitchen.NameAr)
                || EscPosFormatter.ContainsArabic(order.TableName)
                || items.Any(HasArabicItemText);
        }

        private static bool HasArabicItemText(OrderItem item)
        {
            return EscPosFormatter.ContainsArabic(item.ProductName)
                || EscPosFormatter.ContainsArabic(item.Product?.NameAr)
                || EscPosFormatter.ContainsArabic(item.SelectedOptionName)
                || EscPosFormatter.ContainsArabic(item.SelectedOptionNameAr)
                || EscPosFormatter.ContainsArabic(item.Notes)
                || (item.Modifiers?.Any(m =>
                    EscPosFormatter.ContainsArabic(m.ModifierName)
                    || EscPosFormatter.ContainsArabic(m.ModifierNameAr)) ?? false)
                || (item.RecipeSnapshotItems?.Any(s =>
                    s.SourceAlternativeId.HasValue
                    && (EscPosFormatter.ContainsArabic(s.SourceAlternativeName)
                        || EscPosFormatter.ContainsArabic(s.SourceAlternativeNameAr)
                        || EscPosFormatter.ContainsArabic(s.RawMaterialName)
                        || EscPosFormatter.ContainsArabic(s.RawMaterialNameAr))) ?? false);
        }

        private static string ComputeStableHash(IEnumerable<Guid> ids)
        {
            var canonical = string.Join("|", ids.OrderBy(id => id).Select(id => id.ToString("N")));
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        }
    }
}
