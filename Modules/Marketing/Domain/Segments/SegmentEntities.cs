using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    /// <summary>
    /// Reusable customer audience. Ships in Phase 0 schema; consumed by promos/vouchers/
    /// referrals/influencers/campaigns from Phase 2 onward so targeting has one abstraction.
    /// </summary>
    public class CustomerSegment : MarketingBaseEntity
    {
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [MaxLength(100)] public string? NameAr { get; set; }

        public CustomerSegmentType Type { get; set; } = CustomerSegmentType.Dynamic;
        public bool IsActive { get; set; } = true;

        public DateTime? LastComputedAt { get; set; }

        public ICollection<SegmentRule> Rules { get; set; } = new List<SegmentRule>();
        public ICollection<SegmentMember> Members { get; set; } = new List<SegmentMember>();
    }

    /// <summary>Membership predicate for a dynamic segment.</summary>
    public class SegmentRule : MarketingBaseEntity
    {
        public Guid SegmentId { get; set; }
        public CustomerSegment? Segment { get; set; }

        public SegmentField Field { get; set; }
        public SegmentOperator Operator { get; set; }

        [Required, MaxLength(120)] public string Value { get; set; } = string.Empty;

        /// <summary>Predicates sharing a LogicGroup are OR'd; groups are AND'd together.</summary>
        public int LogicGroup { get; set; }
    }

    /// <summary>Explicit (static / materialized) segment membership.</summary>
    public class SegmentMember : MarketingBaseEntity
    {
        public Guid SegmentId { get; set; }
        public CustomerSegment? Segment { get; set; }

        public Guid CustomerId { get; set; }

        public DateTime AddedAt { get; set; }
        public SegmentMemberSource Source { get; set; } = SegmentMemberSource.Manual;
    }
}
