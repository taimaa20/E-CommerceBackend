using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Per-tenant configuration for the human-friendly order display number.
    /// Returns null when a tenant has not opted in — callers must treat null as
    /// "use legacy defaults" (Monthly reset, PerOrderType scope, TA-YYYYMM-N).
    /// </summary>
    public interface IOrderNumberingConfigService
    {
        Task<OrderNumberingConfig?> GetForTenantAsync(Guid tenantId, CancellationToken ct);

        Task<OrderNumberingConfig> UpsertAsync(
            Guid tenantId,
            OrderNumberingConfig draft,
            CancellationToken ct);
    }
}
