using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;

namespace RestaurantPos.Api.Modules.Dashboard.DTOs.Home
{
    /// <summary>
    /// Executive overview — composed from the same calculators the per-scope
    /// dashboards use, so home numbers can never disagree with the deeper
    /// dashboards they link out to.
    /// </summary>
    public sealed record HomeSnapshotDto : DashboardSnapshotEnvelope
    {
        public HomeHeadlineDto                Headline       { get; init; } = new();
        public IReadOnlyList<TimeSeriesPointDto> RevenueTrend  { get; init; } = Array.Empty<TimeSeriesPointDto>();
        public IReadOnlyList<TimeSeriesPointDto> HourlyOrders  { get; init; } = Array.Empty<TimeSeriesPointDto>();
        public IReadOnlyList<RankedRowDto>       TopProducts   { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<RankedRowDto>       TopCashiers   { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<CategorySliceDto>   PaymentSplit  { get; init; } = Array.Empty<CategorySliceDto>();
        public IReadOnlyList<CategorySliceDto>   OrderTypeSplit{ get; init; } = Array.Empty<CategorySliceDto>();
        public IReadOnlyList<DashboardAlertDto>  Alerts        { get; init; } = Array.Empty<DashboardAlertDto>();
        public IReadOnlyList<ActivityEntryDto>   RecentActivity{ get; init; } = Array.Empty<ActivityEntryDto>();
    }

    public sealed record HomeHeadlineDto
    {
        public decimal RevenueToday      { get; init; }
        public int     OrdersToday       { get; init; }
        public int     ActiveOrders      { get; init; }
        public int     StaleOrders       { get; init; }
        public decimal NetProfit         { get; init; }
        public decimal MarginPercent     { get; init; }
        public decimal WasteCost         { get; init; }
        public decimal WastePercent      { get; init; }
        public int     ActiveTables      { get; init; }
        public int     TotalTables       { get; init; }
        public decimal AverageOrderValue { get; init; }
    }

    /// <summary>Lightweight record for "Real-time alerts" and "Recent activity" feeds.</summary>
    public sealed record DashboardAlertDto(
        string Code,
        string Severity,
        string Title,
        string? Meta,
        DateTime OccurredAtUtc,
        string? TitleAr = null,
        string? MetaAr = null);

    public sealed record ActivityEntryDto(
        string Kind,            // "OrderPaid" | "WasteLogged" | "OrderCancelled" | "ShiftClosed"
        string Title,
        string? Meta,
        DateTime OccurredAtUtc,
        string Tone);           // "green" | "blue" | "amber" | "red" | "purple" | "neutral"
}
