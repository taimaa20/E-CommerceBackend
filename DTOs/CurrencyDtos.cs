using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs;

/// One currency the business may select. <see cref="IsActive"/> false means "not available for
/// new selection" — it never means the code stopped being valid on records that already name it.
public sealed class CurrencyDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    /// Resolved against the request's language, exactly like every other bilingual list.
    public string DisplayName { get; init; } = string.Empty;
    public string? Symbol { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    /// True when this is the currency the business currently operates in. Such a row cannot be
    /// deactivated, because that would leave the store's own currency unselectable.
    public bool IsStoreCurrency { get; init; }
}

/// Creating a currency fixes its code permanently — see <see cref="CurrencyUpdateDto"/>.
public sealed class CurrencyCreateDto
{
    [Required]
    [StringLength(Currency.CodeLength, MinimumLength = Currency.CodeLength)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(60, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [StringLength(60)]
    public string? NameAr { get; init; }

    [StringLength(10)]
    public string? Symbol { get; init; }

    public bool IsActive { get; init; } = true;

    public int SortOrder { get; init; }
}

/// Deliberately carries no Code. Once transactional data names a code, renaming it would
/// silently re-interpret every record that already carries the old spelling.
public sealed class CurrencyUpdateDto
{
    [Required, StringLength(60, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [StringLength(60)]
    public string? NameAr { get; init; }

    [StringLength(10)]
    public string? Symbol { get; init; }

    public bool IsActive { get; init; } = true;

    public int SortOrder { get; init; }
}
