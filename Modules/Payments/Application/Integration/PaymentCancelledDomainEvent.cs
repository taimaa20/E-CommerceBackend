using MediatR;

namespace RestaurantPos.Api.Modules.Payments.Application.Integration;

public sealed record PaymentCancelledDomainEvent(
    Guid PaymentId,
    Guid OrderId,
    Guid TenantId,
    Guid BranchId,
    decimal Amount,
    string Currency,
    string Provider,
    string? Reference,
    DateTime OccurredAt) : INotification;
