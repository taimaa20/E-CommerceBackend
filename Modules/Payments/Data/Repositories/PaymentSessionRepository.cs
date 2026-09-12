using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.Enums;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using RestaurantPos.Api.Modules.Payments.Interfaces;

namespace RestaurantPos.Api.Modules.Payments.Data.Repositories;

public sealed class PaymentSessionRepository : IPaymentSessionRepository
{
    private static readonly PaymentSessionStatus[] ActiveStatuses =
    [
        PaymentSessionStatus.Created,
        PaymentSessionStatus.Pending,
        PaymentSessionStatus.RequiresAction
    ];

    private readonly PosDbContext _context;

    public PaymentSessionRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<PaymentSession?> GetByIdAsync(Guid sessionId, CancellationToken ct)
        => _context.PaymentEnginePaymentSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == sessionId, ct);

    public Task<PaymentSession?> GetActiveByPaymentIdAsync(Guid paymentId, CancellationToken ct)
        => _context.PaymentEnginePaymentSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session =>
                session.PaymentId == paymentId &&
                ActiveStatuses.Contains(session.Status), ct);

    public Task<PaymentSession?> GetBySessionReferenceAsync(GatewayReference sessionReference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sessionReference);

        return _context.PaymentEnginePaymentSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session =>
                session.SessionReference != null &&
                session.SessionReference.ProviderCode == sessionReference.ProviderCode &&
                session.SessionReference.Reference == sessionReference.Reference, ct);
    }
}
