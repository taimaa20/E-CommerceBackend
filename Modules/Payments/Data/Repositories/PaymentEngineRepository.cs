using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using RestaurantPos.Api.Modules.Payments.Interfaces;

namespace RestaurantPos.Api.Modules.Payments.Data.Repositories;

public sealed class PaymentEngineRepository : IPaymentEngineRepository
{
    private readonly PosDbContext _context;

    public PaymentEngineRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken ct)
        => _context.PaymentEnginePayments
            .FirstOrDefaultAsync(payment => payment.Id == paymentId, ct);

    public Task<Payment?> GetAggregateAsync(Guid paymentId, CancellationToken ct)
        => AggregateQuery()
            .FirstOrDefaultAsync(payment => payment.Id == paymentId, ct);

    public Task<Payment?> GetByMerchantReferenceAsync(Guid tenantId, MerchantReference merchantReference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(merchantReference);

        return AggregateQuery()
            .FirstOrDefaultAsync(payment =>
                payment.TenantId == tenantId &&
                payment.MerchantReference == merchantReference, ct);
    }

    public Task<Payment?> GetByIdempotencyKeyHashAsync(Guid tenantId, string idempotencyKeyHash, CancellationToken ct)
    {
        var normalizedHash = NormalizeRequired(idempotencyKeyHash, nameof(idempotencyKeyHash));

        return AggregateQuery()
            .FirstOrDefaultAsync(payment =>
                payment.TenantId == tenantId &&
                payment.IdempotencyKeyHash == normalizedHash, ct);
    }

    public async Task<IReadOnlyList<Payment>> GetByOrderIdAsync(Guid tenantId, Guid orderId, CancellationToken ct)
        => await _context.PaymentEnginePayments
            .AsNoTracking()
            .Include(payment => payment.Attempts)
            .Include(payment => payment.Sessions)
            .AsSplitQuery()
            .Where(payment => payment.TenantId == tenantId && payment.OrderId == orderId)
            .OrderByDescending(payment => payment.CreatedAt)
            .ToListAsync(ct);

    public Task<bool> MerchantReferenceExistsAsync(Guid tenantId, MerchantReference merchantReference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(merchantReference);

        return _context.PaymentEnginePayments
            .AsNoTracking()
            .AnyAsync(payment =>
                payment.TenantId == tenantId &&
                payment.MerchantReference == merchantReference, ct);
    }

    public Task<bool> IdempotencyKeyHashExistsAsync(Guid tenantId, string idempotencyKeyHash, CancellationToken ct)
    {
        var normalizedHash = NormalizeRequired(idempotencyKeyHash, nameof(idempotencyKeyHash));

        return _context.PaymentEnginePayments
            .AsNoTracking()
            .AnyAsync(payment =>
                payment.TenantId == tenantId &&
                payment.IdempotencyKeyHash == normalizedHash, ct);
    }

    public async Task AddAsync(Payment payment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await _context.PaymentEnginePayments.AddAsync(payment, ct);
    }

    public Task LockAsync(Guid paymentId, CancellationToken ct)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment id is required.", nameof(paymentId));
        }

        return _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"PaymentEngine\".\"Payments\" WHERE \"Id\" = {paymentId} FOR UPDATE",
            ct);
    }

    private IQueryable<Payment> AggregateQuery()
        // Attempts and sessions are needed for aggregate invariants; events are append-only and can grow large.
        => _context.PaymentEnginePayments
            .Include(payment => payment.Attempts)
            .Include(payment => payment.Sessions)
            .AsSplitQuery();

    private static string NormalizeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
