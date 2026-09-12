using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;

namespace RestaurantPos.Api.Modules.Dashboard.DTOs.Audit
{
    public sealed record AuditSnapshotDto : DashboardSnapshotEnvelope
    {
        public IReadOnlyList<RankedRowDto>   RefundsByEmployee       { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<RankedRowDto>   CancellationsByEmployee { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<RankedRowDto>   DiscountsByEmployee     { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<RankedRowDto>   VoucherAbuseByCashier   { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<RankedRowDto>   ManualStockReductions   { get; init; } = Array.Empty<RankedRowDto>();
        public AuditCountersDto              Counters                { get; init; } = new();
        public IReadOnlyList<TimeSeriesPointDto> CancellationTrend   { get; init; } = Array.Empty<TimeSeriesPointDto>();
        public IReadOnlyList<TimeSeriesPointDto> RefundTrend         { get; init; } = Array.Empty<TimeSeriesPointDto>();
    }

    public sealed record AuditCountersDto
    {
        public int     TotalRefunds          { get; init; }
        public decimal TotalRefundAmount     { get; init; }
        public int     TotalCancellations    { get; init; }
        public decimal TotalCancelledAmount  { get; init; }
        public int     CancelledAfterPayment { get; init; }
        public int     LateNightCancellations{ get; init; }
        public int     ZeroValueOrders       { get; init; }
        public int     ManualWasteEntries    { get; init; }
    }
}
