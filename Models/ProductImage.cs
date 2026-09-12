using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Models
{
    /// <summary>
    /// One ADDITIONAL gallery image of a <see cref="Product"/>, held in a child table so the
    /// shared Product entity is not widened with Image2Url/Image3Url columns.
    ///
    /// The primary image stays on <see cref="Product.ImageUrl"/> / <see cref="Product.ImageKey"/>.
    /// That is deliberate: POS grids, receipts, the QR menu and every existing consumer already
    /// read those two fields, and none of them has to learn about this table. Promoting a gallery
    /// image to primary swaps the two rows rather than moving the concept.
    ///
    /// A product with no rows here is an ordinary single-image product and behaves exactly as
    /// it did before this table existed.
    /// </summary>
    public class ProductImage : BaseEntity
    {
        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        /// <summary>Public URL of the original upload, as returned by POST /api/uploads/image.</summary>
        [Required]
        [MaxLength(2000)]
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Optimized-variant key from the same upload response. Nullable: when the variant
        /// pipeline is unavailable the frontend falls back to <see cref="ImageUrl"/>, exactly
        /// as it already does for <see cref="Product.ImageKey"/>.
        /// </summary>
        [MaxLength(64)]
        public string? ImageKey { get; set; }

        /// <summary>Optional accessible description. Falls back to the product name when null.</summary>
        [MaxLength(200)]
        public string? AltText { get; set; }

        /// <summary>Gallery order after the primary image. Lower first.</summary>
        public int SortOrder { get; set; }
    }
}
