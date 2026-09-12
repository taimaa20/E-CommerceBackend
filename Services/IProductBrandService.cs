using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services;

public interface IProductBrandService
{
    Task<IReadOnlyList<ProductBrandDto>> GetAllAsync(Guid tenantId, CancellationToken ct);

    /// Brand names the catalogue already uses that have no master row yet.
    Task<IReadOnlyList<UnregisteredProductBrandDto>> GetUnregisteredAsync(Guid tenantId, CancellationToken ct);

    Task<ProductBrandDto> CreateAsync(Guid tenantId, ProductBrandUpsertDto dto, CancellationToken ct);

    Task<ProductBrandDto> UpdateAsync(Guid tenantId, Guid id, ProductBrandUpsertDto dto, CancellationToken ct);

    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>
    /// Active brands keyed by <see cref="ProductBrand.Normalize"/>, for resolving a product's
    /// brand text to its artwork. One read for a whole page of products.
    /// </summary>
    Task<IReadOnlyDictionary<string, ProductBrand>> GetLookupAsync(Guid tenantId, CancellationToken ct);
}
