using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Payments.Interfaces;

namespace RestaurantPos.Api.Modules.Payments.Data;

public sealed class PaymentEngineUnitOfWork : IPaymentEngineUnitOfWork
{
    private readonly PosDbContext _context;

    public PaymentEngineUnitOfWork(
        PosDbContext context,
        IPaymentEngineRepository payments,
        IPaymentAttemptRepository attempts,
        IPaymentSessionRepository sessions,
        IPaymentEventRepository events)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Payments = payments ?? throw new ArgumentNullException(nameof(payments));
        Attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
        Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        Events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public IPaymentEngineRepository Payments { get; }

    public IPaymentAttemptRepository Attempts { get; }

    public IPaymentSessionRepository Sessions { get; }

    public IPaymentEventRepository Events { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct)
        => _context.SaveChangesAsync(ct);

    public async Task<IPaymentEngineTransaction> BeginTransactionAsync(CancellationToken ct)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new PaymentEngineTransaction(transaction);
    }
}
