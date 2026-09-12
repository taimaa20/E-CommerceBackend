using MediatR;

namespace RestaurantPos.Api.Events
{
    public class LowMarginAlertEvent : INotification
    {
        public Guid OrderId { get; }
        public string OrderNumber { get; }
        public decimal MarginPercentage { get; }
        public decimal NetProfit { get; }

        public LowMarginAlertEvent(Guid orderId, string orderNumber, decimal marginPercentage, decimal netProfit)
        {
            OrderId = orderId;
            OrderNumber = orderNumber;
            MarginPercentage = marginPercentage;
            NetProfit = netProfit;
        }
    }
}
