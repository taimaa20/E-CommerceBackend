using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Cancel-reason lookup + admin CRUD. Tenants see global rows (TenantId == null)
    /// merged with their own. Mutations are scoped to the tenant's own rows — global
    /// rows are read-only from a tenant request to prevent the "claim attack" where
    /// one tenant could flip a global row to themselves.
    /// </summary>
    public interface ICancelReasonService
    {
        Task<IReadOnlyList<CancelReasonDto>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<CancelReasonDetailDto> CreateAsync(Guid tenantId, CancelReasonUpsertRequest request, CancellationToken cancellationToken = default);
        Task<CancelReasonDetailDto> UpdateAsync(Guid tenantId, int id, CancelReasonUpsertRequest request, CancellationToken cancellationToken = default);
    }
}
