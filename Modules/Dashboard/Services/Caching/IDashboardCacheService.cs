using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Caching
{
    /// <summary>
    /// Façade over <see cref="Services.Caching.ICacheService"/> that knows
    /// how to scope and invalidate dashboard cache by tenant / scope / event.
    /// </summary>
    public interface IDashboardCacheService
    {
        Task<T?> GetOrCreateAsync<T>(
            DashboardScope scope,
            string metric,
            DashboardFilterDto filter,
            Func<Task<T>> factory,
            TimeSpan? ttl = null,
            CancellationToken ct = default) where T : class;

        Task InvalidateScopeAsync(Guid tenantId, DashboardScope scope, CancellationToken ct = default);
        Task InvalidateTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task InvalidateMetricAsync(Guid tenantId, DashboardScope scope, string metric, CancellationToken ct = default);
    }
}
