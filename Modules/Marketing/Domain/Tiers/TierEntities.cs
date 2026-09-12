using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    /// <summary>
    /// Canonical, configurable loyalty tier. Each tier maps to a legacy <see cref="CustomerTier"/>
    /// enum value so existing readers of <c>Customer.Tier</c> keep working while admins define
    /// unlimited tiers. Schema frozen in Phase 0; logic built in Phase 1.
    /// </summary>
    public class LoyaltyTier : MarketingBaseEntity
    {
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [MaxLength(100)] public string? NameAr { get; set; }

        public CustomerTier LegacyTierEnum { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal MinPoints { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal MinSpend { get; set; }
        [Column(TypeName = "decimal(5,2)")] public decimal EarnMultiplier { get; set; } = 1m;

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<TierBenefit> Benefits { get; set; } = new List<TierBenefit>();
    }

    /// <summary>Why a tier transition happened (audit + reporting).</summary>
    public enum TierChangeTrigger
    {
        Recalculation = 0,
        Manual = 1,
        Merge = 2,
    }

    /// <summary>
    /// Append-only record of a customer tier transition. Never updated — each promotion/demotion
    /// adds a row so the full tier journey is preserved for reporting and audit.
    /// </summary>
    public class CustomerTierHistory : MarketingBaseEntity
    {
        public Guid CustomerId { get; set; }
        public Guid? WalletId { get; set; }

        public Guid? PreviousTierId { get; set; }
        public Guid? NewTierId { get; set; }

        public CustomerTier PreviousLegacyTier { get; set; }
        public CustomerTier NewLegacyTier { get; set; }

        public TierChangeTrigger Trigger { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal LifetimePointsAtChange { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal LifetimeSpendAtChange { get; set; }

        public DateTime ChangedAt { get; set; }
    }

    /// <summary>A single benefit granted by a tier.</summary>
    public class TierBenefit : MarketingBaseEntity
    {
        public Guid TierId { get; set; }
        public LoyaltyTier? Tier { get; set; }

        public TierBenefitType BenefitType { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal? Value { get; set; }

        /// <summary>Optional reference (e.g. reward/voucher template) resolved in later phases.</summary>
        public Guid? RefId { get; set; }

        [MaxLength(200)] public string? Description { get; set; }
        [MaxLength(200)] public string? DescriptionAr { get; set; }
    }
}
