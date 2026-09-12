using RestaurantPos.Api.Modules.Dashboard.Predicates;
using RestaurantPos.Api.Modules.Dashboard.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Filters
{
    /// <summary>
    /// Scoped per request. Holds the fully-normalised filter envelope so
    /// services don't re-read DateTime.UtcNow, don't resolve presets, and
    /// can't accidentally pull a different window than their sibling
    /// calculators on the same dashboard.
    /// </summary>
    public interface IDashboardFilterContext
    {
        DashboardFilterDto Filter { get; }
        MetricWindow       Window { get; }
        DashboardScope     Scope  { get; }
        bool               Initialised { get; }

        void Initialise(DashboardFilterDto filter, MetricWindow window, DashboardScope scope);
    }
}
