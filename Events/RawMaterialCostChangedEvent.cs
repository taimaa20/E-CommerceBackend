using MediatR;

namespace RestaurantPos.Api.Events
{
    public sealed record RawMaterialCostChangedEvent(
        Guid RawMaterialId,
        Guid TenantId) : INotification;
}
