using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs;

public sealed class OnlineMenuOrderContextDto
{
    public IReadOnlyList<OnlineShoppingBranchDto> Branches { get; init; } = [];
    public IReadOnlyList<OnlineShoppingProductAvailabilityDto> ProductAvailability { get; init; } = [];
    public IReadOnlyList<OnlineShoppingDeliveryZoneDto> DeliveryZones { get; init; } = [];
    public IReadOnlyList<OnlineShoppingPaymentMethodDto> PaymentMethods { get; init; } = [];
}

public sealed class OnlineShoppingBranchDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public string Code { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public bool IsMainBranch { get; init; }
}

public sealed class OnlineShoppingProductAvailabilityDto
{
    public Guid ProductId { get; init; }
    public bool IsAvailableOnline { get; init; }
    public bool InventoryTrackingEnabled { get; init; }
    public int? AvailableQuantity { get; init; }
}

public sealed class OnlineShoppingDeliveryZoneDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public decimal DeliveryFee { get; init; }
}

/// One payment method the merchant enabled for this branch, straight from the PaymentMethod
/// registry via BranchPaymentMethod. The storefront renders whatever this list contains and
/// decides nothing about it — availability, order, icon and the reference-number rule are all
/// the merchant's configuration. Choosing one records the method on the order, never a payment.
public sealed class OnlineShoppingPaymentMethodDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public string Code { get; init; } = string.Empty;
    /// The merchant's configured icon key, if any. Presentation only.
    public string? Icon { get; init; }
    /// The merchant's ordering. The storefront lists methods in this order.
    public int DisplayOrder { get; init; }
    /// The existing PaymentMethod.RequiresReferenceNumber rule. When true the shopper must
    /// supply the transfer/transaction reference at checkout.
    public bool RequiresReferenceNumber { get; init; }
    /// Classification for presentation only (which icon to draw). Never gates availability.
    public bool IsCash { get; init; }
    public bool IsCard { get; init; }
    public bool IsOnline { get; init; }
}

public sealed class OnlineShoppingCheckoutRequest
{
    [Required]
    public Guid OrderId { get; init; }

    [Required, StringLength(64, MinimumLength = 16)]
    public string ClientOrderUuid { get; init; } = string.Empty;
}

public sealed class OnlineShoppingOrderStatusDto
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string OrderType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CustomerStatus { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public bool IsPaid { get; init; }
    public string PaymentStatus { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string? PaymentMethodAr { get; init; }
    /// The reference the shopper declared at checkout, when their method required one.
    public string? SubmittedPaymentReference { get; init; }
    public DateTime? ScheduledFor { get; init; }
    public DateTime? DispatchedAt { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerEmail { get; init; }
    public string CustomerPhone { get; init; } = string.Empty;
    /// The one readable line the driver is given, exactly as printed on the staff ticket.
    public string? DeliveryAddress { get; init; }
    public string? DeliveryNotes { get; init; }
    public string? DeliveryZoneName { get; init; }
    public decimal? DeliveryFee { get; init; }
    public OnlineShoppingBranchDto Branch { get; init; } = new();
    public IReadOnlyList<OnlineShoppingOrderItemDto> Items { get; init; } = [];
}

public sealed class OnlineShoppingOrderItemDto
{
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
}

public sealed class OnlineShoppingReceiptAccessDto
{
    public string ReceiptUrl { get; init; } = string.Empty;
}

// ─── PUBLIC STORE BROWSING (paged catalog + guest order lookup) ──────────────

public sealed class OnlineStoreCategoryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public int SortOrder { get; init; }
    public int ProductCount { get; init; }

    /// <summary>
    /// How many sellable products in this department carry a genuine price reduction
    /// (DiscountedPrice below BasePrice). Zero when the department has none.
    /// </summary>
    public int DiscountedProductCount { get; init; }

    /// <summary>
    /// The largest of those reductions, as a whole percent. Zero when nothing in the
    /// department is reduced — the storefront shows no discount mark in that case.
    /// Bundle offers are deliberately NOT folded in here: an offer's saving belongs to the
    /// offer, and the storefront combines the two from the offers payload it already holds.
    /// </summary>
    public int MaxDiscountPercent { get; init; }
}

/// Ordering offered by the storefront toolbar. Every option is derived from
/// data the catalogue already returns — no new fields, no invented ranking.
public enum OnlineStoreCatalogSort
{
    Featured = 0,
    PriceAscending = 1,
    PriceDescending = 2,
    NameAscending = 3
}

public sealed class OnlineStoreCatalogQuery
{
    public Guid? CategoryId { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
    public OnlineStoreCatalogSort Sort { get; init; } = OnlineStoreCatalogSort.Featured;
}

public sealed class OnlineStoreCatalogDto
{
    public IReadOnlyList<OnlineStoreCategoryDto> Categories { get; init; } = [];
    public IReadOnlyList<ProductDto> Products { get; init; } = [];
    public IReadOnlyList<OnlineShoppingProductAvailabilityDto> ProductAvailability { get; init; } = [];
    /// Customer-facing merchandising attributes for the products on this page. Additive
    /// side-list, exactly like ProductAvailability — existing clients may ignore it.
    public IReadOnlyList<OnlineStoreProductDetailsDto> ProductDetails { get; init; } = [];
    public Guid? CategoryId { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}

/// One gallery image as the storefront renders it. The PRIMARY image is not here —
/// it stays on ProductDto.ImageUrl / ImageKey, which every other surface already reads.
public sealed class OnlineStoreImageDto
{
    public string ImageUrl { get; init; } = string.Empty;
    public string? ImageKey { get; init; }
    public string? AltText { get; init; }
}

/// Customer-facing product attributes. Deliberately excludes cost, supplier, margin and
/// stock figures — those live on RetailProductDetailDto and never leave the admin surface.
public sealed class OnlineStoreProductDetailsDto
{
    public Guid ProductId { get; init; }
    public string? Brand { get; init; }
    /// Brand identity resolved from the brand master. Null on every field means the brand is
    /// only a name, and the storefront shows that name instead of artwork.
    public string? BrandName { get; init; }
    public string? BrandNameAr { get; init; }
    public string? BrandLogoUrl { get; init; }
    public string? BrandLogoKey { get; init; }
    public string? Sku { get; init; }
    public string? SizeLabel { get; init; }
    public string? CountryOfOrigin { get; init; }
    /// Additional images beyond the primary one, in display order.
    public IReadOnlyList<OnlineStoreImageDto> Images { get; init; } = [];
}

/// One merchandising row on the storefront home. A section is omitted entirely when the
/// catalogue cannot support it, so the store never advertises something it does not have.
public sealed class OnlineStoreSectionDto
{
    /// Stable identifier the storefront maps to a localized heading:
    /// "newArrivals" | "onSale" | "explore".
    public string Key { get; init; } = string.Empty;
    public IReadOnlyList<ProductDto> Products { get; init; } = [];
}

public sealed class OnlineStoreHomeDto
{
    public IReadOnlyList<OnlineStoreCategoryDto> Categories { get; init; } = [];
    public IReadOnlyList<OnlineStoreSectionDto> Sections { get; init; } = [];
    public IReadOnlyList<OnlineShoppingProductAvailabilityDto> ProductAvailability { get; init; } = [];
    public IReadOnlyList<OnlineStoreProductDetailsDto> ProductDetails { get; init; } = [];
    /// Total sellable-or-listed products, so the home page can label its "view all" link.
    public int TotalProductCount { get; init; }
}

/// Everything one product detail page renders, in a single round trip.
public sealed class OnlineStoreProductPageDto
{
    public ProductDto Product { get; init; } = new();
    public OnlineShoppingProductAvailabilityDto Availability { get; init; } = new();
    public OnlineStoreProductDetailsDto Details { get; init; } = new();
    public OnlineStoreCategoryDto? Category { get; init; }
    /// Other products in the same category, current product excluded. A plain catalogue
    /// rule — not a recommendation engine, and never described as one to the shopper.
    public IReadOnlyList<ProductDto> Related { get; init; } = [];
    public IReadOnlyList<OnlineShoppingProductAvailabilityDto> RelatedAvailability { get; init; } = [];
    public IReadOnlyList<OnlineStoreProductDetailsDto> RelatedDetails { get; init; } = [];
}

/// An explicitly requested set of products, in the same shape the catalogue page returns.
///
/// Used by surfaces that already hold product ids and need the current catalogue truth for
/// them — the storefront's device-local favourites list is the first. Ids that no longer
/// resolve (removed, deactivated, not sellable in this branch) are simply absent from the
/// response rather than reported as an error, so a stale list degrades to a shorter one.
public sealed class OnlineStoreProductSetDto
{
    public IReadOnlyList<ProductDto> Products { get; init; } = [];
    public IReadOnlyList<OnlineShoppingProductAvailabilityDto> ProductAvailability { get; init; } = [];
    public IReadOnlyList<OnlineStoreProductDetailsDto> ProductDetails { get; init; } = [];
}

/// Slim row for the search suggestion panel — enough to recognise a product, nothing more.
public sealed class OnlineStoreSuggestionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public decimal Price { get; init; }
    public decimal BasePrice { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageKey { get; init; }
    public string? Brand { get; init; }
}

public sealed class OnlineStoreSuggestionsDto
{
    public IReadOnlyList<OnlineStoreSuggestionDto> Products { get; init; } = [];
    public IReadOnlyList<OnlineStoreCategoryDto> Categories { get; init; } = [];
}

public sealed class OnlineStoreOrderLookupRequest
{
    [Required, StringLength(64, MinimumLength = 1)]
    public string OrderNumber { get; init; } = string.Empty;

    /// Phone number or email address captured on the order. Both are accepted so the
    /// guest can use whichever contact detail they supplied at checkout.
    [Required, StringLength(200, MinimumLength = 3)]
    public string Contact { get; init; } = string.Empty;
}

public sealed class OnlineStoreOrderLookupResultDto
{
    /// The order's existing access token, returned only after the caller proved
    /// ownership. It lets the verified device store a tracking link without
    /// keeping any customer detail locally.
    public string TrackingToken { get; init; } = string.Empty;
    public OnlineShoppingOrderStatusDto Order { get; init; } = new();
}
