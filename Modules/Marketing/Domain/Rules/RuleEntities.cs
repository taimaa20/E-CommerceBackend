using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    /// <summary>
    /// Versioned, immutable earning rule. Editing creates a new version (RuleVersion + 1);
    /// existing rows are never mutated, so historical transactions always reference the exact
    /// rule that produced them via <see cref="WalletTransaction.RuleVersionId"/>.
    /// </summary>
    public class EarningRule : MarketingBaseEntity
    {
        /// <summary>Stable identity shared across all versions of the same logical rule.</summary>
        public Guid RuleGroupId { get; set; }

        public int RuleVersion { get; set; }
        public bool IsCurrent { get; set; }

        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }

        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
        [MaxLength(150)] public string? NameAr { get; set; }

        public EarningRuleType RuleType { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal? PointsValue { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? PointsPerCurrencyUnit { get; set; }
        [Column(TypeName = "decimal(5,2)")] public decimal? Multiplier { get; set; }

        public Guid? TargetCategoryId { get; set; }
        public Guid? TargetProductId { get; set; }
        public OrderSource? ChannelScope { get; set; }

        public PointsApprovalMode ApprovalMode { get; set; } = PointsApprovalMode.Immediate;
        public int? ApprovalDelayDays { get; set; }

        public PointsExpirationMode ExpirationMode { get; set; } = PointsExpirationMode.Never;
        public int? ExpirationValue { get; set; }

        public Guid? CampaignId { get; set; } // FK added in Phase 4

        public int Priority { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Per-tenant marketing feature flags and default values. One row per tenant. Seeded from
    /// the existing <c>SystemSettings</c> loyalty scalars so day-1 behavior matches today.
    /// </summary>
    public class MarketingSettings : MarketingBaseEntity
    {
        [Required, MaxLength(3)] public string BaseCurrencyCode { get; set; } = "JOD";

        public bool LoyaltyEnabled { get; set; } = true;
        public bool RewardsEnabled { get; set; } = true;
        public bool PromosEnabled { get; set; } = true;
        public bool VouchersEnabled { get; set; } = true;
        public bool ReferralsEnabled { get; set; } = true;
        public bool InfluencersEnabled { get; set; } = true;
        public bool CampaignsEnabled { get; set; } = true;

        [Column(TypeName = "decimal(18,2)")] public decimal DefaultPointValue { get; set; }

        public PointsExpirationMode DefaultExpirationMode { get; set; } = PointsExpirationMode.Never;
        public int? DefaultExpirationValue { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal? MinRedeemPoints { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? MaxRedeemPoints { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal? MaxStackedDiscount { get; set; }

        /// <summary>Kill-switch to revert the OrderPaid handler to the legacy flat-earn path.</summary>
        public bool EarnDelegationEnabled { get; set; } = true;

        /// <summary>
        /// When true the tier recalculation engine may move customers DOWN a tier once they fall
        /// below its thresholds. Default false — tiers only ever promote (sticky achievements).
        /// </summary>
        public bool TierDemotionEnabled { get; set; }
    }
}
