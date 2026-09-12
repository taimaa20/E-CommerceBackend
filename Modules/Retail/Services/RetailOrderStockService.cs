using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.Domain;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <summary>
    /// Keeps the retail stock ledger in step with the order lifecycle.
    ///
    /// It is a RECONCILER, not a command handler: every entry point re-derives what the
    /// ledger should look like for an order in its current state and posts only the
    /// difference. That is why payment, readiness, cancellation and refund can all call it,
    /// in any order, any number of times, without a coordination protocol between them.
    ///
    /// Relief point — a retail line is relieved at the FIRST fulfilment signal on the order:
    /// the line is marked ready (the same signal the restaurant FIFO engine uses) or the order
    /// is paid (the counter-sale handover, where nobody presses "ready"). Whichever arrives
    /// first posts the movement; the other finds it already there. Exactly-once comes from the
    /// unique index on (tenant, movement type, source line), never from trigger ordering.
    ///
    /// Return point — a line stops being relieved when the order is cancelled, payment is
    /// cancelled, or the line is removed from the order. Retail goods go back on the shelf,
    /// so the ledger gets a compensating SaleReversal; the original movement is never edited.
    /// </summary>
    public sealed class RetailOrderStockService : IProductStockLedger, IRetailOrderStockService
    {
        private const string PerformedBySystem = "order-fulfilment";

        private static readonly OrderStatus[] DeadOrderStatuses =
        {
            OrderStatus.Cancelled,
            OrderStatus.PaymentCancelled
        };

        private readonly PosDbContext _context;
        private readonly IRetailStockLedger _ledger;
        private readonly ILogger<RetailOrderStockService> _logger;

        public RetailOrderStockService(
            PosDbContext context,
            IRetailStockLedger ledger,
            ILogger<RetailOrderStockService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string?> ValidateAvailabilityAsync(
            Guid branchId,
            IReadOnlyList<ProductStockRequest> lines,
            bool isArabic,
            CancellationToken ct = default)
        {
            if (lines.Count == 0)
                return null;

            var requested = lines
                .GroupBy(l => l.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

            var tracked = await LoadTrackedProductsAsync(requested.Keys.ToList(), ct);
            if (tracked.Count == 0)
                return null;

            var onHand = await _ledger.GetOnHandAsync(branchId, tracked.Keys.ToList(), ct);

            var shortfalls = tracked
                .Where(p => requested[p.Key] > onHand.GetValueOrDefault(p.Key))
                .Select(p => Describe(p.Value, onHand.GetValueOrDefault(p.Key), requested[p.Key], isArabic))
                .ToList();

            if (shortfalls.Count == 0)
                return null;

            return isArabic
                ? $"لا يوجد مخزون كافٍ: {string.Join("، ", shortfalls)}"
                : $"Not enough stock: {string.Join(", ", shortfalls)}";
        }

        public Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(
            Guid branchId,
            IReadOnlyCollection<Guid> productIds,
            CancellationToken ct = default)
            => _ledger.GetOnHandAsync(branchId, productIds, ct);

        public async Task<decimal?> SyncOrderItemAsync(Guid orderId, Guid orderItemId, CancellationToken ct = default)
        {
            var costs = await ReconcileAsync(orderId, ct);
            return costs.TryGetValue(orderItemId, out var cost) ? cost : null;
        }

        public async Task SyncOrderAsync(Guid orderId, CancellationToken ct = default)
            => await ReconcileAsync(orderId, ct);

        /// <summary>
        /// Re-derives the ledger position of every retail line on the order and posts the
        /// difference. Returns the cost of goods for each retail line that is currently
        /// relieved, so the caller can roll it into the order without recomputing it.
        /// </summary>
        private async Task<Dictionary<Guid, decimal>> ReconcileAsync(Guid orderId, CancellationToken ct)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (order is null)
                return new Dictionary<Guid, decimal>();

            var movements = await _ledger.GetBySourceDocumentAsync(RetailStockSourceDocument.Order, orderId, ct);
            var productIds = order.OrderItems.Select(i => i.ProductId).Distinct().ToList();
            var tracked = await LoadTrackedProductsAsync(productIds, ct);

            // Nothing retail on this order and nothing ever posted for it — a pure restaurant
            // order. Leave without touching a single field.
            if (tracked.Count == 0 && movements.Count == 0)
                return new Dictionary<Guid, decimal>();

            var sales = movements
                .Where(m => m.MovementType == RetailStockMovementType.Sale && m.SourceLineId.HasValue)
                .ToDictionary(m => m.SourceLineId!.Value, m => m);
            var reversed = movements
                .Where(m => m.MovementType == RetailStockMovementType.SaleReversal && m.SourceLineId.HasValue)
                .Select(m => m.SourceLineId!.Value)
                .ToHashSet();

            var isAlive = !DeadOrderStatuses.Contains(order.Status);
            var isPaid = OrderPaymentHelper.BuildSnapshot(order).IsPaid;
            var occurredAt = ResolveMovementTime(order);

            var postings = new List<RetailStockPosting>();
            var relieved = new Dictionary<Guid, decimal>();
            var reversals = new List<Guid>();

            foreach (var item in order.OrderItems)
            {
                if (!tracked.TryGetValue(item.ProductId, out var product))
                    continue;

                var hasSale = sales.TryGetValue(item.Id, out var sale);
                var shouldRelieve = isAlive && (item.IsReady || isPaid);

                if (shouldRelieve && !hasSale)
                {
                    var unitCost = product.CostPrice ?? 0m;
                    postings.Add(BuildSale(order, item, product, unitCost, occurredAt));
                    relieved[item.Id] = LineCost(item.Quantity, unitCost);
                    continue;
                }

                if (shouldRelieve)
                {
                    relieved[item.Id] = LineCost(item.Quantity, sale!.UnitCost ?? 0m);
                    continue;
                }

                if (hasSale && !reversed.Contains(item.Id))
                {
                    postings.Add(BuildReversal(order, sale!, occurredAt, ReversalReason(order)));
                    reversals.Add(item.Id);
                }
            }

            // Lines deleted from the order (single-item cancellation removes the row outright)
            // still have a sale on the ledger. The goods went back on the shelf, so compensate.
            var liveItemIds = order.OrderItems.Select(i => i.Id).ToHashSet();
            foreach (var (lineId, sale) in sales)
            {
                if (liveItemIds.Contains(lineId) || reversed.Contains(lineId))
                    continue;

                postings.Add(BuildReversal(order, sale, occurredAt, "order line removed"));
                reversals.Add(lineId);
            }

            if (postings.Count > 0)
                await _ledger.PostManyAsync(postings, ct);

            await ApplyCostSnapshotsAsync(order, relieved, reversals, occurredAt, ct);
            return relieved;
        }

        /// <summary>
        /// Writes the sale-time cost onto the order line and rolls the order total up from the
        /// lines. The snapshot is what makes historical profit immune to later cost changes:
        /// reporting reads <see cref="OrderItem.StockDeductedCost"/>, never today's product cost.
        /// </summary>
        private async Task ApplyCostSnapshotsAsync(
            Order order,
            IReadOnlyDictionary<Guid, decimal> relieved,
            IReadOnlyCollection<Guid> reversals,
            DateTime occurredAt,
            CancellationToken ct)
        {
            var changed = false;

            foreach (var item in order.OrderItems)
            {
                if (relieved.TryGetValue(item.Id, out var cost))
                {
                    if (item.StockDeductedAt.HasValue && item.StockDeductedCost == cost)
                        continue;

                    item.StockDeductedAt ??= occurredAt;
                    item.StockDeductedCost = cost;
                    changed = true;
                    continue;
                }

                if (!reversals.Contains(item.Id) || !item.StockDeductedAt.HasValue)
                    continue;

                item.StockDeductedAt = null;
                item.StockDeductedCost = 0m;
                changed = true;
            }

            if (!changed)
                return;

            // Summed from the lines rather than incremented, so a reversal lowers it correctly
            // and a mixed restaurant/retail order keeps the FIFO engine's contribution intact.
            var totalCost = decimal.Round(order.OrderItems.Sum(i => i.StockDeductedCost), 2, MidpointRounding.AwayFromZero);
            order.TotalCost = totalCost;
            order.NetProfit = decimal.Round(
                DeliveryAccountingHelper.ProfitRevenue(order) - totalCost, 2, MidpointRounding.AwayFromZero);

            await _context.SaveChangesAsync(ct);
        }

        private RetailStockPosting BuildSale(
            Order order,
            OrderItem item,
            RetailTrackedProduct product,
            decimal unitCost,
            DateTime occurredAt)
        {
            if (unitCost <= 0m)
            {
                _logger.LogWarning(
                    "Retail sale for product {ProductId} on order {OrderNumber} has no unit cost; its COGS snapshots as zero",
                    item.ProductId, order.OrderNumber);
            }

            return new RetailStockPosting(
                ProductId: item.ProductId,
                BranchId: item.BranchId,
                MovementType: RetailStockMovementType.Sale,
                Quantity: item.Quantity,
                UnitCost: unitCost,
                SourceDocumentType: RetailStockSourceDocument.Order,
                SourceDocumentId: order.Id,
                SourceLineId: item.Id,
                SourceReference: order.OrderNumber,
                OccurredAtUtc: occurredAt,
                PerformedByUserId: order.PaidByUserId ?? order.WaiterId,
                PerformedBySystem: PerformedBySystem,
                Notes: product.Sku);
        }

        private static RetailStockPosting BuildReversal(
            Order order,
            RetailStockMovement sale,
            DateTime occurredAt,
            string reason)
            => new(
                ProductId: sale.ProductId,
                BranchId: sale.BranchId,
                MovementType: RetailStockMovementType.SaleReversal,
                Quantity: Math.Abs(sale.Quantity),
                UnitCost: sale.UnitCost,
                SourceDocumentType: RetailStockSourceDocument.Order,
                SourceDocumentId: order.Id,
                SourceLineId: sale.SourceLineId,
                SourceReference: order.OrderNumber,
                OccurredAtUtc: occurredAt,
                PerformedByUserId: order.CanceledById,
                PerformedBySystem: PerformedBySystem,
                ReversesMovementId: sale.Id,
                Notes: reason);

        private static string ReversalReason(Order order)
            => order.Status == OrderStatus.PaymentCancelled
                ? "payment cancelled"
                : order.CancelReason is { Length: > 0 } reason
                    ? $"order cancelled: {reason}"
                    : "order cancelled";

        /// <summary>
        /// Business time of the movement: when the goods actually moved, not when the row was
        /// written. Imported history is back-dated to its order date this way.
        /// </summary>
        private static DateTime ResolveMovementTime(Order order)
        {
            var candidate = order.CanceledAt ?? order.ReadyAt ?? order.PaidAt ?? order.CreatedAt;
            return candidate.Kind == DateTimeKind.Utc
                ? candidate
                : DateTime.SpecifyKind(candidate, DateTimeKind.Utc);
        }

        private static decimal LineCost(int quantity, decimal unitCost)
            => decimal.Round(quantity * unitCost, 2, MidpointRounding.AwayFromZero);

        private async Task<Dictionary<Guid, RetailTrackedProduct>> LoadTrackedProductsAsync(
            IReadOnlyCollection<Guid> productIds,
            CancellationToken ct)
        {
            if (productIds.Count == 0)
                return new Dictionary<Guid, RetailTrackedProduct>();

            var rows = await _context.RetailProductDetails
                .AsNoTracking()
                .Where(d => productIds.Contains(d.ProductId))
                .Select(d => new RetailTrackedProduct(
                    d.ProductId,
                    d.Sku,
                    d.Product!.Name,
                    d.Product.NameAr,
                    d.Product.CostPrice))
                .ToListAsync(ct);

            return rows.ToDictionary(r => r.ProductId, r => r);
        }

        private static string Describe(RetailTrackedProduct product, decimal onHand, decimal requested, bool isArabic)
        {
            var name = isArabic && !string.IsNullOrWhiteSpace(product.NameAr) ? product.NameAr! : product.Name;
            return isArabic
                ? $"{name} (متوفر {onHand:0.##}، مطلوب {requested:0.##})"
                : $"{name} (on hand {onHand:0.##}, requested {requested:0.##})";
        }

        private sealed record RetailTrackedProduct(
            Guid ProductId,
            string Sku,
            string Name,
            string? NameAr,
            decimal? CostPrice);
    }
}
