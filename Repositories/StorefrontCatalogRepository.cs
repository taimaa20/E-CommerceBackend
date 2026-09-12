using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

public sealed class StorefrontCatalogRepository : IStorefrontCatalogRepository
{
    private readonly PosDbContext _context;

    public StorefrontCatalogRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<StorefrontProductImageRow>> GetProductImagesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct)
    {
        if (productIds.Count == 0) return [];

        return await _context.ProductImages
            .AsNoTracking()
            .Where(image => image.TenantId == tenantId && productIds.Contains(image.ProductId))
            .OrderBy(image => image.ProductId)
            .ThenBy(image => image.SortOrder)
            .Select(image => new StorefrontProductImageRow
            {
                ProductId = image.ProductId,
                ImageUrl = image.ImageUrl,
                ImageKey = image.ImageKey,
                AltText = image.AltText,
                SortOrder = image.SortOrder,
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StorefrontProductAttributesRow>> GetProductAttributesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct)
    {
        if (productIds.Count == 0) return [];

        // Projected column by column so no cost, supplier or margin value ever leaves the
        // database on a public request.
        var details = await _context.RetailProductDetails
            .AsNoTracking()
            .Where(detail => detail.TenantId == tenantId && productIds.Contains(detail.ProductId))
            .Select(detail => new
            {
                detail.ProductId,
                detail.Brand,
                detail.Sku,
                detail.SizeLabel,
                detail.CountryOfOrigin,
            })
            .ToListAsync(ct);

        // Product -> Brand -> logo. One read for the whole page, matched on the domain's own
        // normalization rule rather than on raw text, so spelling and spacing cannot split a
        // brand in two. A product whose brand has no master row keeps its plain name.
        var brands = await _context.ProductBrands
            .AsNoTracking()
            .Where(brand => brand.TenantId == tenantId && brand.IsActive)
            .Select(brand => new { brand.NormalizedName, brand.Name, brand.NameAr, brand.LogoUrl, brand.LogoKey })
            .ToListAsync(ct);
        var byName = brands
            .GroupBy(brand => brand.NormalizedName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return details.Select(detail =>
        {
            byName.TryGetValue(ProductBrand.Normalize(detail.Brand), out var brand);
            return new StorefrontProductAttributesRow
            {
                ProductId = detail.ProductId,
                Brand = detail.Brand,
                BrandName = brand?.Name,
                BrandNameAr = brand?.NameAr,
                BrandLogoUrl = brand?.LogoUrl,
                BrandLogoKey = brand?.LogoKey,
                Sku = detail.Sku,
                SizeLabel = detail.SizeLabel,
                CountryOfOrigin = detail.CountryOfOrigin,
            };
        }).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, DateTime>> GetProductCreatedAtAsync(
        Guid tenantId,
        CancellationToken ct)
        => await _context.Products
            .AsNoTracking()
            .Where(product => product.TenantId == tenantId && product.IsActive)
            .Select(product => new { product.Id, product.CreatedAt })
            .ToDictionaryAsync(row => row.Id, row => row.CreatedAt, ct);

    public async Task<Guid?> ResolveStoreBranchIdAsync(
        Guid tenantId,
        Guid? selectedBranchId,
        CancellationToken ct)
        => await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.TenantId == tenantId
                && branch.IsActive
                && (selectedBranchId.HasValue ? branch.Id == selectedBranchId.Value : branch.IsMainBranch))
            .Select(branch => (Guid?)branch.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<StorefrontOfferRow>> GetOffersAsync(
        IReadOnlyCollection<int> offerIds,
        CancellationToken ct)
    {
        if (offerIds.Count == 0) return [];

        return await _context.Offers
            .AsNoTracking()
            .Where(offer => offer.IsActive && offerIds.Contains(offer.Id))
            .Select(offer => new StorefrontOfferRow
            {
                Id = offer.Id,
                Name = offer.Name,
                NameAr = offer.NameAr,
                Description = offer.Description,
                DescriptionAr = offer.DescriptionAr,
                ImageUrl = offer.ImageUrl,
                FinalPrice = offer.FinalPrice,
                StartDate = offer.StartDate,
                EndDate = offer.EndDate,
                StartTime = offer.StartTime,
                EndTime = offer.EndTime,
                Products = offer.OfferProducts
                    .Select(link => new StorefrontOfferProductRow
                    {
                        ProductId = link.ProductId,
                        CategoryId = link.Product.CategoryId,
                        Name = link.Product.Name,
                        NameAr = link.Product.NameAr,
                        ImageUrl = link.Product.ImageUrl,
                        ImageKey = link.Product.ImageKey,
                        Quantity = link.Quantity,
                        ListUnitPrice = link.Product.DiscountedPrice ?? link.Product.BasePrice,
                        IsProductActive = link.Product.IsActive,
                        RequiresChoice = link.Product.Options.Any(option => option.IsActive)
                            || link.Product.ProductModifierGroups.Any(group =>
                                group.ModifierGroup.IsRequired || group.ModifierGroup.MinSelection > 0),
                    })
                    .ToList(),
            })
            .ToListAsync(ct);
    }
}
