using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Home;

namespace RestaurantPos.Api.Modules.Dashboard.DTOs.Financial
{
    public sealed record FinancialSnapshotDto : DashboardSnapshotEnvelope
    {
        /// <summary>15-second owner health read. Every field reuses an already
        /// computed metric below — no separate calculation path.</summary>
        public ExecutiveSummaryDto ExecutiveSummary { get; init; } = new();
        /// <summary>Business alert feed (bilingual). Reuses the shared alert
        /// record so the frontend renders it with the existing AlertRow.</summary>
        public IReadOnlyList<DashboardAlertDto> Alerts { get; init; } = Array.Empty<DashboardAlertDto>();
        public RevenueBreakdownDto      Revenue       { get; init; } = new();
        public ProfitabilityDto         Profitability { get; init; } = new();
        public DeliveryMetricsDto       Delivery      { get; init; } = new();
        public CostSharingMetricsDto    CostSharing   { get; init; } = new();
        public IReadOnlyList<TimeSeriesPointDto> RevenueByDay  { get; init; } = Array.Empty<TimeSeriesPointDto>();
        public IReadOnlyList<CategorySliceDto>   ChannelSplit  { get; init; } = Array.Empty<CategorySliceDto>();
        public IReadOnlyList<RankedRowDto>       BestSellers   { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<RankedRowDto>       WorstSellers  { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<RankedRowDto>       RevenueByCashier { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<RankedRowDto>       DiscountsByUser { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<PaymentMethodAnalyticsDto> PaymentMethodAnalytics { get; init; } = Array.Empty<PaymentMethodAnalyticsDto>();
        public IReadOnlyList<OrderSourceAnalyticsDto> OrderSourceAnalytics { get; init; } = Array.Empty<OrderSourceAnalyticsDto>();
        public IReadOnlyList<PartnerAnalyticsDto> PartnerAnalytics { get; init; } = Array.Empty<PartnerAnalyticsDto>();
        public IReadOnlyList<CategorySliceDto> CardBreakdown { get; init; } = Array.Empty<CategorySliceDto>();
        public IReadOnlyList<CategorySliceDto> PayMobBreakdown { get; init; } = Array.Empty<CategorySliceDto>();
        public CancellationSummaryDto Cancellations { get; init; } = new();
        public PeriodComparisonDto?     Comparison    { get; init; }
        public IReadOnlyList<CategoryRevenueDto> CategoryRevenue { get; init; } = Array.Empty<CategoryRevenueDto>();
        public CustomerRevenueDto CustomerRevenue { get; init; } = new();
        public FoodCostDto        FoodCost        { get; init; } = new();
        public PurchasingDto      Purchasing      { get; init; } = new();

        // Profitability & sales depth (all derived from existing order/item data).
        public IReadOnlyList<ProductProfitabilityDto> ProductProfitability { get; init; } = Array.Empty<ProductProfitabilityDto>();
        public LaborCostDto    LaborCost    { get; init; } = new();
        public DiscountCostDto DiscountCost { get; init; } = new();
        public SalesMixDto     SalesMix     { get; init; } = new();
        public IReadOnlyList<RankedRowDto> RevenueByWaiter { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<RankedRowDto> RevenueByTable  { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<TimeSeriesPointDto> RevenueByHour { get; init; } = Array.Empty<TimeSeriesPointDto>();

        // Growth & purchasing intelligence.
        public IReadOnlyList<GrowthRowDto> ProductGrowth  { get; init; } = Array.Empty<GrowthRowDto>();
        public IReadOnlyList<GrowthRowDto> CategoryGrowth { get; init; } = Array.Empty<GrowthRowDto>();
        public IReadOnlyList<PurchasePriceVarianceDto> PurchasePriceVariance { get; init; } = Array.Empty<PurchasePriceVarianceDto>();

        // Decision-support detail (all from existing data).
        public IReadOnlyList<CategorySliceDto> RevenueByWeekday { get; init; } = Array.Empty<CategorySliceDto>();
        public IReadOnlyList<RefundReasonDto>  RefundReasons    { get; init; } = Array.Empty<RefundReasonDto>();
        public IReadOnlyList<TaxBreakdownDto>  TaxBreakdown     { get; init; } = Array.Empty<TaxBreakdownDto>();
    }

    public sealed record CancellationSummaryDto
    {
        public int CancelledBeforePaymentCount { get; init; }
        public decimal CancelledBeforePaymentAmount { get; init; }
        public int RefundedOrderCount { get; init; }
        public decimal RefundedAmount { get; init; }
    }

    public sealed record PaymentMethodAnalyticsDto
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? NameAr { get; init; }
        public bool IsActive { get; init; }
        public int TotalOrders { get; init; }
        public decimal GrossRevenue { get; init; }
        public int RefundedOrderCount { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal NetRevenue { get; init; }
        public decimal TotalCommission { get; init; }
        public decimal RestaurantShare { get; init; }
        public decimal CounterpartyShare { get; init; }
        public decimal NetSettlement { get; init; }
        public decimal NetRevenueAfterCostSharing { get; init; }
        public decimal AverageOrderValue { get; init; }
        public decimal RevenuePercentage { get; init; }
    }

    public sealed record OrderSourceAnalyticsDto
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string NameAr { get; init; } = string.Empty;
        public int OrderCount { get; init; }
        public decimal GrossRevenue { get; init; }
        public decimal NetRevenue { get; init; }
        public int RefundedOrderCount { get; init; }
        public decimal RefundAmount { get; init; }
        public int CancelledBeforePaymentCount { get; init; }
        public decimal CancelledBeforePaymentAmount { get; init; }
        public decimal AverageOrderValue { get; init; }
    }

    public sealed record PartnerAnalyticsDto
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? NameAr { get; init; }
        public bool IsActive { get; init; }
        public int OrderCount { get; init; }
        public decimal GrossRevenue { get; init; }
        public decimal NetRevenue { get; init; }
        public decimal DeliveryFees { get; init; }
        public decimal PartnerFees { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal TotalCommission { get; init; }
        public decimal RestaurantShare { get; init; }
        public decimal CounterpartyShare { get; init; }
        public decimal NetSettlement { get; init; }
        public decimal NetRevenueAfterCostSharing { get; init; }
        public decimal AverageOrderValue { get; init; }
        public int CashOrderCount { get; init; }
        public decimal CashRevenue { get; init; }
        public int CreditOrderCount { get; init; }
        public decimal CreditRevenue { get; init; }
    }

    public sealed record RevenueBreakdownDto
    {
        public decimal Gross         { get; init; } // Subtotal sum
        public decimal Discounts     { get; init; }
        public decimal DiscountPercentage { get; init; }
        public decimal Vouchers      { get; init; }
        public decimal Refunds       { get; init; }
        public decimal Net           { get; init; } // TotalAmount sum minus refunds
        public decimal Tax           { get; init; }
        public decimal ServiceCharge { get; init; }
        public int     OrderCount    { get; init; }
        public decimal AverageOrderValue { get; init; }
    }

    public sealed record ProfitabilityDto
    {
        public decimal Cogs              { get; init; }
        public decimal LaborCost         { get; init; }
        public decimal PrimeCost         { get; init; } // COGS + labour — the headline restaurant cost.
        public decimal PrimeCostPercentage { get; init; }
        public decimal GrossProfit       { get; init; }
        public decimal GrossMarginPercent{ get; init; }
        public decimal NetProfit         { get; init; }
        public decimal MarginPercent     { get; init; } // Net profit margin.
        public decimal ProfitPerOrder    { get; init; }
    }

    /// <summary>
    /// Executive KPI band. Each KPI carries its current value plus the previous
    /// window and change% when a comparison is active — driving the shared
    /// KpiCard contract used across dashboards.
    /// </summary>
    public sealed record ExecutiveSummaryDto
    {
        public KpiValueDto<decimal> Revenue            { get; init; } = new(0m);
        public KpiValueDto<decimal> NetProfit          { get; init; } = new(0m);
        public KpiValueDto<decimal> GrossMarginPercent { get; init; } = new(0m);
        public KpiValueDto<decimal> FoodCostPercent    { get; init; } = new(0m);
        public KpiValueDto<decimal> DiscountPercent    { get; init; } = new(0m);
        public KpiValueDto<decimal> RefundPercent      { get; init; } = new(0m);
        public KpiValueDto<decimal> AverageOrderValue  { get; init; } = new(0m);
        public KpiValueDto<decimal> Orders             { get; init; } = new(0m);
        public KpiValueDto<decimal> Customers          { get; init; } = new(0m);
        public decimal  RevenueGrowthPercent { get; init; }
        public DateTime LastUpdatedUtc       { get; init; }
    }

    public sealed record DeliveryMetricsDto
    {
        public int OrderCount { get; init; }
        public decimal Revenue { get; init; }
        public decimal Cost { get; init; }
        public decimal Profit { get; init; }
    }

    public sealed record CostSharingMetricsDto
    {
        public decimal TotalCommission { get; init; }
        public decimal CommissionPaidByRestaurant { get; init; }
        public decimal CommissionPaidByCounterparty { get; init; }
        public decimal NetRevenueAfterCostSharing { get; init; }
    }

    public sealed record PeriodComparisonDto
    {
        public RevenueBreakdownDto PreviousRevenue       { get; init; } = new();
        public ProfitabilityDto    PreviousProfitability { get; init; } = new();
        public decimal RevenueChangePercent { get; init; }
        public decimal NetProfitChangePercent { get; init; }
        public decimal MarginChangePercent   { get; init; }
    }

    public sealed record CategoryRevenueDto
    {
        public Guid? CategoryId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? NameAr { get; init; }
        public decimal Revenue { get; init; }
        public decimal RevenuePercentage { get; init; }
        public decimal Cost { get; init; }
        public decimal Profit { get; init; }
        public decimal MarginPercent { get; init; }
        public int Quantity { get; init; }
        public int OrderCount { get; init; }
    }

    /// <summary>Menu-engineering row: revenue vs real deducted cost per product.</summary>
    public sealed record ProductProfitabilityDto
    {
        public Guid? ProductId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? NameAr { get; init; }
        public decimal Revenue { get; init; }
        public decimal Cost { get; init; }
        public decimal Profit { get; init; }
        public decimal MarginPercent { get; init; }
        public int Quantity { get; init; }
    }

    public sealed record LaborCostDto
    {
        public decimal LaborCost { get; init; }
        public decimal LaborCostPercentage { get; init; }
        public decimal LaborHours { get; init; }
        public decimal RevenuePerLaborHour { get; init; }
        public int ShiftCount { get; init; }
        public bool HasLaborData { get; init; }
    }

    /// <summary>Revenue lost to each discount channel, from existing order/item data.</summary>
    public sealed record DiscountCostDto
    {
        public decimal ManualDiscounts { get; init; }
        public decimal VoucherDiscounts { get; init; }
        public decimal ComplimentaryValue { get; init; }
        public decimal PartnerDiscounts { get; init; }
        public decimal TotalDiscountCost { get; init; }
        public decimal DiscountPercentage { get; init; }
    }

    public sealed record SalesMixDto
    {
        public int TotalItems { get; init; }
        public decimal AverageItemsPerOrder { get; init; }
        public decimal ModifierRevenue { get; init; }
        public int ComplimentaryItemCount { get; init; }
        public decimal ComplimentaryValue { get; init; }
    }

    /// <summary>Current vs previous-window value with change %. Populated only
    /// when a comparison mode is active (otherwise empty — no extra query cost).</summary>
    public sealed record GrowthRowDto
    {
        public Guid? Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? NameAr { get; init; }
        public decimal Current { get; init; }
        public decimal Previous { get; init; }
        public decimal ChangePercent { get; init; }
    }

    /// <summary>Purchase price spread per raw material within the window — flags
    /// supplier price volatility. Built from PurchaseOrderItem unit prices.</summary>
    public sealed record PurchasePriceVarianceDto
    {
        public Guid RawMaterialId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? NameAr { get; init; }
        public decimal MinUnitPrice { get; init; }
        public decimal MaxUnitPrice { get; init; }
        public decimal LatestUnitPrice { get; init; }
        public decimal AverageUnitPrice { get; init; }
        public decimal VariancePercentage { get; init; }
        public int PurchaseCount { get; init; }
    }

    public sealed record CustomerRevenueDto
    {
        public int UniqueCustomers { get; init; }
        public int NewCustomers { get; init; }
        public int ReturningCustomers { get; init; }
        public decimal RevenueFromIdentified { get; init; }
        public decimal RevenueFromAnonymous { get; init; }
        public decimal AverageCustomerSpend { get; init; }
        public decimal AveragePurchaseFrequency { get; init; }
        public decimal ProfitFromIdentified { get; init; }
        public decimal AverageCustomerProfit { get; init; }
        public IReadOnlyList<RankedRowDto> TopCustomers { get; init; } = Array.Empty<RankedRowDto>();
    }

    public sealed record FoodCostDto
    {
        public decimal ActualFoodCost { get; init; }
        public decimal TheoreticalFoodCost { get; init; }
        public decimal FoodCostPercentage { get; init; }
        public decimal TheoreticalFoodCostPercentage { get; init; }
        public decimal FoodCostVariance { get; init; }
        public decimal FoodCostVariancePercentage { get; init; }
        public bool HasTheoreticalData { get; init; }
    }

    public sealed record SupplierSpendDto
    {
        public Guid SupplierId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? NameAr { get; init; }
        public decimal TotalSpend { get; init; }
        public int OrderCount { get; init; }
        public decimal SpendPercentage { get; init; }
        public decimal AverageLeadTimeDays { get; init; }
        public decimal OnTimeDeliveryPercentage { get; init; }
        public int ReceivedOrderCount { get; init; }
    }

    public sealed record RefundReasonDto
    {
        public string Reason { get; init; } = string.Empty;
        public int Count { get; init; }
        public decimal Amount { get; init; }
        public decimal Percentage { get; init; }
    }

    /// <summary>VAT/tax collected per rate — the basis for a tax filing report.</summary>
    public sealed record TaxBreakdownDto
    {
        public decimal Rate { get; init; }
        public decimal TaxableSales { get; init; }
        public decimal TaxAmount { get; init; }
        public int OrderCount { get; init; }
    }

    public sealed record PurchasingDto
    {
        public decimal TotalPurchasingCost { get; init; }
        public int PurchaseOrderCount { get; init; }
        public IReadOnlyList<SupplierSpendDto> SupplierSpend { get; init; } = Array.Empty<SupplierSpendDto>();
        public IReadOnlyList<TimeSeriesPointDto> PurchaseTrend { get; init; } = Array.Empty<TimeSeriesPointDto>();
    }
}
