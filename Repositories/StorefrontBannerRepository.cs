using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

public sealed class StorefrontBannerRepository : IStorefrontBannerRepository
{
    private readonly PosDbContext _context;

    public StorefrontBannerRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<StorefrontBanner>> GetAllAsync(Guid tenantId, CancellationToken ct)
        => await _context.StorefrontBanners
            .AsNoTracking()
            .Where(banner => banner.TenantId == tenantId)
            .OrderBy(banner => banner.Placement)
            .ThenBy(banner => banner.SortOrder)
            .ThenByDescending(banner => banner.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StorefrontBanner>> GetActiveAsync(
        Guid tenantId,
        StorefrontBannerPlacement placement,
        CancellationToken ct)
        => await _context.StorefrontBanners
            .AsNoTracking()
            .Where(banner => banner.TenantId == tenantId
                && banner.Placement == placement
                && banner.IsActive)
            .OrderBy(banner => banner.SortOrder)
            .ThenByDescending(banner => banner.CreatedAt)
            .ToListAsync(ct);

    public async Task<StorefrontBanner?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => await _context.StorefrontBanners
            .FirstOrDefaultAsync(banner => banner.TenantId == tenantId && banner.Id == id, ct);

    public async Task AddAsync(StorefrontBanner banner, CancellationToken ct)
        => await _context.StorefrontBanners.AddAsync(banner, ct);

    public Task SaveAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}
