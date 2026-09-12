using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;

namespace RestaurantPos.Api.Modules.Dashboard.DTOs.Operations
{
    public sealed record OperationsSnapshotDto : DashboardSnapshotEnvelope
    {
        public OrderCountersDto         Orders         { get; init; } = new();
        public LiveRevenueDto           Revenue        { get; init; } = new();
        public OperationsHealthDto      Health         { get; init; } = new();
        public IReadOnlyList<TimeSeriesPointDto> HourlySeries { get; init; } = Array.Empty<TimeSeriesPointDto>();
        public IReadOnlyList<CategorySliceDto>   OrderTypeSplit { get; init; } = Array.Empty<CategorySliceDto>();
        public IReadOnlyList<RankedRowDto>       TopProducts    { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<AlertDto>           Alerts         { get; init; } = Array.Empty<AlertDto>();

        /// <summary>Live order pipeline — newest first, used by the operations table widget.</summary>
        public IReadOnlyList<LivePipelineOrderDto> RecentOrders { get; init; } = Array.Empty<LivePipelineOrderDto>();
    }

    /// <summary>Single row in the live operations pipeline table.</summary>
    public sealed record LivePipelineOrderDto(
        Guid    Id,
        string  OrderNumber,
        string  TableName,
        string  OrderType,           // "DineIn" | "Takeaway"
        int     ItemCount,
        decimal Total,
        string  Status,              // "New" | "Preparing" | "Ready" | "Served" | "Paid" | "Cancelled" | "Completed"
        string  Tone,                // "blue" | "amber" | "green" | "red" | "neutral"
        DateTime CreatedAtUtc,
        int     ElapsedSeconds,
        string? Cashier,
        string? CashierAr = null);

    public sealed record OrderCountersDto
    {
        public int Total              { get; init; }
        public int Active             { get; init; }
        public int Waiting            { get; init; }
        public int Preparing          { get; init; }
        public int Ready              { get; init; }
        public int Paid               { get; init; }
        public int Served             { get; init; }
        public int Cancelled          { get; init; }
        public int Refunded           { get; init; }
        public int StaleOverThreshold { get; init; }
        public decimal AverageOrderValue { get; init; }
    }

    public sealed record LiveRevenueDto
    {
        public decimal Confirmed       { get; init; }
        public decimal Gross           { get; init; }
        public decimal Discounts       { get; init; }
        public decimal Net             { get; init; }
        public decimal Pending         { get; init; }
        public decimal Cash            { get; init; }
        public decimal Card            { get; init; }
        public decimal CashPercentage  { get; init; }
        public decimal CardPercentage  { get; init; }
        public IReadOnlyList<CategorySliceDto> PaymentBreakdown { get; init; } = Array.Empty<CategorySliceDto>();
        public IReadOnlyList<PaymentOrderTypeSliceDto> PaymentOrderTypeBreakdown { get; init; } = Array.Empty<PaymentOrderTypeSliceDto>();
    }

    public sealed record PaymentOrderTypeSliceDto(
        string OrderType,
        string PaymentMethod,
        string? PaymentMethodAr,
        decimal Value,
        int Count,
        decimal? Percentage = null);

    public sealed record OperationsHealthDto
    {
        public int ActiveDineInTables { get; init; }
        public int KitchenQueueLength { get; init; }
        public int OnlineOrdersLive   { get; init; }
        public int ActiveCashierSessions { get; init; }
        public double AveragePrepMinutes { get; init; }

        /// <summary>Number of orders with a ReadyAt timestamp that fed AveragePrepMinutes.
        /// Zero means no qualifying orders → the UI shows an empty state instead of "0".</summary>
        public int PrepOrderCount { get; init; }
    }
}
