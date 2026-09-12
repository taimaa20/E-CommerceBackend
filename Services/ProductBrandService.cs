using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services;

/// <summary>
/// Back-office management of brand identity. A brand row exists to carry artwork and an Arabic
/// name for a brand the catalogue already names; it never prices, stocks or sells anything.
/// </summary>
public sealed class ProductBrandService : IProductBrandService
{
    private readonly IProductBrandRepository _repository;
    private readonly ILogger<ProductBrandService> _logger;

    public ProductBrandService(
        IProductBrandRepository repository,
        ILogger<ProductBrandService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ProductBrandDto>> GetAllAsync(Guid tenantId, CancellationToken ct)
    {
        var brands = await _repository.GetAllAsync(tenantId, ct);
        var usage = (await _repository.GetCatalogBrandUsageAsync(tenantId, ct))
            .ToDictionary(row => row.NormalizedName, row => row.ProductCount);
        return brands.Select(brand => Map(brand, usage.GetValueOrDefault(brand.NormalizedName))).ToList();
    }

    public async Task<IReadOnlyList<UnregisteredProductBrandDto>> GetUnregisteredAsync(Guid tenantId, CancellationToken ct)
    {
        var known = (await _repository.GetAllAsync(tenantId, ct))
            .Select(brand => brand.NormalizedName)
            .ToHashSet(StringComparer.Ordinal);
        return (await _repository.GetCatalogBrandUsageAsync(tenantId, ct))
            .Where(row => !known.Contains(row.NormalizedName))
            .Select(row => new UnregisteredProductBrandDto { Name = row.Name, ProductCount = row.ProductCount })
            .ToList();
    }

    public async Task<ProductBrandDto> CreateAsync(Guid tenantId, ProductBrandUpsertDto dto, CancellationToken ct)
    {
        var normalized = await ValidateAsync(tenantId, dto, null, ct);
        var brand = new ProductBrand { Id = Guid.NewGuid(), TenantId = tenantId };
        Apply(brand, dto, normalized);

        await _repository.AddAsync(brand, ct);
        await _repository.SaveAsync(ct);

        _logger.LogInformation("Product brand {BrandId} created for tenant {TenantId}", brand.Id, tenantId);
        return Map(brand, await CountProductsAsync(tenantId, normalized, ct));
    }

    public async Task<ProductBrandDto> UpdateAsync(Guid tenantId, Guid id, ProductBrandUpsertDto dto, CancellationToken ct)
    {
        var normalized = await ValidateAsync(tenantId, dto, id, ct);
        var brand = await _repository.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("Brand", id);
        Apply(brand, dto, normalized);
        await _repository.SaveAsync(ct);

        _logger.LogInformation("Product brand {BrandId} updated for tenant {TenantId}", id, tenantId);
        return Map(brand, await CountProductsAsync(tenantId, normalized, ct));
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var brand = await _repository.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("Brand", id);

        // Soft delete: products keep their brand text and simply fall back to the name.
        brand.DeletedAt = DateTime.UtcNow;
        brand.IsActive = false;
        await _repository.SaveAsync(ct);

        _logger.LogInformation("Product brand {BrandId} removed for tenant {TenantId}", id, tenantId);
    }

    public async Task<IReadOnlyDictionary<string, ProductBrand>> GetLookupAsync(Guid tenantId, CancellationToken ct)
    {
        var brands = await _repository.GetActiveAsync(tenantId, ct);
        // First wins: NormalizedName is unique per tenant, so a duplicate here is impossible.
        return brands
            .GroupBy(brand => brand.NormalizedName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    private async Task<string> ValidateAsync(Guid tenantId, ProductBrandUpsertDto dto, Guid? exceptId, CancellationToken ct)
    {
        var normalized = ProductBrand.Normalize(dto.Name);
        if (normalized.Length == 0)
            throw new ValidationException("Brand name is required.");
        if (!string.IsNullOrWhiteSpace(dto.LogoKey) && string.IsNullOrWhiteSpace(dto.LogoUrl))
            throw new ValidationException("A brand logo needs its image address.");
        if (await _repository.NameExistsAsync(tenantId, normalized, exceptId, ct))
            throw new ValidationException("A brand with this name already exists.");
        return normalized;
    }

    private async Task<int> CountProductsAsync(Guid tenantId, string normalizedName, CancellationToken ct)
        => (await _repository.GetCatalogBrandUsageAsync(tenantId, ct))
            .FirstOrDefault(row => row.NormalizedName == normalizedName)?.ProductCount ?? 0;

    private static void Apply(ProductBrand brand, ProductBrandUpsertDto dto, string normalizedName)
    {
        brand.Name = dto.Name.Trim();
        brand.NormalizedName = normalizedName;
        brand.NameAr = TrimToNull(dto.NameAr);
        brand.LogoUrl = TrimToNull(dto.LogoUrl);
        brand.LogoKey = TrimToNull(dto.LogoKey);
        brand.IsActive = dto.IsActive;
        brand.SortOrder = dto.SortOrder;
    }

    private static ProductBrandDto Map(ProductBrand brand, int productCount) => new()
    {
        Id = brand.Id,
        Name = brand.Name,
        NameAr = brand.NameAr,
        LogoUrl = brand.LogoUrl,
        LogoKey = brand.LogoKey,
        IsActive = brand.IsActive,
        SortOrder = brand.SortOrder,
        ProductCount = productCount
    };

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
