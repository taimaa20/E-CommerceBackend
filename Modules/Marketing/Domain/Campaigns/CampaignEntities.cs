using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    // Append-only enums (values never reordered/removed).

    public enum CampaignStatus
    {
        Draft = 0,
        Scheduled = 1,
        Active = 2,
        Paused = 3,
        Expired = 4,
        Archived = 5
    }

    public enum CampaignType
    {
        /// <summary>Fixed bonus points when an order meets a spend threshold.</summary>
        BonusPoints = 0,
        /// <summary>Extra points proportional to spend (e.g. 2× weekend) — issued as bonus on top of base earning.</summary>
        Multiplier = 1,
        /// <summary>One-off reward once a customer completes N qualifying orders in the window.</summary>
        FixedReward = 2
    }

    public enum CampaignTargetType
    {
        All = 0,
        Segment = 1,
        Tier = 2,
        CustomerList = 3
    }

    /// <summary>
    /// A marketing campaign that issues bonus loyalty points by consuming the existing wallet engine
    /// (via <c>IWalletService.CreditAsync</c>) — it never re-implements earning or wallet logic.
    /// </summary>
    public class Campaign : MarketingBaseEntity
    {
        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
        [MaxLength(150)] public string? NameAr { get; set; }
        [MaxLength(500)] public string? Description { get; set; }
        [MaxLength(500)] public string? DescriptionAr { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
        public int Priority { get; set; }

        /// <summary>Admin master switch, independent of the computed lifecycle <see cref="Status"/>.</summary>
        public bool IsActive { get; set; } = true;

        public CampaignType Type { get; set; }

        public CampaignTargetType TargetType { get; set; } = CampaignTargetType.All;
        public Guid? TargetSegmentId { get; set; }
        public Guid? TargetTierId { get; set; }

        // ── Bonus configuration (interpreted per Type) ──
        [Column(TypeName = "decimal(18,2)")] public decimal? BonusPoints { get; set; }        // BonusPoints / FixedReward award
        [Column(TypeName = "decimal(18,2)")] public decimal? SpendThreshold { get; set; }     // BonusPoints: min order subtotal
        [Column(TypeName = "decimal(5,2)")] public decimal? Multiplier { get; set; }          // Multiplier factor (e.g. 2.0)
        [Column(TypeName = "decimal(18,2)")] public decimal? PointsPerCurrencyUnit { get; set; } // Multiplier base rate
        public int? OrderCountThreshold { get; set; }                                          // FixedReward: orders required

        public PointsExpirationMode ExpirationMode { get; set; } = PointsExpirationMode.Never;
        public int? ExpirationValue { get; set; }

        public ICollection<CampaignCustomer> TargetCustomers { get; set; } = new List<CampaignCustomer>();
    }

    /// <summary>Manual customer-list targeting row (used when TargetType = CustomerList).</summary>
    public class CampaignCustomer : MarketingBaseEntity
    {
        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }
        public Guid CustomerId { get; set; }
    }
}
