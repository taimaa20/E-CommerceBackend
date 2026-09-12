using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;

namespace RestaurantPos.Api.Modules.Dashboard.DTOs.Inventory
{
    public sealed record InventorySnapshotDto : DashboardSnapshotEnvelope
    {
        public StockHealthDto                StockHealth      { get; init; } = new();
        public WasteBreakdownDto             Waste            { get; init; } = new();
        public IReadOnlyList<RankedRowDto>   TopWastedProducts{ get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<RankedRowDto>   WasteByEmployee  { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<TimeSeriesPointDto> WasteDailyTrend { get; init; } = Array.Empty<TimeSeriesPointDto>();
        public IReadOnlyList<RankedRowDto>   ExpiringSoon     { get; init; } = Array.Empty<RankedRowDto>();
        public IReadOnlyList<CategorySliceDto> WasteByCategory{ get; init; } = Array.Empty<CategorySliceDto>();
    }

    public sealed record StockHealthDto
    {
        public int     OutOfStockCount    { get; init; }
        public int     LowStockCount      { get; init; }
        public int     ExpiringWithin3Days{ get; init; }
        public decimal TotalInventoryValue{ get; init; }
        public int     ReorderNeededCount { get; init; }
        public decimal PeriodCogs         { get; init; } // COGS consumed in the window.
        public decimal StockTurnoverRatio { get; init; } // COGS ÷ inventory value.
        public decimal DaysInventoryOnHand{ get; init; } // How long current stock lasts at this consumption.
    }

    public sealed record WasteBreakdownDto
    {
        public int     ManualCount        { get; init; }
        public int     ExpiryCount        { get; init; }
        public int     CancellationCount  { get; init; }
        public decimal TotalCost          { get; init; }
        public decimal SalePriceLoss      { get; init; }
        public decimal WastePercentOfRevenue { get; init; }
    }
}
