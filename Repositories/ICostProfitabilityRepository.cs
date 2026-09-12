using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface ICostProfitabilityRepository
    {
        Task<List<CostSharingProvider>> GetProvidersAsync(CostProviderType? type, bool includeInactive, CancellationToken ct);
        Task<CostSharingProvider?> GetProviderByIdAsync(Guid id, CancellationToken ct);
        Task<bool> ProviderCodeExistsAsync(string code, Guid? excludeId, CancellationToken ct);
        Task<CostSharingProvider> AddProviderAsync(CostSharingProvider provider, CancellationToken ct);
        Task<CostSharingProvider> UpdateProviderAsync(CostSharingProvider provider, CancellationToken ct);
        Task DeleteProviderAsync(CostSharingProvider provider, CancellationToken ct);

        Task<List<CostSharingRule>> GetRulesAsync(CostProviderType? providerType, bool includeInactive, CancellationToken ct);
        Task<CostSharingRule?> GetRuleByIdAsync(Guid id, CancellationToken ct);
        Task<CostSharingRule> AddRuleAsync(CostSharingRule rule, CancellationToken ct);
        Task<CostSharingRule> UpdateRuleAsync(CostSharingRule rule, CancellationToken ct);
        Task DeleteRuleAsync(CostSharingRule rule, CancellationToken ct);

        Task<Order?> GetOrderForSnapshotAsync(Guid orderId, CancellationToken ct);
        Task<OrderProfitabilitySnapshot?> GetSnapshotForUpdateAsync(Guid orderId, CancellationToken ct);
        Task<OrderProfitabilitySnapshot?> GetSnapshotByOrderIdAsync(Guid orderId, CancellationToken ct);
        void AddSnapshot(OrderProfitabilitySnapshot snapshot);
        Task SaveChangesAsync(CancellationToken ct);

        Task<CostProfitabilitySummaryDto> GetSummaryAsync(CostProfitabilityFilterDto filter, CancellationToken ct);
        Task<List<CostPartnerAnalyticsDto>> GetPartnerAnalyticsAsync(CostProfitabilityFilterDto filter, CancellationToken ct);
        Task<List<CostPaymentMethodAnalyticsDto>> GetPaymentMethodAnalyticsAsync(CostProfitabilityFilterDto filter, CancellationToken ct);
        Task<List<CostProductProfitabilityDto>> GetProductProfitabilityAsync(CostProfitabilityFilterDto filter, CancellationToken ct);
        Task<List<CostCustomerProfitabilityDto>> GetCustomerProfitabilityAsync(CostProfitabilityFilterDto filter, CancellationToken ct);
    }
}
