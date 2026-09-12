using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Services.Printing
{
    public class KitchenRoutingService : IKitchenRoutingService
    {
        private readonly PosDbContext _context;
        private readonly IBranchConfigurationService _branchConfigurationService;
        private readonly ILogger<KitchenRoutingService> _logger;

        public KitchenRoutingService(
            PosDbContext context,
            IBranchConfigurationService branchConfigurationService,
            ILogger<KitchenRoutingService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<KitchenRouteGroup>> RouteAsync(
            Guid orderId,
            IReadOnlyCollection<Guid>? itemIdFilter,
            CancellationToken ct)
        {
            var branchId = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => o.BranchId)
                .FirstOrDefaultAsync(ct);
            if (branchId == Guid.Empty)
                return Array.Empty<KitchenRouteGroup>();

            var query = _context.OrderItems
                .AsNoTrackingWithIdentityResolution()
                .AsSplitQuery()
                .Include(oi => oi.Modifiers)
                .Include(oi => oi.RecipeSnapshotItems)
                .Where(oi => oi.OrderId == orderId && oi.BranchId == branchId);

            if (itemIdFilter is { Count: > 0 })
            {
                var ids = itemIdFilter.ToList();
                query = query.Where(oi => ids.Contains(oi.Id));
            }

            var items = (await query.ToListAsync(ct))
                .DistinctBy(item => item.Id)
                .ToList();
            if (items.Count == 0) return Array.Empty<KitchenRouteGroup>();

            var productIds = items.Select(i => i.ProductId).Distinct().ToList();

            var productLookup = await _context.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.KitchenId, p.NameAr, p.AllKitchenPrinters })
                .ToDictionaryAsync(x => x.Id, ct);

            foreach (var item in items)
            {
                if (!productLookup.TryGetValue(item.ProductId, out var product)) continue;
                item.Product = new Product
                {
                    Id = item.ProductId,
                    Name = item.ProductName,
                    NameAr = product.NameAr
                };
            }

            // Product → explicit printer mappings (junction).
            var explicitMappings = await _context.ProductKitchenPrinters
                .AsNoTracking()
                .Where(m => productIds.Contains(m.ProductId))
                .Select(m => new { m.ProductId, m.KitchenPrinterId })
                .ToListAsync(ct);

            var mappingsByProduct = explicitMappings
                .GroupBy(m => m.ProductId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.KitchenPrinterId).Distinct().ToList());

            // All active kitchen printers in the tenant — needed for the
            // "All Printers" path and for resolving the legacy fallback.
            var enabledPrinterIds = await _branchConfigurationService.GetEnabledPrinterIdsAsync(branchId, ct);
            var activeKitchenPrinters = await _context.Printers
                .AsNoTracking()
                .Where(p => p.IsActive && p.KitchenId != null && !p.IsReceiptPrinter && enabledPrinterIds.Contains(p.Id))
                .ToListAsync(ct);

            var allActivePrinterIds = activeKitchenPrinters.Select(p => p.Id).ToList();

            // Fallback default kitchen — preserves legacy behavior when a
            // product has no KitchenId and no explicit printer mapping.
            var fallbackKitchenId = await _context.Kitchens
                .AsNoTracking()
                .Where(k => k.IsActive)
                .OrderBy(k => k.SortOrder)
                .ThenBy(k => k.Name)
                .Select(k => (Guid?)k.Id)
                .FirstOrDefaultAsync(ct);

            var printerById = activeKitchenPrinters.ToDictionary(p => p.Id);

            // Pre-load every kitchen referenced by an active printer so we can
            // attach Kitchen to each group without an N+1.
            var kitchenIds = activeKitchenPrinters
                .Where(p => p.KitchenId.HasValue)
                .Select(p => p.KitchenId!.Value)
                .Concat(fallbackKitchenId.HasValue ? new[] { fallbackKitchenId.Value } : Array.Empty<Guid>())
                .Distinct()
                .ToList();

            var kitchens = kitchenIds.Count == 0
                ? new Dictionary<Guid, Kitchen>()
                : await _context.Kitchens
                    .AsNoTracking()
                    .Where(k => kitchenIds.Contains(k.Id))
                    .ToDictionaryAsync(k => k.Id, ct);

            // Key = (KitchenId ?? Empty, PrinterId ?? Empty).
            var groups = new Dictionary<(Guid, Guid), KitchenRouteGroup>();
            var unroutedItems = new List<OrderItem>();

            foreach (var item in items)
            {
                if (!productLookup.TryGetValue(item.ProductId, out var product))
                {
                    unroutedItems.Add(item);
                    continue;
                }

                List<Guid> targetPrinterIds;

                if (product.AllKitchenPrinters)
                {
                    targetPrinterIds = allActivePrinterIds;
                }
                else if (mappingsByProduct.TryGetValue(item.ProductId, out var mapped) && mapped.Count > 0)
                {
                    // Only printers that are still active are honored.
                    targetPrinterIds = mapped.Where(id => printerById.ContainsKey(id)).ToList();
                }
                else
                {
                    // Kitchen-scoped routing: a product assigned to a kitchen
                    // fans out to EVERY active printer in that kitchen. This
                    // matches operator intent — "this dish goes to Kitchen 1"
                    // means every printer staffing Kitchen 1 fires, not just
                    // the first one ordered by IsDefault/Name.
                    var legacyKitchenId = product.KitchenId ?? fallbackKitchenId;
                    if (legacyKitchenId.HasValue)
                    {
                        targetPrinterIds = activeKitchenPrinters
                            .Where(p => p.KitchenId == legacyKitchenId.Value)
                            .OrderByDescending(p => p.IsDefault)
                            .ThenBy(p => p.Name)
                            .Select(p => p.Id)
                            .ToList();
                    }
                    else
                    {
                        targetPrinterIds = new List<Guid>();
                    }
                }

                if (targetPrinterIds.Count == 0)
                {
                    unroutedItems.Add(item);
                    continue;
                }

                foreach (var printerId in targetPrinterIds)
                {
                    if (!printerById.TryGetValue(printerId, out var printer)) continue;

                    Kitchen? kitchen = null;
                    if (printer.KitchenId.HasValue)
                    {
                        kitchens.TryGetValue(printer.KitchenId.Value, out kitchen);
                    }

                    var key = (kitchen?.Id ?? Guid.Empty, printer.Id);
                    if (!groups.TryGetValue(key, out var group))
                    {
                        group = new KitchenRouteGroup
                        {
                            Kitchen = kitchen,
                            Printer = printer
                        };
                        groups[key] = group;
                    }
                    group.Items.Add(item);
                }
            }

            if (unroutedItems.Count > 0)
            {
                _logger.LogWarning(
                    "Order {OrderId}: {Count} item(s) had no resolvable printer; ticket printing skipped for those items.",
                    orderId, unroutedItems.Count);

                groups[(Guid.Empty, Guid.Empty)] = new KitchenRouteGroup
                {
                    Kitchen = null,
                    Printer = null,
                    Items = unroutedItems
                };
            }

            return groups.Values.ToList();
        }
    }
}
