using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

public sealed class WhatsAppContactRepository : IWhatsAppContactRepository
{
    private readonly PosDbContext _context;

    public WhatsAppContactRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<WhatsAppContact>> ListAsync(Guid tenantId, bool activeOnly, CancellationToken ct)
    {
        var query = _context.WhatsAppContacts
            .AsNoTracking()
            .Where(contact => contact.TenantId == tenantId);

        if (activeOnly) query = query.Where(contact => contact.IsActive);

        // Default first inside a purpose — the same order the resolver relies on, so the
        // back-office list reads in the order the storefront actually picks.
        return await query
            .OrderBy(contact => contact.Purpose)
            .ThenByDescending(contact => contact.IsDefault)
            .ThenBy(contact => contact.SortOrder)
            .ThenBy(contact => contact.Label)
            .ToListAsync(ct);
    }

    public Task<bool> HasEverConfiguredAsync(Guid tenantId, CancellationToken ct)
        // IgnoreQueryFilters drops the soft-delete filter (and the tenant one, which is why the
        // tenant is restated here), so a removed destination still counts as "was configured".
        => _context.WhatsAppContacts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(contact => contact.TenantId == tenantId, ct);

    public Task<WhatsAppContact?> GetTrackedAsync(Guid tenantId, Guid id, CancellationToken ct)
        => _context.WhatsAppContacts
            .FirstOrDefaultAsync(contact => contact.TenantId == tenantId && contact.Id == id, ct);

    public async Task<IReadOnlyList<WhatsAppContact>> GetTrackedByPurposeAsync(
        Guid tenantId,
        WhatsAppContactPurpose purpose,
        CancellationToken ct)
        => await _context.WhatsAppContacts
            .Where(contact => contact.TenantId == tenantId && contact.Purpose == purpose)
            .ToListAsync(ct);

    public Task AddAsync(WhatsAppContact contact, CancellationToken ct)
        => _context.WhatsAppContacts.AddAsync(contact, ct).AsTask();

    public Task SaveAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}
