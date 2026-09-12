using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    public class ProductOptionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public decimal Price { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public List<ProductOptionRecipeItemDto> RecipeItems { get; set; } = new();
    }

    public class ProductOptionRecipeItemDto
    {
        public Guid Id { get; set; }
        public Guid RawMaterialId { get; set; }
        public string RawMaterialName { get; set; } = string.Empty;
        public string? RawMaterialNameAr { get; set; }
        public bool ShowInMenu { get; set; }
        public bool IsPostPrice { get; set; }
        public decimal Amount { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class ProductOptionCreateDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? NameAr { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        public bool IsDefault { get; set; } = false;
        public int SortOrder { get; set; } = 0;
    }

    public class ProductOptionUpdateDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? NameAr { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    public class ProductOptionRecipeItemCreateDto
    {
        [Required]
        public Guid ProductOptionId { get; set; }

        [Required]
        public Guid RawMaterialId { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }
    }
}
