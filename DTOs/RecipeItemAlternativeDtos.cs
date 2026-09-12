using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    public class RecipeItemAlternativeDto
    {
        public Guid Id { get; set; }
        public Guid RecipeItemId { get; set; }
        public Guid RawMaterialId { get; set; }
        public string RawMaterialName { get; set; } = string.Empty;
        public string? RawMaterialNameAr { get; set; }
        /// <summary>Display override; falls back to raw-material name on the client.</summary>
        public string? Name { get; set; }
        public string? NameAr { get; set; }
        public decimal Amount { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal PriceAdjustment { get; set; }
        public int AlternativePricingType { get; set; } = 1;
        public decimal? CustomerAdditionalPrice { get; set; }
        public decimal? FixedOverridePrice { get; set; }
        public decimal CustomerPriceImpact { get; set; }
        public decimal CostImpact { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    public class RecipeItemAlternativeCreateDto
    {
        [Required]
        public Guid RecipeItemId { get; set; }

        [Required]
        public Guid RawMaterialId { get; set; }

        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(200)]
        public string? NameAr { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }

        [Range(-999999.99, 999999.99)]
        public decimal PriceAdjustment { get; set; } = 0m;

        [Range(0, 2)]
        public int AlternativePricingType { get; set; } = 1;

        [Range(-999999.99, 999999.99)]
        public decimal? CustomerAdditionalPrice { get; set; }

        [Range(0, 999999.99)]
        public decimal? FixedOverridePrice { get; set; }

        public bool IsDefault { get; set; } = false;
        public int SortOrder { get; set; } = 0;
    }

    public class RecipeItemAlternativeUpdateDto
    {
        [Required]
        public Guid RawMaterialId { get; set; }

        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(200)]
        public string? NameAr { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }

        [Range(-999999.99, 999999.99)]
        public decimal PriceAdjustment { get; set; } = 0m;

        [Range(0, 2)]
        public int AlternativePricingType { get; set; } = 1;

        [Range(-999999.99, 999999.99)]
        public decimal? CustomerAdditionalPrice { get; set; }

        [Range(0, 999999.99)]
        public decimal? FixedOverridePrice { get; set; }

        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>Wire contract for a single alternative chosen at order time.</summary>
    public class SelectedRecipeAlternativeDto
    {
        [Required]
        public Guid RecipeItemId { get; set; }

        [Required]
        public Guid AlternativeId { get; set; }
    }
}
