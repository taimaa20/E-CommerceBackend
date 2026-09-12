namespace RestaurantPos.Api.Repositories;

/// <summary>
/// Read-only merchandising facts the public storefront needs on top of the cached public
/// product catalogue: the gallery, the customer-safe retail attributes and the creation
/// date used to order "new arrivals".
///
/// Kept apart from <see cref="IProductRepository"/> on purpose — nothing here may ever grow
/// a cost, supplier or margin field, and a separate seam makes that easy to enforce.
/// </summary>
public interface IStorefrontCatalogRepository
{
    /// <summary>Gallery images for the given products, ordered per product by SortOrder.</summary>
    Task<IReadOnlyList<StorefrontProductImageRow>> GetProductImagesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct);

    /// <summary>Customer-safe retail attributes for the given products. Products without a
    /// retail detail row simply do not appear in the result.</summary>
    Task<IReadOnlyList<StorefrontProductAttributesRow>> GetProductAttributesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct);

    /// <summary>Creation timestamps for every active product of the tenant, used only to
    /// order the "new arrivals" row. Two columns — deliberately not a full entity load.</summary>
    Task<IReadOnlyDictionary<Guid, DateTime>> GetProductCreatedAtAsync(
        Guid tenantId,
        CancellationToken ct);

    /// <summary>The branch the public store is shopping: the shopper's explicit choice when it
    /// is a real active branch, otherwise the Main Branch. Mirrors how anonymous order intake
    /// resolves the branch, so a displayed offer is an orderable offer.</summary>
    Task<Guid?> ResolveStoreBranchIdAsync(
        Guid tenantId,
        Guid? selectedBranchId,
        CancellationToken ct);

    /// <summary>The given bundle offers with their products. Only customer-facing columns —
    /// no cost, supplier or margin value is ever projected here.</summary>
    Task<IReadOnlyList<StorefrontOfferRow>> GetOffersAsync(
        IReadOnlyCollection<int> offerIds,
        CancellationToken ct);
}

public sealed class StorefrontOfferRow
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? DescriptionAr { get; init; }
    public string? ImageUrl { get; init; }
    public decimal FinalPrice { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public IReadOnlyList<StorefrontOfferProductRow> Products { get; init; } = [];
}

public sealed class StorefrontOfferProductRow
{
    public Guid ProductId { get; init; }
    public Guid? CategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageKey { get; init; }
    public int Quantity { get; init; }
    /// <summary>The product's normal effective price — DiscountedPrice when set, else BasePrice.
    /// The same expression order intake charges outside an offer.</summary>
    public decimal ListUnitPrice { get; init; }
    public bool IsProductActive { get; init; }

    /// <summary>True when the product cannot be ordered without the shopper picking an option
    /// or a required modifier. A bundle has no place to express that choice, so an offer
    /// containing such a product is not offered online.</summary>
    public bool RequiresChoice { get; init; }
}

public sealed class StorefrontProductImageRow
{
    public Guid ProductId { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public string? ImageKey { get; init; }
    public string? AltText { get; init; }
    public int SortOrder { get; init; }
}

public sealed class StorefrontProductAttributesRow
{
    public Guid ProductId { get; init; }
    /// Brand text as the catalogue holds it.
    public string? Brand { get; init; }
    /// Canonical name and Arabic name from the brand master, when one is registered.
    public string? BrandName { get; init; }
    public string? BrandNameAr { get; init; }
    /// Brand artwork from the brand master. Null when the brand has no logo yet, which is
    /// what makes the storefront fall back to the brand's name.
    public string? BrandLogoUrl { get; init; }
    public string? BrandLogoKey { get; init; }
    public string? Sku { get; init; }
    public string? SizeLabel { get; init; }
    public string? CountryOfOrigin { get; init; }
}
