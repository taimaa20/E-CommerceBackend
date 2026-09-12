using RestaurantPos.Api.Modules.Dashboard.Filters;

namespace RestaurantPos.Api.Modules.Dashboard.Security
{
    /// <summary>
    /// Declarative marker placed on dashboard controllers. The
    /// <see cref="DashboardFilterMiddleware"/> reads it to resolve
    /// the per-dashboard default preset (Today / ThisWeek / ThisMonth)
    /// without each controller having to repeat itself.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class DashboardScopeAttribute : Attribute
    {
        public DashboardScope Scope { get; }
        public DashboardFilterPreset DefaultPreset { get; init; } = DashboardFilterPreset.Today;

        public DashboardScopeAttribute(DashboardScope scope)
        {
            Scope = scope;
        }
    }
}
