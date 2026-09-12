using MediatR;

namespace RestaurantPos.Api.Events
{
    /// <summary>
    /// Raised after an order is cancelled. Loyalty consumes this to reverse any granted points
    /// (a no-op when the order never earned — cancellation only applies to unpaid orders today).
    /// </summary>
    public class OrderCancelledEvent : INotification
    {
        public Guid OrderId { get; }
        public Guid TenantId { get; }

        public OrderCancelledEvent(Guid orderId, Guid tenantId)
        {
            OrderId = orderId;
            TenantId = tenantId;
        }
    }
}
