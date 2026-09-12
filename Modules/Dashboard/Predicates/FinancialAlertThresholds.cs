namespace RestaurantPos.Api.Modules.Dashboard.Predicates
{
    /// <summary>
    /// Centralized, configurable thresholds for the business alert engine.
    /// Kept as constants (no magic numbers in calculators) and structured so a
    /// future SystemSettings-backed override can replace these without touching
    /// alert logic.
    /// </summary>
    public static class FinancialAlertThresholds
    {
        // Food cost as a percentage of net revenue.
        public const decimal FoodCostPercentWarning = 35m;
        public const decimal FoodCostPercentDanger  = 40m;

        // Actual food cost over theoretical (recipe) cost.
        public const decimal FoodCostVariancePercentWarning = 15m;

        // Refunds as a percentage of gross revenue.
        public const decimal RefundPercentWarning = 5m;
        public const decimal RefundPercentDanger  = 10m;

        // Orders cancelled before payment as a percentage of recorded sales.
        public const decimal CancellationPercentWarning = 10m;

        // Discounts as a percentage of gross revenue.
        public const decimal DiscountPercentWarning = 15m;

        // Net profit margin floor — below this the operation is under-earning.
        public const decimal NetMarginPercentWarning = 10m;

        // Revenue change vs the comparison window (negative = decline).
        public const decimal RevenueDeclinePercentWarning = -10m;
    }
}
