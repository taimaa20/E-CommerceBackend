using MediatR;

namespace RestaurantPos.Api.Events
{
    public class OrderPaidEvent : INotification
    {
        public Guid OrderId { get; }
        public decimal TotalAmount { get; }
        public string PaymentMethod { get; }
        public Guid? UserId { get; } // Transaction triggered by whom (optional)

        public OrderPaidEvent(Guid orderId, decimal totalAmount, string paymentMethod, Guid? userId = null)
        {
            OrderId = orderId;
            TotalAmount = totalAmount;
            PaymentMethod = paymentMethod;
            UserId = userId;
        }
    }
}
