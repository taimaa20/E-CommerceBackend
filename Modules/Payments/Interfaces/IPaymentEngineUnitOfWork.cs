namespace RestaurantPos.Api.Modules.Payments.Interfaces;

/// <summary>
/// Coordinates payment engine repositories and database transactions.
/// </summary>
public interface IPaymentEngineUnitOfWork
{
    IPaymentEngineRepository Payments { get; }

    IPaymentAttemptRepository Attempts { get; }

    IPaymentSessionRepository Sessions { get; }

    IPaymentEventRepository Events { get; }

    Task<int> SaveChangesAsync(CancellationToken ct);

    Task<IPaymentEngineTransaction> BeginTransactionAsync(CancellationToken ct);
}
