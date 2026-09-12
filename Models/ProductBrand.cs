using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Models
{
    /// <summary>
    /// A brand the store carries, with the artwork that represents it.
    ///
    /// The catalogue already names a product's brand as text on
    /// <see cref="Modules.Retail.Domain.RetailProductDetail.Brand"/>; this is the master row
    /// that name resolves to, matched on <see cref="NormalizedName"/>. Nothing is duplicated
    /// onto the product, so the relationship stays Product → Brand → logo and a merchant can
    /// give a brand its logo at any time without re-tagging a single product.
    ///
    /// Logo artwork uses the shared upload pipeline (POST /api/uploads/image): the public URL
    /// plus the optional variant key, exactly as products and banners store theirs.
    /// </summary>
    public class ProductBrand : BaseEntity
    {
        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? NameAr { get; set; }

        /// <summary>
        /// Upper-cased, whitespace-collapsed <see cref="Name"/>. The join key against the
        /// catalogue's free-text brand, and the tenant-unique key, so "L'Oréal " and "L'ORÉAL"
        /// cannot become two brands with two logos.
        /// </summary>
        [Required]
        [MaxLength(120)]
        public string NormalizedName { get; set; } = string.Empty;

        /// <summary>Public URL of the logo, as returned by the shared image upload.</summary>
        [MaxLength(2000)]
        public string? LogoUrl { get; set; }

        /// <summary>Optimized-variant key from the same upload. Null falls back to <see cref="LogoUrl"/>.</summary>
        [MaxLength(64)]
        public string? LogoKey { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Lower shows first wherever brands are listed.</summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// The one rule that turns any brand text — typed by a merchant, imported from the
        /// workbook, stored on a product — into the key brands are matched on. Applied in
        /// memory rather than in SQL so the catalogue's free text and this table can never
        /// disagree about what counts as the same brand.
        /// </summary>
        public static string Normalize(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var collapsed = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return collapsed.ToUpperInvariant();
        }
    }
}
