using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using RestaurantPos.Api.Modules.Payments.Interfaces;

namespace RestaurantPos.Api.Modules.Payments.Data.Repositories;

public sealed class PaymentEventRepository : IPaymentEventRepository
{
    private readonly PosDbContext _context;

    public PaymentEventRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<PaymentEvent>> GetByPaymentIdAsync(Guid paymentId, CancellationToken ct)
        => await _context.PaymentEnginePaymentEvents
            .AsNoTracking()
            .Where(paymentEvent => paymentEvent.PaymentId == paymentId)
            .OrderBy(paymentEvent => paymentEvent.OccurredAtUtc)
            .ToListAsync(ct);

    public Task<bool> ExternalEventExistsAsync(Guid tenantId, string providerCode, string externalEventId, CancellationToken ct)
    {
        var normalizedProvider = GatewayReference.NormalizeProviderCode(providerCode);
        var normalizedEventId = NormalizeRequired(externalEventId, nameof(externalEventId));

        return _context.PaymentEnginePaymentEvents
            .AsNoTracking()
            .AnyAsync(paymentEvent =>
                paymentEvent.TenantId == tenantId &&
                paymentEvent.ProviderCode == normalizedProvider &&
                paymentEvent.ExternalEventId == normalizedEventId, ct);
    }

    public Task<PaymentEvent?> GetByExternalEventIdAsync(Guid tenantId, string providerCode, string externalEventId, CancellationToken ct)
    {
        var normalizedProvider = GatewayReference.NormalizeProviderCode(providerCode);
        var normalizedEventId = NormalizeRequired(externalEventId, nameof(externalEventId));

        return _context.PaymentEnginePaymentEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(paymentEvent =>
                paymentEvent.TenantId == tenantId &&
                paymentEvent.ProviderCode == normalizedProvider &&
                paymentEvent.ExternalEventId == normalizedEventId, ct);
    }

    public async Task<IReadOnlyList<PaymentEvent>> GetByPayloadHashAsync(Guid tenantId, string payloadHash, CancellationToken ct)
    {
        var normalizedHash = NormalizeRequired(payloadHash, nameof(payloadHash));

        return await _context.PaymentEnginePaymentEvents
            .AsNoTracking()
            .Where(paymentEvent =>
                paymentEvent.TenantId == tenantId &&
                paymentEvent.PayloadHash == normalizedHash)
            .OrderBy(paymentEvent => paymentEvent.OccurredAtUtc)
            .ToListAsync(ct);
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
