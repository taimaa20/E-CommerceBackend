using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IDeliveryZoneService
    {
        /// <summary>Cached, active-only list of the current branch for the cashier selector (autocomplete + dropdown).</summary>
        Task<List<DeliveryZoneSelectDto>> GetActiveForSelectAsync(CancellationToken ct = default);

        /// <summary>
        /// Cached, active-only list of the tenant's Main Branch. For customer-mobile/public flows,
        /// whose orders always land on the Main Branch and which have no staff branch context.
        /// </summary>
        Task<List<DeliveryZoneSelectDto>> GetActiveForMainBranchAsync(CancellationToken ct = default);

        /// <summary>Server-side paged management listing with search + active filter.</summary>
        Task<DeliveryZonePagedResultDto> GetPagedAsync(
            string? search,
            bool? isActive,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<DeliveryZoneDto> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// O(1) server-side resolve of a single active zone's full snapshot (fee/cost/name/mode)
        /// for order creation. Reads from the cached active list — no DB hit on the cashier hot path.
        /// Returns the full DTO (incl. internal cost) for snapshotting; never exposed to cashiers via the API.
        /// </summary>
        Task<DeliveryZoneDto?> ResolveActiveAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Same resolution against an explicitly supplied branch, for callers that have already
        /// established which branch they are acting for and have no signed-in user to derive it
        /// from — public storefront order intake. The branch-scoping rule is identical: a zone
        /// belonging to another branch still fails to resolve.
        /// </summary>
        Task<DeliveryZoneDto?> ResolveActiveForBranchAsync(Guid id, Guid branchId, CancellationToken ct = default);

        Task<DeliveryZoneDto> CreateAsync(DeliveryZoneCreateDto dto, CancellationToken ct = default);
        Task<DeliveryZoneDto> UpdateAsync(Guid id, DeliveryZoneUpdateDto dto, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
    }
}
