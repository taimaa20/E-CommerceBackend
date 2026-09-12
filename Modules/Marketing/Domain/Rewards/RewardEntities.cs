using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    // Append-only enums (values never reordered/removed).

    public enum RewardType
    {
        FixedDiscount = 0,
        PercentageDiscount = 1,
        FreeProduct = 2,
        FreeDelivery = 3
    }

    public enum RewardRedemptionStatus
    {
        Completed = 0,
        Reversed = 1
    }

    /// <summary>
    /// A redeemable loyalty reward (catalog item). Redemption deducts points via the existing
    /// <c>IWalletService.RedeemAsync</c> and returns a benefit the cashier applies through the
    /// existing discount mechanism — no checkout/earning/wallet logic is re-implemented.
    /// </summary>
    public class Reward : MarketingBaseEntity
    {
        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
        [MaxLength(150)] public string? NameAr { get; set; }
        [MaxLength(500)] public string? Description { get; set; }
        [MaxLength(500)] public string? DescriptionAr { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal PointsRequired { get; set; }

        public RewardType Type { get; set; }

        /// <summary>Fixed discount amount, or percentage value, per <see cref="Type"/>. Unused for FreeProduct/FreeDelivery.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal? RewardValue { get; set; }

        /// <summary>Cap for PercentageDiscount rewards.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal? MaxDiscountAmount { get; set; }

        /// <summary>Existing product reference for FreeProduct rewards (never duplicated).</summary>
        public Guid? ProductId { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    /// <summary>Append-only record of a reward redemption (history, dashboard, audit trail).</summary>
    public class RewardRedemption : MarketingBaseEntity
    {
        public Guid RewardId { get; set; }
        public Reward? Reward { get; set; }

        public Guid CustomerId { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal PointsUsed { get; set; }

        // Immutable snapshot of what was granted (catalog may change later).
        public RewardType RewardTypeSnapshot { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? RewardValueSnapshot { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? DiscountAmountSnapshot { get; set; }
        public Guid? ProductIdSnapshot { get; set; }

        /// <summary>The wallet ledger row that deducted the points.</summary>
        public Guid WalletTransactionId { get; set; }

        public Guid? OrderId { get; set; }

        public RewardRedemptionStatus Status { get; set; } = RewardRedemptionStatus.Completed;
        public DateTime RedeemedAt { get; set; }
    }
}
