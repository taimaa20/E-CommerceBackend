using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;

namespace RestaurantPos.Api.Modules.Payments.Interfaces;

/// <summary>
/// Read repository for hosted checkout sessions owned by Payment aggregates.
/// </summary>
public interface IPaymentSessionRepository
{
    Task<PaymentSession?> GetByIdAsync(Guid sessionId, CancellationToken ct);

    Task<PaymentSession?> GetActiveByPaymentIdAsync(Guid paymentId, CancellationToken ct);

    Task<PaymentSession?> GetBySessionReferenceAsync(GatewayReference sessionReference, CancellationToken ct);
}
