using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

public interface ICurrencyRepository
{
    /// <param name="activeOnly">True for a selector; false for back-office management, which
    /// must keep seeing a deactivated row in order to reactivate it.</param>
    Task<IReadOnlyList<Currency>> ListAsync(Guid tenantId, bool activeOnly, CancellationToken ct);

    Task<Currency?> GetTrackedAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? exceptId, CancellationToken ct);

    Task AddAsync(Currency currency, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
