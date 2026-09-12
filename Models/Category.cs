
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    public enum CategoryDisplayMode
    {
        Grid = 0,           // Standard Grid with Images
        List = 1,           // List with small thumbnails/icons
        ListNoImage = 2,    // Text-only List (e.g. Wine list)
        CardCarousel = 3    // Horizontal slider
    }

    public class Category : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(100)]
        public string? NameAr { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(2000)]
        public string? ImageUrl { get; set; }

        public int SortOrder { get; set; } = 0;

        public CategoryDisplayMode DisplayMode { get; set; } = CategoryDisplayMode.Grid;

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<Subcategory> Subcategories { get; set; } = new List<Subcategory>();
    }
}
