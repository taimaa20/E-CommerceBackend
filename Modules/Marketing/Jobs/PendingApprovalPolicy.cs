using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>Immutable facts the approval decision needs (pure input — no EF types).</summary>
    public sealed record PendingApprovalContext
    {
        public required PointsApprovalMode ApprovalMode { get; init; }
        public int? ApprovalDelayDays { get; init; }
        public required DateTime EarnedAtUtc { get; init; }
        public OrderStatus? OrderStatus { get; init; }
    }

    /// <summary>
    /// Pure decision for whether a pending earn is now eligible for approval. No new approval modes —
    /// just resolves the four existing ones against order state / elapsed time.
    /// </summary>
    public static class PendingApprovalPolicy
    {
        public static bool IsDue(PendingApprovalContext c, DateTime nowUtc) => c.ApprovalMode switch
        {
            PointsApprovalMode.Immediate => true,
            PointsApprovalMode.OnPaymentConfirmed =>
                c.OrderStatus is OrderStatus.Paid or OrderStatus.Completed,
            PointsApprovalMode.OnOrderCompleted =>
                c.OrderStatus is OrderStatus.Completed,
            PointsApprovalMode.Delayed =>
                c.EarnedAtUtc.AddDays(Math.Max(0, c.ApprovalDelayDays ?? 0)) <= nowUtc,
            _ => false
        };
    }
}
