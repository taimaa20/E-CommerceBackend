using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs;

/// Admin view of a banner: everything the merchant configured, including a schedule that
/// has not started yet or has already finished.
public sealed class StorefrontBannerDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? TitleAr { get; init; }
    public string? Subtitle { get; init; }
    public string? SubtitleAr { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public string? ImageKey { get; init; }
    public string? MobileImageUrl { get; init; }
    public string? MobileImageKey { get; init; }
    public string? CtaLabel { get; init; }
    public string? CtaLabelAr { get; init; }
    public StorefrontBannerLinkType LinkType { get; init; }
    public string? LinkValue { get; init; }
    public StorefrontBannerPlacement Placement { get; init; }
    public bool IsActive { get; init; }
    public DateTime? StartsAtUtc { get; init; }
    public DateTime? EndsAtUtc { get; init; }
    public int SortOrder { get; init; }
    /// Whether this banner is being shown to shoppers right now.
    public bool IsLiveNow { get; init; }
}

/// Public projection. Only banners that are live right now are ever mapped into this,
/// so the storefront has no schedule of its own to evaluate.
public sealed class StorefrontBannerPublicDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? TitleAr { get; init; }
    public string? Subtitle { get; init; }
    public string? SubtitleAr { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public string? ImageKey { get; init; }
    public string? MobileImageUrl { get; init; }
    public string? MobileImageKey { get; init; }
    public string? CtaLabel { get; init; }
    public string? CtaLabelAr { get; init; }
    public StorefrontBannerLinkType LinkType { get; init; }
    public string? LinkValue { get; init; }
}

public sealed class StorefrontBannerUpsertDto
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;

    [StringLength(120)]
    public string? TitleAr { get; init; }

    [StringLength(240)]
    public string? Subtitle { get; init; }

    [StringLength(240)]
    public string? SubtitleAr { get; init; }

    [Required, StringLength(2000, MinimumLength = 1)]
    public string ImageUrl { get; init; } = string.Empty;

    [StringLength(64)]
    public string? ImageKey { get; init; }

    [StringLength(2000)]
    public string? MobileImageUrl { get; init; }

    [StringLength(64)]
    public string? MobileImageKey { get; init; }

    [StringLength(40)]
    public string? CtaLabel { get; init; }

    [StringLength(40)]
    public string? CtaLabelAr { get; init; }

    public StorefrontBannerLinkType LinkType { get; init; } = StorefrontBannerLinkType.None;

    [StringLength(500)]
    public string? LinkValue { get; init; }

    public StorefrontBannerPlacement Placement { get; init; } = StorefrontBannerPlacement.HomeHero;

    public bool IsActive { get; init; } = true;

    public DateTime? StartsAtUtc { get; init; }
    public DateTime? EndsAtUtc { get; init; }
    public int SortOrder { get; init; }
}
