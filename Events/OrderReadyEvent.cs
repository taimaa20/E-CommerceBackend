using MediatR;

namespace RestaurantPos.Api.Events
{
    public class OrderReadyEvent : INotification
    {
        public Guid OrderId { get; }
        public string OrderNumber { get; }
        public Guid? UserId { get; } // Optional: who marked it ready
        public OrderReadyEvent(Guid orderId, string orderNumber, Guid? userId = null)
        {
            OrderId = orderId;
            OrderNumber = orderNumber;
            UserId = userId;
        }
    }
}
