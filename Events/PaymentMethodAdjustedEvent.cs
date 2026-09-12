using MediatR;

namespace RestaurantPos.Api.Events
{
    public sealed record PaymentMethodAdjustedEvent(Guid OrderId, Guid TenantId) : INotification;
}
