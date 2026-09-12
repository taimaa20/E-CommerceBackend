using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IDeliveryZoneRepository
    {
        /// <summary>Active zones of one branch only, ordered for display. Used to build the cached select list.</summary>
        Task<List<DeliveryZone>> GetActiveAsync(Guid branchId, CancellationToken ct = default);

        /// <summary>
        /// Server-side paged + filtered management query, scoped to a branch. Returns the page slice
        /// and the total count in a single round trip's worth of work (count + page).
        /// </summary>
        Task<(List<DeliveryZone> Items, int TotalCount)> GetPagedAsync(
            Guid branchId,
            string? search,
            bool? isActive,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<DeliveryZone?> GetByIdAsync(Guid id, Guid branchId, CancellationToken ct = default);

        /// <summary>Tenant's Main Branch id — branch anchor for customer-mobile/public zone reads.</summary>
        Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct = default);

        /// <summary>Branch-scoped uniqueness check for Code, optionally excluding a given id (for updates).</summary>
        Task<bool> CodeExistsAsync(string code, Guid branchId, Guid? excludeId, CancellationToken ct = default);

        Task<DeliveryZone> AddAsync(DeliveryZone zone, CancellationToken ct = default);
        Task<DeliveryZone> UpdateAsync(DeliveryZone zone, CancellationToken ct = default);
        Task DeleteAsync(DeliveryZone zone, CancellationToken ct = default);
    }
}
