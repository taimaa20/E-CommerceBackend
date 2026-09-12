using MediatR;

namespace RestaurantPos.Api.Events
{
    public sealed record OrderStockProcessedEvent(Guid OrderId, Guid TenantId) : INotification;
}
