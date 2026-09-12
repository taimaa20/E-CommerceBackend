using MediatR;

namespace RestaurantPos.Api.Events
{
    public class HighLaborCostAlertEvent : INotification
    {
        public DateTime Date { get; }
        public decimal TotalRevenue { get; }
        public decimal TotalLaborCost { get; }
        public decimal Ratio { get; }

        public HighLaborCostAlertEvent(DateTime date, decimal totalRevenue, decimal totalLaborCost, decimal ratio)
        {
            Date = date;
            TotalRevenue = totalRevenue;
            TotalLaborCost = totalLaborCost;
            Ratio = ratio;
        }
    }
}
