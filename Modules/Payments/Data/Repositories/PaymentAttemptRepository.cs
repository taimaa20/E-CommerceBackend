using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.Enums;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using RestaurantPos.Api.Modules.Payments.Interfaces;

namespace RestaurantPos.Api.Modules.Payments.Data.Repositories;

public sealed class PaymentAttemptRepository : IPaymentAttemptRepository
{
    private static readonly PaymentAttemptStatus[] ActiveStatuses =
    [
        PaymentAttemptStatus.Created,
        PaymentAttemptStatus.Processing,
        PaymentAttemptStatus.RequiresAction
    ];

    private readonly PosDbContext _context;

    public PaymentAttemptRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<PaymentAttempt?> GetByIdAsync(Guid attemptId, CancellationToken ct)
        => _context.PaymentEnginePaymentAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(attempt => attempt.Id == attemptId, ct);

    public Task<PaymentAttempt?> GetActiveByPaymentIdAsync(Guid paymentId, CancellationToken ct)
        => _context.PaymentEnginePaymentAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(attempt =>
                attempt.PaymentId == paymentId &&
                ActiveStatuses.Contains(attempt.Status), ct);

    public Task<PaymentAttempt?> GetByGatewayReferenceAsync(GatewayReference gatewayReference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(gatewayReference);

        return _context.PaymentEnginePaymentAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(attempt =>
                attempt.GatewayReference != null &&
                attempt.GatewayReference.ProviderCode == gatewayReference.ProviderCode &&
                attempt.GatewayReference.Reference == gatewayReference.Reference, ct);
    }

    public async Task<IReadOnlyList<PaymentAttempt>> GetByPaymentIdAsync(Guid paymentId, CancellationToken ct)
        => await _context.PaymentEnginePaymentAttempts
            .AsNoTracking()
            .Where(attempt => attempt.PaymentId == paymentId)
            .OrderBy(attempt => attempt.AttemptNumber)
            .ToListAsync(ct);
}
