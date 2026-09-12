namespace RestaurantPos.Api.Modules.Dashboard.Security
{
    /// <summary>
    /// Logical dashboard area. Drives default filter preset, cache namespacing,
    /// SignalR group membership and role-based authorization.
    /// </summary>
    public enum DashboardScope
    {
        Operations = 0,
        Financial  = 1,
        Inventory  = 2,
        Audit      = 3
    }
}
