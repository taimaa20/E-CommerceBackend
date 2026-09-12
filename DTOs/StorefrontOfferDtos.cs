namespace RestaurantPos.Api.DTOs;

/// <summary>
/// A bundle offer as the public store may present it.
///
/// Every price here is produced by the same allocation the order intake charges, so what
/// the shopper adds to the cart is what the order will cost. Only offers that are live at
/// the moment of the request are ever mapped into this shape — the storefront has no
/// schedule of its own to evaluate and cannot show an expired deal.
/// </summary>
public sealed class OnlineStoreOfferDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public string? Description { get; init; }
    public string? DescriptionAr { get; init; }
    public string? ImageUrl { get; init; }

    /// <summary>What the bundle costs.</summary>
    public decimal Price { get; init; }

    /// <summary>What the same items cost bought separately, at their normal effective price.</summary>
    public decimal OriginalPrice { get; init; }

    public decimal SavingAmount { get; init; }
    public int SavingPercent { get; init; }

    /// <summary>Last day the offer runs, in the restaurant's own calendar.</summary>
    public DateOnly EndsOn { get; init; }

    /// <summary>Daily window in restaurant-local time. Null when the offer runs all day.</summary>
    public string? DailyStartTime { get; init; }
    public string? DailyEndTime { get; init; }

    public IReadOnlyList<OnlineStoreOfferItemDto> Items { get; init; } = [];
}

public sealed class OnlineStoreOfferItemDto
{
    public Guid ProductId { get; init; }

    /// <summary>
    /// The department this product sits in. It lets the storefront say which departments
    /// currently contain something on offer without a second read, and it is the product's
    /// own category — nothing about the offer is inferred from it.
    /// </summary>
    public Guid? CategoryId { get; init; }

    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageKey { get; init; }
    public int Quantity { get; init; }

    /// <summary>The unit price order intake will charge for this line inside the offer.</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>The unit price outside the offer, shown struck through.</summary>
    public decimal ListUnitPrice { get; init; }
}

public sealed class OnlineStoreOffersDto
{
    public IReadOnlyList<OnlineStoreOfferDto> Offers { get; init; } = [];
}
