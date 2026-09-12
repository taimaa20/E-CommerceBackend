using RestaurantPos.Api.Modules.Retail.DTOs;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <summary>
    /// The single place every derived retail figure is computed. Kept as pure static
    /// functions so the catalogue service, future reports and tests all agree by
    /// construction rather than by convention.
    /// </summary>
    public static class RetailCalculations
    {
        /// <summary>
        /// Share of lifetime receipts below which a product is considered due for reorder.
        /// Mirrors the workbook's <c>ROUNDUP(received × 0.3, 0)</c>. Held here as the one
        /// definition; it moves to persisted retail settings when the reorder policy is
        /// implemented properly.
        /// </summary>
        private const decimal ReorderPercentOfReceipts = 0.30m;

        /// <summary>Margin is a share of the SELLING price, not a markup on cost:
        /// <c>price = cost ÷ (1 − margin)</c>. Rounded to the nearest whole unit, matching
        /// the workbook's <c>ROUND(...,0)</c>. Deliberately NOT delegated to the restaurant
        /// <c>PricingEngine</c>, which rounds up to the nearest 5.</summary>
        public static decimal? CalculateSellingPrice(decimal? costPerItem, decimal? targetMarginPercent)
        {
            if (costPerItem is not > 0m || targetMarginPercent is null)
                return null;

            var margin = targetMarginPercent.Value / 100m;
            if (margin < 0m || margin >= 1m)
                return null;

            return decimal.Round(costPerItem.Value / (1m - margin), 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Landed total for everything received: supplier invoice plus the
        /// per-unit shipping/customs allocation across those units.</summary>
        public static decimal CalculateTotalCost(
            decimal supplierCostTotal,
            decimal shippingCostPerUnit,
            decimal receivedQuantity)
            => decimal.Round(supplierCostTotal + (shippingCostPerUnit * receivedQuantity), 2, MidpointRounding.AwayFromZero);

        public static decimal? CalculateProfitPerUnit(decimal approvedSellingPrice, decimal? costPerItem)
            => costPerItem is null
                ? null
                : decimal.Round(approvedSellingPrice - costPerItem.Value, 2, MidpointRounding.AwayFromZero);

        /// <summary>Margin actually achieved at the approved price. Zero — not an error —
        /// when the price is zero, because giveaways are a normal retail case.</summary>
        public static decimal? CalculateActualMarginPercent(decimal approvedSellingPrice, decimal? costPerItem)
        {
            if (costPerItem is null)
                return null;
            if (approvedSellingPrice <= 0m)
                return 0m;

            var profit = approvedSellingPrice - costPerItem.Value;
            return decimal.Round(profit / approvedSellingPrice * 100m, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal CalculateReorderLevel(decimal receivedQuantity)
            => receivedQuantity <= 0m ? 0m : Math.Ceiling(receivedQuantity * ReorderPercentOfReceipts);

        /// <summary>Mirrors the workbook's traffic light so operators see the same answer
        /// they see today. The emoji stay in the UI; the API returns a stable code.</summary>
        public static string ResolveStockStatus(decimal stockOnHand, decimal reorderLevel)
        {
            if (stockOnHand <= 0m) return RetailStockStatuses.OutOfStock;
            if (stockOnHand < reorderLevel) return RetailStockStatuses.Urgent;
            if (stockOnHand == reorderLevel) return RetailStockStatuses.ReorderNow;
            return RetailStockStatuses.Ok;
        }
    }
}
