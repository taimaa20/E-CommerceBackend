using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services;

/// Read-only browsing surface for the public online store. Composes the existing
/// public product catalogue (pricing already resolved by IProductService), the
/// existing public category list and branch availability into one paged response
/// so the storefront never downloads the whole catalogue.
public interface IOnlineStoreCatalogService
{
    Task<OnlineStoreCatalogDto> GetCatalogAsync(
        Guid tenantId,
        Guid? branchId,
        bool isArabic,
        OnlineStoreCatalogQuery query,
        CancellationToken ct);

    /// Merchandising payload for the storefront home: departments plus a small number of
    /// product rows. Every row is derived from data the catalogue already holds — a row
    /// with nothing to show is omitted rather than filled with a claim the data cannot back.
    Task<OnlineStoreHomeDto> GetHomeAsync(
        Guid tenantId,
        Guid? branchId,
        bool isArabic,
        CancellationToken ct);

    /// One product with its gallery, customer-safe attributes and same-category neighbours.
    Task<OnlineStoreProductPageDto> GetProductPageAsync(
        Guid tenantId,
        Guid? branchId,
        bool isArabic,
        Guid productId,
        CancellationToken ct);

    /// Type-ahead suggestions for the storefront search box. Bounded on both sides.
    Task<OnlineStoreSuggestionsDto> GetSuggestionsAsync(
        Guid tenantId,
        Guid? branchId,
        bool isArabic,
        string? search,
        CancellationToken ct);
}
