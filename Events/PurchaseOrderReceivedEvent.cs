using MediatR;

namespace RestaurantPos.Api.Events
{
    public class PurchaseOrderReceivedEvent : INotification
    {
        public Guid PurchaseOrderId { get; }
        public Guid TenantId { get; }

        public PurchaseOrderReceivedEvent(Guid purchaseOrderId, Guid tenantId)
        {
            PurchaseOrderId = purchaseOrderId;
            TenantId = tenantId;
        }
    }
}
