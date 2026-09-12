using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs;

/// Back-office view of a brand, including how many catalogue products carry its name.
public sealed class ProductBrandDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public string? LogoUrl { get; init; }
    public string? LogoKey { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    /// Sellable products whose brand text resolves to this row. Zero means nothing uses it yet.
    public int ProductCount { get; init; }
}

/// A brand name found in the catalogue that has no master row yet, offered so a merchant can
/// create it (and give it a logo) without retyping what the catalogue already says.
public sealed class UnregisteredProductBrandDto
{
    public string Name { get; init; } = string.Empty;
    public int ProductCount { get; init; }
}

public sealed class ProductBrandUpsertDto
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [StringLength(120)]
    public string? NameAr { get; init; }

    [StringLength(2000)]
    public string? LogoUrl { get; init; }

    [StringLength(64)]
    public string? LogoKey { get; init; }

    public bool IsActive { get; init; } = true;

    public int SortOrder { get; init; }
}
