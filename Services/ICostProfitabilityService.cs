using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public interface ICostProfitabilityService
    {
        Task<List<CostSharingProviderDto>> GetProvidersAsync(CostProviderType? type, bool includeInactive, CancellationToken ct);
        Task<CostSharingProviderDto> GetProviderByIdAsync(Guid id, CancellationToken ct);
        Task<CostSharingProviderDto> CreateProviderAsync(CostSharingProviderUpsertDto dto, CancellationToken ct);
        Task<CostSharingProviderDto> UpdateProviderAsync(Guid id, CostSharingProviderUpsertDto dto, CancellationToken ct);
        Task DeleteProviderAsync(Guid id, CancellationToken ct);

        Task<List<CostSharingRuleDto>> GetRulesAsync(CostProviderType? providerType, bool includeInactive, CancellationToken ct);
        Task<CostSharingRuleDto> GetRuleByIdAsync(Guid id, CancellationToken ct);
        Task<CostSharingRuleDto> CreateRuleAsync(CostSharingRuleUpsertDto dto, CancellationToken ct);
        Task<CostSharingRuleDto> UpdateRuleAsync(Guid id, CostSharingRuleUpsertDto dto, CancellationToken ct);
        Task DeleteRuleAsync(Guid id, CancellationToken ct);

        Task<OrderProfitabilitySnapshotDto?> CaptureSnapshotAsync(
            Guid orderId,
            ProfitabilitySnapshotSource source,
            CancellationToken ct);

        Task<OrderProfitabilitySnapshotDto> GetOrderSnapshotAsync(Guid orderId, CancellationToken ct);
        Task<CostProfitabilityDashboardDto> GetDashboardAsync(CostProfitabilityFilterDto filter, CancellationToken ct);
    }
}
