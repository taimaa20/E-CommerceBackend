using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

public sealed class CurrencyRepository : ICurrencyRepository
{
    private readonly PosDbContext _context;

    public CurrencyRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<Currency>> ListAsync(Guid tenantId, bool activeOnly, CancellationToken ct)
    {
        var query = _context.Currencies
            .AsNoTracking()
            .Where(currency => currency.TenantId == tenantId);

        if (activeOnly) query = query.Where(currency => currency.IsActive);

        return await query
            .OrderBy(currency => currency.SortOrder)
            .ThenBy(currency => currency.Code)
            .ToListAsync(ct);
    }

    public Task<Currency?> GetTrackedAsync(Guid tenantId, Guid id, CancellationToken ct)
        => _context.Currencies
            .FirstOrDefaultAsync(currency => currency.TenantId == tenantId && currency.Id == id, ct);

    public Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? exceptId, CancellationToken ct)
        => _context.Currencies
            .AsNoTracking()
            .AnyAsync(currency => currency.TenantId == tenantId
                && currency.Code == code
                && (!exceptId.HasValue || currency.Id != exceptId.Value), ct);

    public Task AddAsync(Currency currency, CancellationToken ct)
        => _context.Currencies.AddAsync(currency, ct).AsTask();

    public Task SaveAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}
