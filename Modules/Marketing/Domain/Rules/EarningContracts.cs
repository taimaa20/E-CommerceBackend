using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    /// <summary>A single order line reduced to what earning rules need (pure input).</summary>
    public sealed record OrderLine(Guid ProductId, Guid? CategoryId, decimal LineTotal);

    /// <summary>Immutable order facts the earning engine evaluates against. No EF types — fully testable.</summary>
    public sealed record OrderEarningContext
    {
        public required decimal Total { get; init; }
        public required decimal Subtotal { get; init; }
        public required decimal Discount { get; init; }
        public required decimal Tax { get; init; }
        public OrderSource Source { get; init; }
        public bool IsFirstOrder { get; init; }
        public bool IsBirthday { get; init; }
        public IReadOnlyList<OrderLine> Lines { get; init; } = Array.Empty<OrderLine>();
    }

    /// <summary>A versioned earning rule flattened for evaluation.</summary>
    public sealed record EarningRuleSnapshot
    {
        public required Guid RuleVersionId { get; init; }
        public EarningRuleType RuleType { get; init; }
        public decimal? PointsValue { get; init; }
        public decimal? PointsPerCurrencyUnit { get; init; }
        public decimal? Multiplier { get; init; }
        public Guid? TargetCategoryId { get; init; }
        public Guid? TargetProductId { get; init; }
        public OrderSource? ChannelScope { get; init; }
        public PointsApprovalMode ApprovalMode { get; init; }
        public int? ApprovalDelayDays { get; init; }
        public PointsExpirationMode ExpirationMode { get; init; }
        public int? ExpirationValue { get; init; }
        public int Priority { get; init; }
    }

    /// <summary>One computed earning result, ready to become a wallet credit.</summary>
    public sealed record PointsAward
    {
        public required Guid RuleVersionId { get; init; }
        public required decimal Points { get; init; }
        public bool IsPending { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public PointsApprovalMode ApprovalMode { get; init; }
    }
}
