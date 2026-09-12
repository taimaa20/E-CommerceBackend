namespace RestaurantPos.Api.Modules.Payments.Interfaces;

/// <summary>
/// Payment engine transaction boundary.
/// </summary>
public interface IPaymentEngineTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);

    Task RollbackAsync(CancellationToken ct);
}
