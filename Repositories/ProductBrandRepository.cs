using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

public sealed class ProductBrandRepository : IProductBrandRepository
{
    private readonly PosDbContext _context;

    public ProductBrandRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ProductBrand>> GetAllAsync(Guid tenantId, CancellationToken ct)
        => await _context.ProductBrands
            .AsNoTracking()
            .Where(brand => brand.TenantId == tenantId)
            .OrderBy(brand => brand.SortOrder)
            .ThenBy(brand => brand.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProductBrand>> GetActiveAsync(Guid tenantId, CancellationToken ct)
        => await _context.ProductBrands
            .AsNoTracking()
            .Where(brand => brand.TenantId == tenantId && brand.IsActive)
            .OrderBy(brand => brand.SortOrder)
            .ThenBy(brand => brand.Name)
            .ToListAsync(ct);

    public async Task<ProductBrand?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => await _context.ProductBrands
            .FirstOrDefaultAsync(brand => brand.TenantId == tenantId && brand.Id == id, ct);

    public async Task<bool> NameExistsAsync(Guid tenantId, string normalizedName, Guid? exceptId, CancellationToken ct)
        => await _context.ProductBrands
            .AsNoTracking()
            .AnyAsync(brand => brand.TenantId == tenantId
                && brand.NormalizedName == normalizedName
                && (!exceptId.HasValue || brand.Id != exceptId.Value), ct);

    public async Task<IReadOnlyList<CatalogBrandUsageRow>> GetCatalogBrandUsageAsync(Guid tenantId, CancellationToken ct)
    {
        // Projected to the brand column alone: no cost, supplier or margin value is read here.
        var rows = await _context.RetailProductDetails
            .AsNoTracking()
            .Where(detail => detail.TenantId == tenantId && detail.Brand != null && detail.Brand != string.Empty)
            .Join(_context.Products.AsNoTracking().Where(product => product.TenantId == tenantId && product.IsActive),
                detail => detail.ProductId,
                product => product.Id,
                (detail, _) => detail.Brand!)
            .ToListAsync(ct);

        // Grouped after the read because the normalization rule lives in the domain, not in SQL.
        // The display name is the first spelling the catalogue uses, so the merchant recognises it.
        return rows
            .GroupBy(ProductBrand.Normalize)
            .Where(group => group.Key.Length > 0)
            .Select(group => new CatalogBrandUsageRow
            {
                Name = group.First().Trim(),
                NormalizedName = group.Key,
                ProductCount = group.Count()
            })
            .OrderByDescending(row => row.ProductCount)
            .ThenBy(row => row.Name)
            .ToList();
    }

    public async Task AddAsync(ProductBrand brand, CancellationToken ct)
        => await _context.ProductBrands.AddAsync(brand, ct);

    public Task SaveAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}
