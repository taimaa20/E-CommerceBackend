using RestaurantPos.Api.Modules.Payments.Domain.Entities;

namespace RestaurantPos.Api.Modules.Payments.Interfaces;

/// <summary>
/// Read repository for persisted payment audit events.
/// </summary>
public interface IPaymentEventRepository
{
    Task<IReadOnlyList<PaymentEvent>> GetByPaymentIdAsync(Guid paymentId, CancellationToken ct);

    Task<bool> ExternalEventExistsAsync(Guid tenantId, string providerCode, string externalEventId, CancellationToken ct);

    Task<PaymentEvent?> GetByExternalEventIdAsync(Guid tenantId, string providerCode, string externalEventId, CancellationToken ct);

    Task<IReadOnlyList<PaymentEvent>> GetByPayloadHashAsync(Guid tenantId, string payloadHash, CancellationToken ct);
}
