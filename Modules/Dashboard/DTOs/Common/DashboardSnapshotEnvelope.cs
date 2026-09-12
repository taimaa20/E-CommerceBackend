using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Security;

namespace RestaurantPos.Api.Modules.Dashboard.DTOs.Common
{
    /// <summary>
    /// Every snapshot DTO inherits this envelope so the frontend always knows
    /// which filter the numbers were computed against (powers the "showing X
    /// for branch Y, dd/mm to dd/mm" sub-header and the export header).
    /// </summary>
    public abstract record DashboardSnapshotEnvelope
    {
        public DashboardScope     Scope          { get; init; }
        public DateTime           GeneratedAtUtc { get; init; } = DateTime.UtcNow;
        public DateTime           WindowStartUtc { get; init; }
        public DateTime           WindowEndUtc   { get; init; }
        public DashboardFilterDto AppliedFilter  { get; init; } = new();
    }
}
