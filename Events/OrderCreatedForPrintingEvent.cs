using MediatR;

namespace RestaurantPos.Api.Events
{
    /// <summary>
    /// Fired AFTER an order is committed. Triggers per-kitchen ticket dispatch +
    /// customer receipt enqueue. Decoupled from the order-create pipeline so
    /// printing failures or slow printers never delay the HTTP response.
    /// </summary>
    public class OrderCreatedForPrintingEvent : INotification
    {
        public Guid OrderId { get; }
        public Guid TenantId { get; }
        /// <summary>
        /// Caller (cashier or waiter acting as cashier) who created the order
        /// — used to resolve their assigned receipt printer. Null = use tenant
        /// default → any active receipt printer.
        /// </summary>
        public Guid? CashierUserId { get; }

        public OrderCreatedForPrintingEvent(Guid orderId, Guid tenantId, Guid? cashierUserId = null)
        {
            OrderId = orderId;
            TenantId = tenantId;
            CashierUserId = cashierUserId;
        }
    }
}
