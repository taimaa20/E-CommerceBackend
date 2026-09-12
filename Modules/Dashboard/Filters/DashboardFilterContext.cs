using RestaurantPos.Api.Modules.Dashboard.Predicates;
using RestaurantPos.Api.Modules.Dashboard.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Filters
{
    /// <inheritdoc />
    public sealed class DashboardFilterContext : IDashboardFilterContext
    {
        private DashboardFilterDto? _filter;
        private MetricWindow        _window;
        private DashboardScope      _scope;

        public DashboardFilterDto Filter => _filter
            ?? throw new InvalidOperationException("DashboardFilterContext accessed before initialisation.");
        public MetricWindow   Window => _window;
        public DashboardScope Scope  => _scope;
        public bool Initialised => _filter is not null;

        public void Initialise(DashboardFilterDto filter, MetricWindow window, DashboardScope scope)
        {
            if (_filter is not null)
                throw new InvalidOperationException("DashboardFilterContext already initialised for this request.");
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _window = window;
            _scope  = scope;
        }
    }
}
