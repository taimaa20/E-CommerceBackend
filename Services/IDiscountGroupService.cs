using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    /// <summary>Management + lookup for affiliation-based discount groups. Tenant scoping and
    /// audit fields are owned by the service — never trusted from the request.</summary>
    public interface IDiscountGroupService
    {
        /// <summary>All groups (active and inactive) for the management screen, newest first.</summary>
        Task<List<DiscountGroupDto>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Active groups only — the options a cashier can apply to an order.</summary>
        Task<List<DiscountGroupOptionDto>> GetActiveOptionsAsync(CancellationToken ct = default);

        Task<DiscountGroupDto> CreateAsync(Guid tenantId, DiscountGroupCreateDto input, CancellationToken ct = default);

        /// <summary>Updates name/percentage/active-state/description. Returns null when not found.</summary>
        Task<DiscountGroupDto?> UpdateAsync(Guid id, DiscountGroupUpdateDto input, CancellationToken ct = default);

        /// <summary>Flips active state without touching other fields. Returns null when not found.
        /// Groups are never hard-deleted — deactivation retires them while preserving history.</summary>
        Task<DiscountGroupDto?> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    }
}
