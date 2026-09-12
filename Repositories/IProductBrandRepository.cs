using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

public interface IProductBrandRepository
{
    Task<IReadOnlyList<ProductBrand>> GetAllAsync(Guid tenantId, CancellationToken ct);

    /// Active brands only — what the storefront resolves a product's brand text against.
    Task<IReadOnlyList<ProductBrand>> GetActiveAsync(Guid tenantId, CancellationToken ct);

    Task<ProductBrand?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<bool> NameExistsAsync(Guid tenantId, string normalizedName, Guid? exceptId, CancellationToken ct);

    /// Brand text as the catalogue itself holds it, with how many sellable products carry each.
    Task<IReadOnlyList<CatalogBrandUsageRow>> GetCatalogBrandUsageAsync(Guid tenantId, CancellationToken ct);

    Task AddAsync(ProductBrand brand, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}

/// One distinct brand name found on catalogue products, as the merchant typed it.
public sealed class CatalogBrandUsageRow
{
    public string Name { get; init; } = string.Empty;
    public string NormalizedName { get; init; } = string.Empty;
    public int ProductCount { get; init; }
}
