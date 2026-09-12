using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public class ShiftRulesConfigDto
    {
        public ActiveShiftRule ActiveShiftRule { get; set; } = ActiveShiftRule.Unlimited;

        public bool RequireAllOrdersPaid      { get; set; }
        public bool RequireAllOrdersReady     { get; set; }
        public bool RequireAllOrdersServed    { get; set; }
        public bool RequireAllOrdersCompleted { get; set; }

        public bool AllowPendingOrders               { get; set; } = true;
        public bool AllowPreparingOrders             { get; set; } = true;
        public bool AllowReadyOrders                 { get; set; } = true;
        public bool AllowPendingDeliveryOrders       { get; set; } = true;
        public bool AllowPendingCancellationRequests { get; set; } = true;

        public bool AllowForcedShiftClose         { get; set; }
        public bool AllowShiftCloseWithOpenOrders { get; set; }
    }
}
