using MediatR;

namespace RestaurantPos.Api.Modules.Payments.Application.Integration;

public sealed record PaymentPendingDomainEvent(
    Guid PaymentId,
    Guid OrderId,
    Guid TenantId,
    Guid BranchId,
    decimal Amount,
    string Currency,
    string Provider,
    DateTime OccurredAt) : INotification;
