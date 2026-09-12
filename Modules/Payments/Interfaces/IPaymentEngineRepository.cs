using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;

namespace RestaurantPos.Api.Modules.Payments.Interfaces;

/// <summary>
/// Repository for the Payment aggregate root.
/// </summary>
public interface IPaymentEngineRepository
{
    Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken ct);

    Task<Payment?> GetAggregateAsync(Guid paymentId, CancellationToken ct);

    Task<Payment?> GetByMerchantReferenceAsync(Guid tenantId, MerchantReference merchantReference, CancellationToken ct);

    Task<Payment?> GetByIdempotencyKeyHashAsync(Guid tenantId, string idempotencyKeyHash, CancellationToken ct);

    Task<IReadOnlyList<Payment>> GetByOrderIdAsync(Guid tenantId, Guid orderId, CancellationToken ct);

    Task<bool> MerchantReferenceExistsAsync(Guid tenantId, MerchantReference merchantReference, CancellationToken ct);

    Task<bool> IdempotencyKeyHashExistsAsync(Guid tenantId, string idempotencyKeyHash, CancellationToken ct);

    Task AddAsync(Payment payment, CancellationToken ct);

    Task LockAsync(Guid paymentId, CancellationToken ct);
}
