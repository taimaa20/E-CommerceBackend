using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Dashboard.Queries
{
    /// <summary>
    /// THE single source of truth for "what does a waste record cost?".
    /// Both the Inventory dashboard (waste breakdown, top-wasted, by-employee,
    /// trend, by-category) and the Home dashboard headline waste KPI consume
    /// this projection, guaranteeing they report identical cost/sale-loss for
    /// the same filter window. No other class may re-implement the cost ladder.
    /// </summary>
    /// <remarks>
    /// Cost-resolution ladder (first match wins):
    /// 1. Stored <see cref="WasteLog.CostAmount"/> when already priced.
    /// 2. Sum of linked inventory-transaction costs (FIFO/batch deductions).
    /// 3. Expiry waste → remaining quantity × source-batch unit cost.
    /// 4. Cancel waste → the order item's already-deducted stock cost.
    /// 5. Manual waste → the manually entered amount.
    /// 6. Otherwise 0 — never null, so downstream sums never produce technical values.
    /// </remarks>
    public static class WasteMetricProjection
    {
        public static IQueryable<WasteMetricRow> Project(IQueryable<WasteLog> source)
            => source.Select(w => new WasteMetricRow
            {
                Category = w.Category,
                Status = w.Status,
                ItemId = w.ProductId ?? w.MaterialId ?? w.ItemId,
                ItemName = w.ItemName,
                ItemNameAr = w.ItemNameAr ?? (w.Product != null ? w.Product.NameAr : null) ?? (w.Material != null ? w.Material.NameAr : null),
                LoggedById = w.LoggedById,
                LoggedByName = w.LoggedBy != null ? (w.LoggedBy.FullName ?? w.LoggedBy.Username) : (w.LoggedByName ?? w.CreatedBy ?? "System"),
                LoggedByNameAr = w.LoggedBy != null ? w.LoggedBy.FullNameAr : null,
                Quantity = w.Quantity,
                CreatedAt = w.WasteDate ?? w.CreatedAt,
                CostAmount = w.CostAmount > 0m
                    ? w.CostAmount
                    : (w.InventoryTransactions.Sum(t => (decimal?)t.CostAmount) ?? 0m) > 0m
                        ? w.InventoryTransactions.Sum(t => t.CostAmount)
                        : w.Category == WasteCategory.ExpiryProduct && w.SourceBatch != null
                            ? w.Quantity * w.SourceBatch.UnitCost
                            : w.Category == WasteCategory.CancelProduct && w.SourceOrderItem != null
                                ? w.SourceOrderItem.StockDeductedCost
                                : (w.Category == WasteCategory.ManualProduct || w.Category == WasteCategory.ManualMaterial) && w.Amount > 0m
                                    ? w.Amount
                                    : 0m,
                SalePriceLoss = w.SalePriceLoss > 0m
                    ? w.SalePriceLoss
                    : w.Category == WasteCategory.CancelProduct && w.Amount > 0m
                        ? w.Amount
                        : 0m
            });
    }

    /// <summary>Normalized, dashboard-facing shape of a single waste record.</summary>
    public sealed class WasteMetricRow
    {
        public WasteCategory Category { get; set; }
        public WasteLogStatus Status { get; set; }
        public Guid? ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? ItemNameAr { get; set; }
        public Guid? LoggedById { get; set; }
        public string LoggedByName { get; set; } = string.Empty;
        public string? LoggedByNameAr { get; set; }
        public decimal Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal CostAmount { get; set; }
        public decimal SalePriceLoss { get; set; }
    }
}
