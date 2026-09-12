using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Dashboard.Filters
{
    /// <summary>
    /// Single canonical filter envelope. Every dashboard endpoint accepts this
    /// shape, every cache key hashes from it, every export honours it.
    /// </summary>
    /// <remarks>
    /// All <see cref="DateTime"/> values are stored in UTC. The middleware
    /// converts presets to absolute UTC bounds using the tenant timezone
    /// before any service code runs, so calculators never compute periods.
    /// </remarks>
    public sealed class DashboardFilterDto
    {
        public DashboardFilterPreset Preset { get; set; } = DashboardFilterPreset.Default;

        public DateTime? StartUtc { get; set; }
        public DateTime? EndUtc   { get; set; }

        /// <summary>IANA timezone (e.g. "Africa/Cairo"). Falls back to UTC if absent.</summary>
        public string? TimeZone { get; set; }

        // Scope filters - all optional. BranchId is normalised by the dashboard
        // binding filter: omitted = current branch, Guid.Empty = all branches.
        public Guid? BranchId   { get; set; }
        /// <summary>Selected warehouse (null = all warehouses). Applied in the shared
        /// query layer to inventory stock-balance metrics that have warehouse data.</summary>
        public Guid? WarehouseId { get; set; }
        public Guid? CashierId  { get; set; }
        public Guid? EmployeeId { get; set; }
        public Guid? WaiterId   { get; set; }
        public Guid? ShiftId    { get; set; }
        public Guid? TableId    { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? ProductId  { get; set; }

        public OrderType?   OrderType     { get; set; }
        public OrderSource? OrderSource   { get; set; }
        public OrderStatus? Status        { get; set; }
        public string?      PaymentMethod { get; set; }
        public string?      Partner       { get; set; }

        public ComparisonMode Compare { get; set; } = ComparisonMode.None;

        /// <summary>One of: "hour" | "day" | "week" | "cashier" | "product" | "category".</summary>
        public string? GroupBy { get; set; }

        // Pagination — applies to list endpoints only.
        public int? Page     { get; set; }
        public int? PageSize { get; set; }

        public DashboardFilterDto Clone() => (DashboardFilterDto)MemberwiseClone();
    }
}
