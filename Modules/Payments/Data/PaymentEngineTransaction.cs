using Microsoft.EntityFrameworkCore.Storage;
using RestaurantPos.Api.Modules.Payments.Interfaces;

namespace RestaurantPos.Api.Modules.Payments.Data;

internal sealed class PaymentEngineTransaction : IPaymentEngineTransaction
{
    private readonly IDbContextTransaction _transaction;

    public PaymentEngineTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    public Task CommitAsync(CancellationToken ct)
        => _transaction.CommitAsync(ct);

    public Task RollbackAsync(CancellationToken ct)
        => _transaction.RollbackAsync(ct);

    public ValueTask DisposeAsync()
        => _transaction.DisposeAsync();
}
