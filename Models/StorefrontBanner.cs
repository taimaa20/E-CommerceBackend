using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Models
{
    /// <summary>
    /// Where a banner may send the shopper. Deliberately a closed set of internal commerce
    /// destinations plus one validated external option — the storefront never renders a raw
    /// href it was handed, so a stored value can never become a script or data URL.
    /// Append only: existing numbers must keep their meaning.
    /// </summary>
    public enum StorefrontBannerLinkType
    {
        None = 0,
        Category = 1,
        Product = 2,
        Offers = 3,
        Search = 4,
        External = 5
    }

    /// <summary>Which storefront slot the banner belongs to. Append only.</summary>
    public enum StorefrontBannerPlacement
    {
        HomeHero = 0
    }

    /// <summary>
    /// A scheduled promotional banner on the public storefront.
    ///
    /// This is the merchant's own campaign surface, separate from
    /// <see cref="SystemSettings.CoverImageUrl"/>, which stays the store's permanent cover
    /// photo and remains the hero whenever no banner is live. Nothing here prices, reserves
    /// or sells anything — a banner only points at a destination that already exists.
    /// </summary>
    public class StorefrontBanner : BaseEntity
    {
        [Required]
        [MaxLength(120)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? TitleAr { get; set; }

        [MaxLength(240)]
        public string? Subtitle { get; set; }

        [MaxLength(240)]
        public string? SubtitleAr { get; set; }

        /// <summary>Public URL of the desktop/wide image, as returned by POST /api/uploads/image.</summary>
        [Required]
        [MaxLength(2000)]
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>Optimized-variant key from the same upload. Null falls back to <see cref="ImageUrl"/>.</summary>
        [MaxLength(64)]
        public string? ImageKey { get; set; }

        /// <summary>Optional portrait crop for phones. Null means the wide image is used at every width.</summary>
        [MaxLength(2000)]
        public string? MobileImageUrl { get; set; }

        [MaxLength(64)]
        public string? MobileImageKey { get; set; }

        [MaxLength(40)]
        public string? CtaLabel { get; set; }

        [MaxLength(40)]
        public string? CtaLabelAr { get; set; }

        public StorefrontBannerLinkType LinkType { get; set; } = StorefrontBannerLinkType.None;

        /// <summary>Category/product id, search term or absolute https URL, per <see cref="LinkType"/>.</summary>
        [MaxLength(500)]
        public string? LinkValue { get; set; }

        public StorefrontBannerPlacement Placement { get; set; } = StorefrontBannerPlacement.HomeHero;

        public bool IsActive { get; set; } = true;

        /// <summary>UTC. Null means "already started" / "never ends".</summary>
        public DateTime? StartsAtUtc { get; set; }
        public DateTime? EndsAtUtc { get; set; }

        /// <summary>Lower shows first within a placement.</summary>
        public int SortOrder { get; set; }
    }
}
