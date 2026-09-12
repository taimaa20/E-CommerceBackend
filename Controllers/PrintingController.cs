using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Services.Printing;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public class PrintingController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchConfigurationService _branchConfigurationService;
        private readonly IPrintDispatcher _dispatcher;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly IBranchContext _branchContext;

        public PrintingController(
            PosDbContext context,
            ITenantResolver tenantResolver,
            IBranchConfigurationService branchConfigurationService,
            IPrintDispatcher dispatcher,
            ICurrentUserResolver currentUserResolver,
            IBranchContext branchContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _currentUserResolver = currentUserResolver ?? throw new ArgumentNullException(nameof(currentUserResolver));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        }

        // ── Kitchens ──────────────────────────────────────────────────────────

        [HttpGet("kitchens")]
        [AllowAnonymous]
        public async Task<ActionResult<List<KitchenDto>>> GetKitchens(CancellationToken ct)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Unauthorized();

            if (!CanUseCashierPrinting())
                return Forbid();

            var list = await _context.Kitchens
                .AsNoTracking()
                .Select(k => new KitchenDto
                {
                    Id = k.Id,
                    Name = k.Name,
                    NameAr = k.NameAr,
                    IsActive = k.IsActive,
                    SortOrder = k.SortOrder,
                    PrinterCount = _context.Printers.Count(p => p.KitchenId == k.Id && p.DeletedAt == null)
                })
                .OrderBy(k => k.SortOrder).ThenBy(k => k.Name)
                .ToListAsync(ct);
            return Ok(list);
        }

        [HttpPost("kitchens")]
        public async Task<ActionResult<KitchenDto>> CreateKitchen([FromBody] KitchenUpsertDto input, CancellationToken ct)
        {
            var kitchen = new Kitchen
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                Name = input.Name.Trim(),
                NameAr = input.NameAr?.Trim(),
                IsActive = input.IsActive,
                SortOrder = input.SortOrder
            };
            _context.Kitchens.Add(kitchen);
            await _context.SaveChangesAsync(ct);
            return Ok(MapKitchen(kitchen, 0));
        }

        [HttpPut("kitchens/{id}")]
        public async Task<ActionResult<KitchenDto>> UpdateKitchen(Guid id, [FromBody] KitchenUpsertDto input, CancellationToken ct)
        {
            var kitchen = await _context.Kitchens.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (kitchen == null) return NotFound();

            kitchen.Name = input.Name.Trim();
            kitchen.NameAr = input.NameAr?.Trim();
            kitchen.IsActive = input.IsActive;
            kitchen.SortOrder = input.SortOrder;
            await _context.SaveChangesAsync(ct);

            var printerCount = await _context.Printers.CountAsync(p => p.KitchenId == id, ct);
            return Ok(MapKitchen(kitchen, printerCount));
        }

        [HttpDelete("kitchens/{id}")]
        public async Task<IActionResult> DeleteKitchen(Guid id, CancellationToken ct)
        {
            var kitchen = await _context.Kitchens.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (kitchen == null) return NotFound();

            // Soft delete via DeletedAt — printers and products keep their FK
            // but stop matching the global filter; unassigned printers stay
            // valid because the FK is configured with SetNull on delete.
            kitchen.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return NoContent();
        }

        // ── Printers ──────────────────────────────────────────────────────────

        [HttpGet("printers")]
        public async Task<ActionResult<List<PrinterDto>>> GetPrinters(CancellationToken ct)
        {
            var list = await _context.Printers
                .AsNoTracking()
                .Include(p => p.Kitchen)
                .Select(p => new PrinterDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    NameAr = p.NameAr,
                    KitchenId = p.KitchenId,
                    KitchenName = p.Kitchen != null ? p.Kitchen.Name : null,
                    Type = (int)p.Type,
                    IpAddress = p.IpAddress,
                    Port = p.Port,
                    WindowsPrinterName = p.WindowsPrinterName,
                    UsbPortName = p.UsbPortName,
                    CodePage = p.CodePage,
                    IsActive = p.IsActive,
                    IsReceiptPrinter = p.IsReceiptPrinter,
                    IsDefault = p.IsDefault,
                    UseLocalAgent = p.UseLocalAgent,
                    CopiesPerJob = p.CopiesPerJob
                })
                .OrderBy(p => p.Name)
                .ToListAsync(ct);
            return Ok(list);
        }

        [HttpPost("printers")]
        public async Task<ActionResult<PrinterDto>> CreatePrinter([FromBody] PrinterUpsertDto input, CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            // Default flag is only meaningful on receipt printers — silently
            // strip it from kitchen-only rows so the resolver can't pick a
            // kitchen printer as a receipt fallback.
            var isDefault = input.IsDefault && input.IsReceiptPrinter;

            var printerType = (PrinterType)input.Type;
            var printer = new Printer
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = input.Name.Trim(),
                NameAr = input.NameAr?.Trim(),
                KitchenId = input.KitchenId,
                Type = printerType,
                IpAddress = input.IpAddress?.Trim(),
                Port = input.Port,
                WindowsPrinterName = input.WindowsPrinterName?.Trim(),
                UsbPortName = input.UsbPortName?.Trim(),
                CodePage = input.CodePage,
                IsActive = input.IsActive,
                IsReceiptPrinter = input.IsReceiptPrinter,
                IsDefault = isDefault,
                UseLocalAgent = input.UseLocalAgent || printerType == PrinterType.Usb,
                CopiesPerJob = Math.Max(1, input.CopiesPerJob)
            };

            if (isDefault)
            {
                await ClearOtherDefaultsAsync(tenantId, exceptPrinterId: printer.Id, ct);
            }

            _context.Printers.Add(printer);
            await _context.SaveChangesAsync(ct);
            await _branchConfigurationService.EnsureMainBranchPrintersAsync(
                new[] { new MainBranchConfigurationSeed(printer.TenantId, printer.Id) },
                ct);
            return Ok(MapPrinter(printer, null));
        }

        [HttpPut("printers/{id}")]
        public async Task<ActionResult<PrinterDto>> UpdatePrinter(Guid id, [FromBody] PrinterUpsertDto input, CancellationToken ct)
        {
            var printer = await _context.Printers.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (printer == null) return NotFound();

            var isDefault = input.IsDefault && input.IsReceiptPrinter;

            var printerType = (PrinterType)input.Type;

            printer.Name = input.Name.Trim();
            printer.NameAr = input.NameAr?.Trim();
            printer.KitchenId = input.KitchenId;
            printer.Type = printerType;
            printer.IpAddress = input.IpAddress?.Trim();
            printer.Port = input.Port;
            printer.WindowsPrinterName = input.WindowsPrinterName?.Trim();
            printer.UsbPortName = input.UsbPortName?.Trim();
            printer.CodePage = input.CodePage;
            printer.IsActive = input.IsActive;
            printer.IsReceiptPrinter = input.IsReceiptPrinter;
            printer.IsDefault = isDefault;
            printer.UseLocalAgent = input.UseLocalAgent || printerType == PrinterType.Usb;
            printer.CopiesPerJob = Math.Max(1, input.CopiesPerJob);

            if (isDefault)
            {
                await ClearOtherDefaultsAsync(printer.TenantId, exceptPrinterId: printer.Id, ct);
            }

            await _context.SaveChangesAsync(ct);

            var kitchenName = printer.KitchenId.HasValue
                ? await _context.Kitchens.Where(k => k.Id == printer.KitchenId.Value).Select(k => k.Name).FirstOrDefaultAsync(ct)
                : null;
            return Ok(MapPrinter(printer, kitchenName));
        }

        /// <summary>
        /// Clears IsDefault on every other receipt printer in the same tenant.
        /// Maintains the "at most one default per tenant" invariant without
        /// needing a partial unique constraint that would race with EF's change
        /// tracker on transactional saves.
        /// </summary>
        private async Task ClearOtherDefaultsAsync(Guid tenantId, Guid exceptPrinterId, CancellationToken ct)
        {
            await _context.Printers
                .Where(p => p.TenantId == tenantId && p.IsDefault && p.Id != exceptPrinterId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.IsDefault, false), ct);
        }

        [HttpDelete("printers/{id}")]
        public async Task<IActionResult> DeletePrinter(Guid id, CancellationToken ct)
        {
            var printer = await _context.Printers.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (printer == null) return NotFound();
            printer.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return NoContent();
        }

        // ── Print queue (read-only + reprint) ─────────────────────────────────

        [HttpGet("jobs")]
        public async Task<ActionResult<List<PrintJobDto>>> GetJobs(
            [FromQuery] int? status,
            [FromQuery] int take = 100,
            CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 500);
            // Print jobs are branch operational data — the queue view follows the active branch.
            var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
            var query = _context.PrintJobs.AsNoTracking().Where(j => j.BranchId == branchId);
            if (status.HasValue) query = query.Where(j => (int)j.Status == status.Value);

            var list = await query
                .OrderByDescending(j => j.CreatedAt)
                .Take(take)
                .Select(j => new PrintJobDto
                {
                    Id = j.Id,
                    OrderId = j.OrderId,
                    OrderNumber = j.OrderNumberSnapshot,
                    KitchenId = j.KitchenId,
                    KitchenName = j.KitchenNameSnapshot,
                    PrinterId = j.PrinterId,
                    JobType = (int)j.JobType,
                    IsReprint = j.IsReprint,
                    Status = (int)j.Status,
                    AttemptCount = j.AttemptCount,
                    MaxAttempts = j.MaxAttempts,
                    LastAttemptAt = j.LastAttemptAt,
                    NextAttemptAt = j.NextAttemptAt,
                    CompletedAt = j.CompletedAt,
                    LastError = j.LastError,
                    CreatedAt = j.CreatedAt
                })
                .ToListAsync(ct);
            return Ok(list);
        }

        [HttpPost("jobs/{id}/reprint")]
        public async Task<ActionResult<Guid>> Reprint(Guid id, CancellationToken ct)
        {
            var newId = await _dispatcher.ReprintAsync(id, _tenantResolver.GetTenantId(), ct);
            return Ok(new { jobId = newId });
        }

        [HttpPost("resend-kitchen")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendKitchenTicket(
            [FromBody] ResendKitchenTicketRequest? input,
            CancellationToken ct)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Unauthorized();

            if (!CanUseCashierPrinting())
                return Forbid();

            if (input == null || input.OrderId == Guid.Empty)
                return BadRequest(new { error = "orderId is required." });

            var kitchenIds = input.KitchenIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (kitchenIds.Count == 0)
                return BadRequest(new { error = "Select at least one kitchen." });

            var tenantId = _tenantResolver.GetTenantId();
            var orderExists = await _context.Orders
                .AsNoTracking()
                .AnyAsync(o => o.Id == input.OrderId && o.TenantId == tenantId, ct);

            if (!orderExists) return NotFound();

            var result = await _dispatcher.ResendKitchenTicketsAsync(input.OrderId, tenantId, kitchenIds, ct);
            return Ok(result);
        }

        // POST: api/printing/orders/{orderId}/receipt
        // Allows the cashier UI to push a receipt to the network printer
        // without invoking the browser print dialog. Open to all POS roles
        // because every cashier flow may call it.
        [HttpPost("orders/{orderId}/receipt")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<IActionResult> PrintReceipt(Guid orderId, CancellationToken ct)
        {
            // Caller's user id is the cashier the resolver should prefer.
            var cashierId = await _currentUserResolver.ResolveUserIdAsync(User);

            var enqueued = await _dispatcher.DispatchReceiptOnDemandAsync(
                orderId, _tenantResolver.GetTenantId(), cashierId, ct);

            return enqueued
                ? Ok(new { enqueued = true })
                : Ok(new { enqueued = false, reason = "no_receipt_printer" });
        }

        // POST: api/printing/orders/{orderId}/receipt-image
        // Frontend-rendered receipt path (Part 1 of the printing brief). The
        // cashier UI snapshots its existing HTML receipt design via
        // html2canvas and POSTs the base64 PNG here. Backend converts the
        // PNG → ESC/POS raster bytes and enqueues a print job. The agent
        // prints it raw — no further server-side formatting.
        [HttpPost("orders/{orderId}/receipt-image")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        [RequestSizeLimit(8 * 1024 * 1024)] // 8 MB cap — a typical 80mm receipt is <500 KB
        public async Task<IActionResult> PrintReceiptImage(
            Guid orderId,
            [FromBody] ReceiptImageDto input,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(input);
            if (string.IsNullOrWhiteSpace(input.ImageBase64))
                return BadRequest(new { error = "imageBase64 is required." });

            byte[] imageBytes;
            try
            {
                // Strip optional "data:image/png;base64," prefix defensively —
                // the frontend already strips it but a future caller might not.
                var raw = input.ImageBase64;
                var commaIdx = raw.IndexOf(',');
                if (raw.StartsWith("data:") && commaIdx > 0)
                    raw = raw[(commaIdx + 1)..];
                imageBytes = Convert.FromBase64String(raw);
            }
            catch (FormatException)
            {
                return BadRequest(new { error = "imageBase64 is not valid base64." });
            }

            var cashierId = await _currentUserResolver.ResolveUserIdAsync(User);
            var enqueued = await _dispatcher.DispatchReceiptFromImageAsync(
                orderId, _tenantResolver.GetTenantId(), cashierId, imageBytes, ct);

            return enqueued
                ? Ok(new { enqueued = true })
                : Ok(new { enqueued = false, reason = "no_receipt_printer" });
        }

        [HttpPost("orders/{orderId}/receipt-html")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<IActionResult> PrintReceiptHtml(
            Guid orderId,
            [FromBody] ReceiptHtmlRequest? request,
            CancellationToken ct)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.HtmlContent))
                return BadRequest(new { error = "htmlContent is required." });

            var tenantId = _tenantResolver.GetTenantId();
            var orderExists = await _context.Orders
                .AsNoTracking()
                .AnyAsync(o => o.Id == orderId && o.TenantId == tenantId, ct);
            if (!orderExists) return NotFound();

            var cashierId = await _currentUserResolver.ResolveUserIdAsync(User);
            var enqueued = await _dispatcher.DispatchReceiptFromHtmlAsync(
                orderId, tenantId, cashierId, request.HtmlContent, ct);

            return enqueued
                ? Ok(new { enqueued = true })
                : Ok(new { enqueued = false, reason = "no_receipt_printer" });
        }

        // ── My printer config (cashier UI caches this for offline mode) ──────

        // [AllowAnonymous] is needed to override the class-level AdminOnly role
        // check (which excludes cashiers/waiters). We still require an
        // authenticated user — manually verified below — so this endpoint only
        // ever returns the caller's own printer.
        [HttpGet("me/receipt-printer-config")]
        [AllowAnonymous]
        public async Task<ActionResult<MyReceiptPrinterDto>> GetMyReceiptPrinterConfig(CancellationToken ct)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Unauthorized();

            var cashierId = await _currentUserResolver.ResolveUserIdAsync(User);
            var dto = await _dispatcher.GetMyReceiptPrinterConfigAsync(cashierId, ct);
            if (dto == null) return NotFound();
            return Ok(dto);
        }

        // ── Cashier ↔ printer assignment ──────────────────────────────────────

        [HttpGet("cashiers")]
        public async Task<ActionResult<List<CashierPrinterAssignmentDto>>> GetCashierAssignments(CancellationToken ct)
        {
            // Surface every user that can place orders — Admin/Manager often
            // double as cashiers, so keep them in the list. The dispatcher
            // resolver will simply ignore inactive/missing assignments.
            var allowedRoles = new[] { UserRole.Admin, UserRole.SuperAdmin, UserRole.Manager, UserRole.Cashier, UserRole.Waiter };

            var rows = await _context.Users
                .AsNoTracking()
                .Where(u => allowedRoles.Contains(u.Role))
                .OrderBy(u => u.Role).ThenBy(u => u.Username)
                .Select(u => new CashierPrinterAssignmentDto
                {
                    UserId = u.Id,
                    Username = u.Username,
                    FullName = u.FullName,
                    FullNameAr = u.FullNameAr,
                    Role = u.Role.ToString(),
                    ReceiptPrinterId = u.ReceiptPrinterId,
                    ReceiptPrinterName = u.ReceiptPrinter != null ? u.ReceiptPrinter.Name : null
                })
                .ToListAsync(ct);

            return Ok(rows);
        }

        [HttpPut("cashiers/{userId}/printer")]
        public async Task<IActionResult> SetCashierPrinter(Guid userId, [FromBody] CashierPrinterUpdateDto input, CancellationToken ct)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null) return NotFound("User not found.");

            // Validate the chosen printer belongs to this tenant + is a receipt
            // printer. Reject mismatches early so a misconfigured assignment
            // can't quietly route receipts to a kitchen printer.
            if (input.ReceiptPrinterId.HasValue)
            {
                var printer = await _context.Printers
                    .AsNoTracking()
                    .Where(p => p.Id == input.ReceiptPrinterId.Value)
                    .Select(p => new { p.Id, p.IsReceiptPrinter, p.IsActive })
                    .FirstOrDefaultAsync(ct);

                if (printer == null) return BadRequest("Printer not found.");
                if (!printer.IsReceiptPrinter)
                    return BadRequest("Selected printer is not flagged as a receipt printer.");
            }

            user.ReceiptPrinterId = input.ReceiptPrinterId;
            await _context.SaveChangesAsync(ct);
            return NoContent();
        }

        // ── Agent (local print-agent service) ─────────────────────────────────
        // The agent runs on a PC inside the store LAN. It logs in with a
        // normal POS user (Admin role recommended), polls /agent/jobs/pending
        // every ~2 s, claims one job at a time, prints to the LAN/USB printer,
        // and reports the result.
        // All endpoints are tenant-scoped via the existing TenantMiddleware
        // (X-Tenant-Id header) + JWT — no new auth pipeline.

        [HttpGet("agent/jobs/pending")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<List<AgentJobDto>>> GetAgentPendingJobs(
            [FromQuery] int take = 10,
            CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 50);
            var tenantId = _tenantResolver.GetTenantId();
            var now = DateTime.UtcNow;

            // Agent claims local-agent jobs plus HTML receipts, because HTML
            // receipts require the Puppeteer sidecar before any printer write.
            var jobs = await _context.PrintJobs
                .AsNoTracking()
                .Join(_context.Printers.AsNoTracking(),
                    j => j.PrinterId,
                    p => p.Id,
                    (j, p) => new { Job = j, Printer = p })
                .Where(x => x.Job.TenantId == tenantId
                    && (x.Printer.UseLocalAgent || x.Job.PayloadType == PrintPayloadType.Html)
                    && x.Printer.IsActive
                    && (x.Job.Status == PrintJobStatus.Pending
                        || (x.Job.Status == PrintJobStatus.Failed
                            && x.Job.AttemptCount < x.Job.MaxAttempts
                            && (x.Job.NextAttemptAt == null || x.Job.NextAttemptAt <= now))))
                .OrderBy(x => x.Job.NextAttemptAt ?? x.Job.CreatedAt)
                .Take(take)
                .Select(x => new AgentJobDto
                {
                    Id = x.Job.Id,
                    OrderId = x.Job.OrderId,
                    OrderNumber = x.Job.OrderNumberSnapshot,
                    PrinterId = x.Printer.Id,
                    PrinterName = x.Printer.Name,
                    PrinterType = (int)x.Printer.Type,
                    IpAddress = x.Printer.IpAddress,
                    Port = x.Printer.Port,
                    WindowsPrinterName = x.Printer.WindowsPrinterName,
                    CopiesPerJob = x.Printer.CopiesPerJob,
                    CodePage = x.Printer.CodePage,
                    JobType = (int)x.Job.JobType,
                    PayloadType = (int)x.Job.PayloadType,
                    KitchenName = x.Job.KitchenNameSnapshot,
                    PayloadBase64 = x.Job.PayloadBase64,
                    AttemptCount = x.Job.AttemptCount,
                    MaxAttempts = x.Job.MaxAttempts
                })
                .ToListAsync(ct);

            return Ok(jobs);
        }

        // Atomic Pending|Failed → Printing transition. Returns 200 if claimed,
        // 409 if another agent (or another tick of this one) already owns it.
        [HttpPost("agent/jobs/{id}/claim")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> ClaimAgentJob(Guid id, CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var now = DateTime.UtcNow;

            // ExecuteUpdateAsync does the conditional WHERE + SET in a single
            // SQL statement, so two agents racing don't both claim. The
            // returned row count tells us who won.
            var rows = await _context.PrintJobs
                .Where(j => j.Id == id
                    && j.TenantId == tenantId
                    && (j.Status == PrintJobStatus.Pending || j.Status == PrintJobStatus.Failed))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, PrintJobStatus.Printing)
                    .SetProperty(j => j.LastAttemptAt, now)
                    .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1),
                    ct);

            return rows > 0
                ? Ok(new { claimed = true })
                : Conflict(new { claimed = false, reason = "already_taken_or_completed" });
        }

        [HttpPost("agent/jobs/{id}/result")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> ReportAgentResult(Guid id, [FromBody] AgentResultDto input, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(input);
            var tenantId = _tenantResolver.GetTenantId();
            var job = await _context.PrintJobs
                .FirstOrDefaultAsync(j => j.Id == id && j.TenantId == tenantId, ct);
            if (job == null) return NotFound();

            if (input.Success)
            {
                job.Status = PrintJobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                job.LastError = null;
            }
            else
            {
                // Mirror PrintQueueProcessorBackgroundService.MarkFailureAsync
                // logic so cloud-direct and agent-printed jobs share one
                // retry/backoff curve.
                var truncated = (input.Error ?? "Agent reported failure");
                if (truncated.Length > 1000) truncated = truncated[..1000];
                job.LastError = truncated;

                if (job.AttemptCount >= job.MaxAttempts)
                {
                    job.Status = PrintJobStatus.DeadLetter;
                    job.NextAttemptAt = null;
                }
                else
                {
                    job.Status = PrintJobStatus.Failed;
                    var backoffSeconds = job.AttemptCount switch { 1 => 5, 2 => 30, _ => 120 };
                    var jitterMs = Random.Shared.Next(0, 1500);
                    job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoffSeconds).AddMilliseconds(jitterMs);
                }
            }

            await _context.SaveChangesAsync(ct);
            return Ok(new { status = (int)job.Status });
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static KitchenDto MapKitchen(Kitchen k, int printerCount) => new()
        {
            Id = k.Id,
            Name = k.Name,
            NameAr = k.NameAr,
            IsActive = k.IsActive,
            SortOrder = k.SortOrder,
            PrinterCount = printerCount
        };

        private static PrinterDto MapPrinter(Printer p, string? kitchenName) => new()
        {
            Id = p.Id,
            Name = p.Name,
            NameAr = p.NameAr,
            KitchenId = p.KitchenId,
            KitchenName = kitchenName,
            Type = (int)p.Type,
            IpAddress = p.IpAddress,
            Port = p.Port,
            WindowsPrinterName = p.WindowsPrinterName,
            UsbPortName = p.UsbPortName,
            CodePage = p.CodePage,
            IsActive = p.IsActive,
            IsReceiptPrinter = p.IsReceiptPrinter,
            IsDefault = p.IsDefault,
            UseLocalAgent = p.UseLocalAgent,
            CopiesPerJob = p.CopiesPerJob
        };

        private bool CanUseCashierPrinting()
            => User.IsInRole(AppRoleNames.Admin)
               || User.IsInRole(AppRoleNames.Manager)
               || User.IsInRole(AppRoleNames.Cashier);

        public record ReceiptHtmlRequest(string HtmlContent);
    }
}
