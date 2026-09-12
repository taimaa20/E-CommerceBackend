using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Per-tenant configuration for shift open/close validation rules.
    /// Returns null when a tenant has not opted in — callers must treat null as
    /// "use legacy permissive behavior" (no restrictions on open/close).
    /// </summary>
    public interface IShiftRulesConfigService
    {
        Task<ShiftRulesConfig?> GetForTenantAsync(Guid tenantId, CancellationToken ct);

        Task<ShiftRulesConfig> UpsertAsync(
            Guid tenantId,
            ShiftRulesConfig draft,
            CancellationToken ct);
    }
}
