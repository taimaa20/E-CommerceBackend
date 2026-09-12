using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.DTOs
{
    // ── Wallet / profile / history (read) ────────────────────────────────────

    public class WalletDto
    {
        public Guid CustomerId { get; set; }
        public decimal AvailablePoints { get; set; }
        public decimal PendingPoints { get; set; }
        public decimal ExpiredPoints { get; set; }
        public decimal RedeemedPoints { get; set; }
        public decimal LifetimePoints { get; set; }
        public decimal LifetimeSpend { get; set; }
        public string CurrencyCode { get; set; } = "JOD";
        public int TotalOrders { get; set; }
        public int TotalVisits { get; set; }
        public string Status { get; set; } = nameof(WalletStatus.Active);
        public Guid? CurrentTierId { get; set; }
        public DateTime? LastRecalculatedAt { get; set; }
    }

    public class LoyaltyProfileDto
    {
        public Guid CustomerId { get; set; }
        public string? LoyaltyCustomerCode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Tier { get; set; } = nameof(CustomerTier.Standard);
        public decimal AvailablePoints { get; set; }
        public decimal LifetimePoints { get; set; }
        public string Status { get; set; } = nameof(WalletStatus.Active);
    }

    public class WalletTransactionDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Points { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public Guid? OrderId { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    // ── Earning rules (read/write) ───────────────────────────────────────────

    public class EarningRuleDto
    {
        public Guid Id { get; set; }
        public Guid RuleGroupId { get; set; }
        public int RuleVersion { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsActive { get; set; }
        public Guid? BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public EarningRuleType RuleType { get; set; }
        public decimal? PointsValue { get; set; }
        public decimal? PointsPerCurrencyUnit { get; set; }
        public decimal? Multiplier { get; set; }
        public Guid? TargetCategoryId { get; set; }
        public Guid? TargetProductId { get; set; }
        public OrderSource? ChannelScope { get; set; }
        public PointsApprovalMode ApprovalMode { get; set; }
        public int? ApprovalDelayDays { get; set; }
        public PointsExpirationMode ExpirationMode { get; set; }
        public int? ExpirationValue { get; set; }
        public int Priority { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class EarningRuleCreateDto
    {
        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
        [MaxLength(150)] public string? NameAr { get; set; }
        public EarningRuleType RuleType { get; set; }
        public decimal? PointsValue { get; set; }
        public decimal? PointsPerCurrencyUnit { get; set; }
        public decimal? Multiplier { get; set; }
        public Guid? TargetCategoryId { get; set; }
        public Guid? TargetProductId { get; set; }
        public OrderSource? ChannelScope { get; set; }
        public PointsApprovalMode ApprovalMode { get; set; } = PointsApprovalMode.Immediate;
        public int? ApprovalDelayDays { get; set; }
        public PointsExpirationMode ExpirationMode { get; set; } = PointsExpirationMode.Never;
        public int? ExpirationValue { get; set; }
        public int Priority { get; set; }
        public Guid? BranchId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // ── Marketing settings (read/write) ──────────────────────────────────────

    public class MarketingSettingsDto
    {
        public string BaseCurrencyCode { get; set; } = "JOD";
        public bool LoyaltyEnabled { get; set; }
        public bool RewardsEnabled { get; set; }
        public bool PromosEnabled { get; set; }
        public bool VouchersEnabled { get; set; }
        public bool ReferralsEnabled { get; set; }
        public bool InfluencersEnabled { get; set; }
        public bool CampaignsEnabled { get; set; }
        public decimal DefaultPointValue { get; set; }
        public PointsExpirationMode DefaultExpirationMode { get; set; }
        public int? DefaultExpirationValue { get; set; }
        public decimal? MinRedeemPoints { get; set; }
        public decimal? MaxRedeemPoints { get; set; }
        public decimal? MaxStackedDiscount { get; set; }
        public bool EarnDelegationEnabled { get; set; }
        public bool TierDemotionEnabled { get; set; }
    }

    // ── Seeding / cutover ────────────────────────────────────────────────────

    public class MarketingSeedResultDto
    {
        public bool SettingsCreated { get; set; }
        public int TiersCreated { get; set; }
        public bool EarningRuleCreated { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    // ── Customer merge ───────────────────────────────────────────────────────

    public class MergeCustomersRequest
    {
        [Required] public Guid SurvivorCustomerId { get; set; }
        [Required] public Guid MergedCustomerId { get; set; }
    }

    public class CustomerMergeHistoryDto
    {
        public Guid Id { get; set; }
        public Guid SurvivorCustomerId { get; set; }
        public Guid MergedCustomerId { get; set; }
        public decimal PointsTransferred { get; set; }
        public DateTime MergedAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class MergeCustomerSideDto
    {
        public Guid CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public decimal AvailablePoints { get; set; }
        public decimal LifetimePoints { get; set; }
        public string Tier { get; set; } = nameof(CustomerTier.Standard);
        public int TransactionCount { get; set; }
        public int OrderCount { get; set; }
        public bool HasWallet { get; set; }
    }

    public class CustomerMergePreviewDto
    {
        public MergeCustomerSideDto Survivor { get; set; } = new();
        public MergeCustomerSideDto Merged { get; set; } = new();

        public decimal ProjectedAvailablePoints { get; set; }
        public decimal ProjectedLifetimePoints { get; set; }
        public int ProjectedTransactionCount { get; set; }
        public int ProjectedOrderCount { get; set; }

        public bool AlreadyMerged { get; set; }
        public bool CanMerge { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    // ── Wallet admin: search & manual adjustment ─────────────────────────────

    /// <summary>A customer row for the wallet admin search grid (loyalty projection).</summary>
    public class WalletSearchItemDto
    {
        public Guid CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? LoyaltyCustomerCode { get; set; }
        public string Tier { get; set; } = nameof(CustomerTier.Standard);
        public decimal AvailablePoints { get; set; }
        public string Status { get; set; } = nameof(WalletStatus.Active);
        public bool HasWallet { get; set; }
    }

    public enum WalletAdjustmentDirection
    {
        Add = 0,
        Deduct = 1
    }

    /// <summary>Manual points adjustment (add/deduct) by an administrator. Reason is mandatory and audited.</summary>
    public class WalletAdjustRequest
    {
        public WalletAdjustmentDirection Direction { get; set; }

        [Range(0.01, 1_000_000)] public decimal Points { get; set; }

        [Required, MaxLength(300)] public string Reason { get; set; } = string.Empty;
        [MaxLength(300)] public string? ReasonAr { get; set; }
    }

    // ── Loyalty tiers (read/write) ───────────────────────────────────────────

    public class LoyaltyTierDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public CustomerTier LegacyTierEnum { get; set; }
        public decimal MinPoints { get; set; }
        public decimal MinSpend { get; set; }
        public decimal EarnMultiplier { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public List<TierBenefitDto> Benefits { get; set; } = new();
    }

    public class TierBenefitDto
    {
        public Guid Id { get; set; }
        public TierBenefitType BenefitType { get; set; }
        public decimal? Value { get; set; }
        public Guid? RefId { get; set; }
        public string? Description { get; set; }
        public string? DescriptionAr { get; set; }

        /// <summary>True when the loyalty core honors this benefit today (currently only Multiplier).</summary>
        public bool Enforced { get; set; }
    }

    public class LoyaltyTierUpsertDto
    {
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [MaxLength(100)] public string? NameAr { get; set; }
        public CustomerTier LegacyTierEnum { get; set; }
        [Range(0, 100_000_000)] public decimal MinPoints { get; set; }
        [Range(0, 100_000_000)] public decimal MinSpend { get; set; }
        [Range(0, 999.99)] public decimal EarnMultiplier { get; set; } = 1m;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public List<TierBenefitUpsertDto> Benefits { get; set; } = new();
    }

    public class TierBenefitUpsertDto
    {
        public TierBenefitType BenefitType { get; set; }
        public decimal? Value { get; set; }
        public Guid? RefId { get; set; }
        [MaxLength(200)] public string? Description { get; set; }
        [MaxLength(200)] public string? DescriptionAr { get; set; }
    }

    // ── Dashboard (read) ─────────────────────────────────────────────────────

    public class LoyaltyKpiDto
    {
        public int TotalMembers { get; set; }
        public int ActiveMembers { get; set; }
        public int NewMembers { get; set; }
        public int FrozenWallets { get; set; }
        public int AdjustmentCount { get; set; }
        public decimal TotalWalletBalance { get; set; }
        public decimal TotalPointsEarned { get; set; }
        public decimal TotalPointsRedeemed { get; set; }
        public decimal TotalPointsExpired { get; set; }
        public decimal OutstandingPoints { get; set; }
        public decimal AveragePointsPerCustomer { get; set; }
    }

    public class TierCountDto
    {
        public string Tier { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class TopCustomerDto
    {
        public Guid CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public decimal Value { get; set; }
    }

    public class LoyaltyTrendPointDto
    {
        public DateTime Date { get; set; }
        public decimal Earned { get; set; }
        public decimal Redeemed { get; set; }
        public decimal Expired { get; set; }
    }

    public class LoyaltyDashboardDto
    {
        public LoyaltyKpiDto Kpis { get; set; } = new();
        public List<TierCountDto> Tiers { get; set; } = new();
        public List<TopCustomerDto> TopEarners { get; set; } = new();
        public List<TopCustomerDto> TopRedeemers { get; set; } = new();
        public List<TopCustomerDto> MostActive { get; set; } = new();
        public List<LoyaltyTrendPointDto> Trends { get; set; } = new();
    }

    // ── Segments (read/write) ────────────────────────────────────────────────

    public class SegmentRuleDto
    {
        public Guid Id { get; set; }
        public SegmentField Field { get; set; }
        public SegmentOperator Operator { get; set; }
        public string Value { get; set; } = string.Empty;
        public int LogicGroup { get; set; }
    }

    public class SegmentDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public CustomerSegmentType Type { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastComputedAt { get; set; }
        public int MemberCount { get; set; }
        public List<SegmentRuleDto> Rules { get; set; } = new();
    }

    public class SegmentRuleUpsertDto
    {
        public SegmentField Field { get; set; }
        public SegmentOperator Operator { get; set; }
        [Required, MaxLength(120)] public string Value { get; set; } = string.Empty;
        public int LogicGroup { get; set; }
    }

    public class SegmentUpsertDto
    {
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [MaxLength(100)] public string? NameAr { get; set; }
        public CustomerSegmentType Type { get; set; } = CustomerSegmentType.Dynamic;
        public bool IsActive { get; set; } = true;
        public List<SegmentRuleUpsertDto> Rules { get; set; } = new();
    }

    public class SegmentMemberDto
    {
        public Guid CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateTime AddedAt { get; set; }
        public SegmentMemberSource Source { get; set; }
    }

    public class AddSegmentMemberRequest
    {
        [Required] public Guid CustomerId { get; set; }
    }

    public class SegmentComputeResultDto
    {
        public Guid SegmentId { get; set; }
        public int Matched { get; set; }
        public int Added { get; set; }
        public int Removed { get; set; }
    }

    // ── Campaigns (read/write) ───────────────────────────────────────────────

    public class CampaignDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Description { get; set; }
        public string? DescriptionAr { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public CampaignStatus Status { get; set; }
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public CampaignType Type { get; set; }
        public CampaignTargetType TargetType { get; set; }
        public Guid? TargetSegmentId { get; set; }
        public Guid? TargetTierId { get; set; }
        public decimal? BonusPoints { get; set; }
        public decimal? SpendThreshold { get; set; }
        public decimal? Multiplier { get; set; }
        public decimal? PointsPerCurrencyUnit { get; set; }
        public int? OrderCountThreshold { get; set; }
        public PointsExpirationMode ExpirationMode { get; set; }
        public int? ExpirationValue { get; set; }
        public List<Guid> TargetCustomerIds { get; set; } = new();
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// For Multiplier campaigns: the bonus points granted per currency unit of spend
        /// (= PointsPerCurrencyUnit × (Multiplier − 1)). This is a CAMPAIGN BONUS multiplier —
        /// extra points at the campaign's own rate, on top of normal earning — not a multiple of
        /// the order's actually-earned points. Null for non-multiplier campaigns.
        /// </summary>
        public decimal? EffectiveBonusRate { get; set; }
    }

    public class CampaignUpsertDto
    {
        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
        [MaxLength(150)] public string? NameAr { get; set; }
        [MaxLength(500)] public string? Description { get; set; }
        [MaxLength(500)] public string? DescriptionAr { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Priority { get; set; }
        public bool IsActive { get; set; } = true;
        public CampaignType Type { get; set; }
        public CampaignTargetType TargetType { get; set; } = CampaignTargetType.All;
        public Guid? TargetSegmentId { get; set; }
        public Guid? TargetTierId { get; set; }
        public decimal? BonusPoints { get; set; }
        public decimal? SpendThreshold { get; set; }
        public decimal? Multiplier { get; set; }
        public decimal? PointsPerCurrencyUnit { get; set; }
        public int? OrderCountThreshold { get; set; }
        public PointsExpirationMode ExpirationMode { get; set; } = PointsExpirationMode.Never;
        public int? ExpirationValue { get; set; }
        public List<Guid> TargetCustomerIds { get; set; } = new();
    }

    public class CampaignStatusChangeDto
    {
        public CampaignStatus Status { get; set; }
    }

    public class CampaignDashboardItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public CampaignStatus Status { get; set; }
        public CampaignType Type { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public CampaignTargetType TargetType { get; set; }
        public int AudienceSize { get; set; }
        public int Participants { get; set; }
        public decimal PointsIssued { get; set; }
        public decimal ConversionRate { get; set; }
    }

    public class CampaignKpisDto
    {
        public int TotalCampaigns { get; set; }
        public int ActiveCampaigns { get; set; }
        public int ScheduledCampaigns { get; set; }
        public int ExpiredCampaigns { get; set; }

        public int TotalParticipants { get; set; }
        public decimal TotalPointsIssued { get; set; }
        public decimal AverageParticipantsPerCampaign { get; set; }
        public decimal AveragePointsPerCampaign { get; set; }

        // Conversion across currently-active campaigns.
        public int EligibleCustomers { get; set; }
        public int ParticipatingCustomers { get; set; }
        public decimal ParticipationRate { get; set; }
    }

    public class CampaignDashboardDto
    {
        public CampaignKpisDto Summary { get; set; } = new();
        public List<CampaignDashboardItemDto> Campaigns { get; set; } = new();
    }

    // ── Rewards (read/write) ─────────────────────────────────────────────────

    public class RewardDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Description { get; set; }
        public string? DescriptionAr { get; set; }
        public decimal PointsRequired { get; set; }
        public RewardType Type { get; set; }
        public decimal? RewardValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public bool IsActive { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class RewardUpsertDto
    {
        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
        [MaxLength(150)] public string? NameAr { get; set; }
        [MaxLength(500)] public string? Description { get; set; }
        [MaxLength(500)] public string? DescriptionAr { get; set; }
        [Range(0, 100_000_000)] public decimal PointsRequired { get; set; }
        public RewardType Type { get; set; }
        public decimal? RewardValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public Guid? ProductId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class RewardRedeemRequest
    {
        [Required] public Guid CustomerId { get; set; }
        public Guid? OrderId { get; set; }
        /// <summary>Current order total — required for percentage rewards so the discount can be computed.</summary>
        public decimal? OrderTotal { get; set; }
        /// <summary>
        /// Client-supplied idempotency token (one per redeem action). The same token replays the same
        /// redemption — deducts points once, one record, one audit — protecting against double-click/retry.
        /// </summary>
        [MaxLength(64)] public string? IdempotencyKey { get; set; }
    }

    public class RewardRedemptionResultDto
    {
        public Guid RedemptionId { get; set; }
        public Guid RewardId { get; set; }
        public string RewardName { get; set; } = string.Empty;
        public RewardType Type { get; set; }
        public decimal PointsUsed { get; set; }
        public decimal DiscountAmount { get; set; }
        public Guid? FreeProductId { get; set; }
        public bool FreeDelivery { get; set; }
        public decimal RemainingPoints { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class RewardRedemptionDto
    {
        public Guid Id { get; set; }
        public Guid RewardId { get; set; }
        public string RewardName { get; set; } = string.Empty;
        public RewardType Type { get; set; }
        public decimal PointsUsed { get; set; }
        public decimal DiscountAmount { get; set; }
        public DateTime RedeemedAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class RewardTopItemDto
    {
        public Guid RewardId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Redemptions { get; set; }
        public decimal PointsRedeemed { get; set; }
    }

    public class RewardDashboardDto
    {
        public int ActiveRewards { get; set; }
        public int TotalRedemptions { get; set; }
        public decimal PointsRedeemed { get; set; }
        public List<RewardTopItemDto> MostRedeemed { get; set; } = new();
    }

    public class MarketingSettingsUpdateDto
    {
        [Required, MaxLength(3)] public string BaseCurrencyCode { get; set; } = "JOD";
        public bool LoyaltyEnabled { get; set; }
        public bool RewardsEnabled { get; set; }
        public bool PromosEnabled { get; set; }
        public bool VouchersEnabled { get; set; }
        public bool ReferralsEnabled { get; set; }
        public bool InfluencersEnabled { get; set; }
        public bool CampaignsEnabled { get; set; }
        public decimal DefaultPointValue { get; set; }
        public PointsExpirationMode DefaultExpirationMode { get; set; }
        public int? DefaultExpirationValue { get; set; }
        public decimal? MinRedeemPoints { get; set; }
        public decimal? MaxRedeemPoints { get; set; }
        public decimal? MaxStackedDiscount { get; set; }
        public bool EarnDelegationEnabled { get; set; }
        public bool TierDemotionEnabled { get; set; }
    }
}
