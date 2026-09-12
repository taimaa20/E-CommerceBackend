using MediatR;

namespace RestaurantPos.Api.Events
{
    /// <summary>
    /// Fired when items are appended to an existing order. Only the newly added
    /// items are routed to their kitchens — already-printed items are not reprinted.
    /// </summary>
    public class OrderItemsAddedForPrintingEvent : INotification
    {
        public Guid OrderId { get; }
        public Guid TenantId { get; }
        public IReadOnlyList<Guid> AddedOrderItemIds { get; }

        public OrderItemsAddedForPrintingEvent(Guid orderId, Guid tenantId, IReadOnlyList<Guid> addedOrderItemIds)
        {
            OrderId = orderId;
            TenantId = tenantId;
            AddedOrderItemIds = addedOrderItemIds ?? Array.Empty<Guid>();
        }
    }
}
