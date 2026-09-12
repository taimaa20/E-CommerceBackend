using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;

namespace RestaurantPos.Api.Modules.Payments.Interfaces;

/// <summary>
/// Read repository for payment attempts owned by Payment aggregates.
/// </summary>
public interface IPaymentAttemptRepository
{
    Task<PaymentAttempt?> GetByIdAsync(Guid attemptId, CancellationToken ct);

    Task<PaymentAttempt?> GetActiveByPaymentIdAsync(Guid paymentId, CancellationToken ct);

    Task<PaymentAttempt?> GetByGatewayReferenceAsync(GatewayReference gatewayReference, CancellationToken ct);

    Task<IReadOnlyList<PaymentAttempt>> GetByPaymentIdAsync(Guid paymentId, CancellationToken ct);
}
