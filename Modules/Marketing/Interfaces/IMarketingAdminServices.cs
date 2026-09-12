using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.DTOs;

namespace RestaurantPos.Api.Modules.Marketing.Interfaces
{
    /// <summary>Admin CRUD for versioned earning rules (edit = new immutable version).</summary>
    public interface IEarningRuleService
    {
        Task<List<EarningRuleDto>> GetCurrentAsync(bool includeInactive, CancellationToken ct);
        Task<List<EarningRuleDto>> GetVersionsAsync(Guid ruleGroupId, CancellationToken ct);
        Task<EarningRuleDto> GetByIdAsync(Guid id, CancellationToken ct);
        Task<EarningRuleDto> CreateAsync(EarningRuleCreateDto dto, CancellationToken ct);
        Task<EarningRuleDto> UpdateAsync(Guid ruleGroupId, EarningRuleCreateDto dto, CancellationToken ct);
        Task DeactivateAsync(Guid ruleGroupId, CancellationToken ct);
    }

    /// <summary>Per-tenant marketing feature flags &amp; defaults. Get auto-seeds a safe row.</summary>
    public interface IMarketingSettingsService
    {
        Task<MarketingSettingsDto> GetAsync(CancellationToken ct);
        Task<MarketingSettingsDto> UpdateAsync(MarketingSettingsUpdateDto dto, CancellationToken ct);
    }

    /// <summary>
    /// One-click cutover helper: seeds the per-tenant legacy-parity config (settings, tier
    /// multipliers, base PerAmount rule) so enabling delegation reproduces current earn exactly.
    /// Idempotent — safe to call repeatedly.
    /// </summary>
    public interface IMarketingSeedService
    {
        Task<MarketingSeedResultDto> SeedLegacyParityAsync(CancellationToken ct);
    }

    /// <summary>Read-only wallet/profile/history projections for staff &amp; admin surfaces.</summary>
    public interface IWalletQueryService
    {
        Task<WalletDto?> GetWalletAsync(Guid customerId, CancellationToken ct);
        Task<LoyaltyProfileDto?> GetProfileAsync(Guid customerId, bool isArabic, CancellationToken ct);
        Task<PaginatedResponse<WalletTransactionDto>> GetHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken ct);

        /// <summary>Throws if the customer does not belong to the resolved tenant (guards mutating endpoints).</summary>
        Task EnsureCustomerInCurrentTenantAsync(Guid customerId, CancellationToken ct);

        /// <summary>Tenant-scoped customer search (name / phone / loyalty code) for the wallet admin grid.</summary>
        Task<PaginatedResponse<WalletSearchItemDto>> SearchAsync(string? term, int page, int pageSize, bool isArabic, CancellationToken ct);
    }

    /// <summary>Admin CRUD for configurable loyalty tiers and their benefits. Edits never recompute customers.</summary>
    public interface ITierAdminService
    {
        Task<List<LoyaltyTierDto>> GetAllAsync(bool includeInactive, CancellationToken ct);
        Task<LoyaltyTierDto> GetByIdAsync(Guid id, CancellationToken ct);
        Task<LoyaltyTierDto> CreateAsync(LoyaltyTierUpsertDto dto, CancellationToken ct);
        Task<LoyaltyTierDto> UpdateAsync(Guid id, LoyaltyTierUpsertDto dto, CancellationToken ct);
        Task DeactivateAsync(Guid id, CancellationToken ct);
    }

    /// <summary>Admin CRUD + membership management for customer segments (static &amp; dynamic).</summary>
    public interface ISegmentService
    {
        Task<List<SegmentDto>> GetAllAsync(bool includeInactive, CancellationToken ct);
        Task<SegmentDto> GetByIdAsync(Guid id, CancellationToken ct);
        Task<SegmentDto> CreateAsync(SegmentUpsertDto dto, CancellationToken ct);
        Task<SegmentDto> UpdateAsync(Guid id, SegmentUpsertDto dto, CancellationToken ct);
        Task DeactivateAsync(Guid id, CancellationToken ct);

        Task<PaginatedResponse<SegmentMemberDto>> GetMembersAsync(Guid id, int page, int pageSize, bool isArabic, CancellationToken ct);
        Task AddMemberAsync(Guid id, Guid customerId, CancellationToken ct);
        Task RemoveMemberAsync(Guid id, Guid customerId, CancellationToken ct);

        Task<SegmentComputeResultDto> ComputeAsync(Guid id, CancellationToken ct);
    }

    /// <summary>
    /// Admin CRUD + lifecycle for marketing campaigns. Campaigns issue points only through the
    /// existing wallet engine (via the processing job) — this service never credits wallets directly.
    /// </summary>
    public interface ICampaignService
    {
        Task<List<CampaignDto>> GetAllAsync(bool includeArchived, CancellationToken ct);
        Task<CampaignDto> GetByIdAsync(Guid id, CancellationToken ct);
        Task<CampaignDto> CreateAsync(CampaignUpsertDto dto, CancellationToken ct);
        Task<CampaignDto> UpdateAsync(Guid id, CampaignUpsertDto dto, CancellationToken ct);
        Task<CampaignDto> ChangeStatusAsync(Guid id, CampaignStatus target, CancellationToken ct);
        Task<CampaignDashboardDto> GetDashboardAsync(CancellationToken ct);
    }

    /// <summary>
    /// Simple rewards catalog + redemption. Redemption deducts points through the existing
    /// <c>IWalletService.RedeemAsync</c> and records the redemption — no checkout/wallet changes.
    /// </summary>
    public interface IRewardService
    {
        Task<List<RewardDto>> GetAllAsync(bool includeInactive, bool isArabic, CancellationToken ct);
        Task<RewardDto> GetByIdAsync(Guid id, bool isArabic, CancellationToken ct);
        Task<RewardDto> CreateAsync(RewardUpsertDto dto, CancellationToken ct);
        Task<RewardDto> UpdateAsync(Guid id, RewardUpsertDto dto, CancellationToken ct);
        Task SetActiveAsync(Guid id, bool active, CancellationToken ct);

        /// <summary>Active, in-window rewards for the POS/mobile catalog.</summary>
        Task<List<RewardDto>> GetActiveCatalogAsync(bool isArabic, CancellationToken ct);

        Task<RewardRedemptionResultDto> RedeemAsync(Guid rewardId, RewardRedeemRequest request, bool isArabic, CancellationToken ct);
        Task<PaginatedResponse<RewardRedemptionDto>> GetRedemptionsAsync(Guid customerId, int page, int pageSize, bool isArabic, CancellationToken ct);
        Task<RewardDashboardDto> GetDashboardAsync(CancellationToken ct);
    }
}
