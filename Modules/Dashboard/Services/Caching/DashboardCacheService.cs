using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Options;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Services.Caching;
using Microsoft.Extensions.Options;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Caching
{
    /// <inheritdoc />
    public sealed class DashboardCacheService : IDashboardCacheService
    {
        private readonly ICacheService _inner;
        private readonly ITenantResolver _tenant;
        private readonly IOptionsMonitor<DashboardOptions> _dashboardOptions;

        public DashboardCacheService(
            ICacheService inner,
            ITenantResolver tenant,
            IOptionsMonitor<DashboardOptions> dashboardOptions)
        {
            _inner            = inner            ?? throw new ArgumentNullException(nameof(inner));
            _tenant           = tenant           ?? throw new ArgumentNullException(nameof(tenant));
            _dashboardOptions = dashboardOptions ?? throw new ArgumentNullException(nameof(dashboardOptions));
        }

        public Task<T?> GetOrCreateAsync<T>(
            DashboardScope scope,
            string metric,
            DashboardFilterDto filter,
            Func<Task<T>> factory,
            TimeSpan? ttl = null,
            CancellationToken ct = default) where T : class
        {
            var excludeWasteLogs = _dashboardOptions.CurrentValue.ShouldExcludeWasteLogs(scope);
            var key = DashboardCacheKey.Build(_tenant.GetTenantId(), scope, metric, filter, excludeWasteLogs);
            var effective = ttl ?? DashboardCacheTtl.Medium;
            // Use the same TTL for both sliding and absolute to give a hard upper bound.
            return _inner.GetOrCreateAsync(key, factory, slidingExpiration: effective, absoluteExpiration: effective, cancellationToken: ct);
        }

        public Task InvalidateScopeAsync(Guid tenantId, DashboardScope scope, CancellationToken ct = default)
            => _inner.RemoveByPatternAsync(DashboardCacheKey.ScopePrefix(tenantId, scope), ct);

        public Task InvalidateTenantAsync(Guid tenantId, CancellationToken ct = default)
            => _inner.RemoveByPatternAsync(DashboardCacheKey.TenantPrefix(tenantId), ct);

        public Task InvalidateMetricAsync(Guid tenantId, DashboardScope scope, string metric, CancellationToken ct = default)
            => _inner.RemoveByPatternAsync(DashboardCacheKey.MetricPrefix(tenantId, scope, metric), ct);
    }
}
