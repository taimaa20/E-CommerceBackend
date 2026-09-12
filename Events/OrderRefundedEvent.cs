using MediatR;

namespace RestaurantPos.Api.Events
{
    /// <summary>
    /// Raised after an order is refunded. Loyalty consumes this to reverse earned points
    /// proportionally to the refunded amount (full refund = full reversal). The RefundLog id
    /// scopes idempotency so re-delivery never double-reverses.
    /// </summary>
    public class OrderRefundedEvent : INotification
    {
        public Guid OrderId { get; }
        public Guid TenantId { get; }
        public decimal RefundAmount { get; }
        public decimal OrderTotal { get; }
        public Guid RefundLogId { get; }

        public OrderRefundedEvent(Guid orderId, Guid tenantId, decimal refundAmount, decimal orderTotal, Guid refundLogId)
        {
            OrderId = orderId;
            TenantId = tenantId;
            RefundAmount = refundAmount;
            OrderTotal = orderTotal;
            RefundLogId = refundLogId;
        }
    }
}
