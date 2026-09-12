namespace RestaurantPos.Api.Modules.Dashboard.Filters
{
    /// <summary>
    /// Date-range presets shared by every dashboard. <see cref="Default"/> defers
    /// to the per-dashboard default declared on the controller via
    /// <see cref="Security.DashboardScopeAttribute"/>.
    /// </summary>
    public enum DashboardFilterPreset
    {
        Default      = 0,
        Today        = 1,
        Yesterday    = 2,
        ThisWeek     = 3,
        ThisMonth    = 4,
        ThisQuarter  = 5,
        ThisYear     = 6,
        Last7        = 7,
        Last30       = 8,
        LastMonth    = 9,
        Custom       = 100
    }

    public enum ComparisonMode
    {
        None             = 0,
        PreviousPeriod   = 1,
        PreviousYear     = 2
    }
}
