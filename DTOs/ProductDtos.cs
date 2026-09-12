using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    // --- READ DTOs (For GET) ---
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public string DisplayName { get; set; }
        public string? Description { get; set; }
        public string? DescriptionAr { get; set; }
        public int? Calories { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? CostPrice { get; set; }
        public string PricingMode { get; set; } = ProductPricingModes.Manual;
        public decimal Markup { get; set; }
        public string MarkupType { get; set; } = "multiplier"; // "multiplier", "percentage", or "fixed"
        public decimal ProfitAmount { get; set; }
        public decimal ProfitMargin { get; set; }
        public decimal ProfitMultiplier { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal? DiscountedPrice { get; set; }
        // Promotional price — consumed by public menu / informative website only.
        public decimal? CustomPrice { get; set; }
        public bool UseCustomPrice { get; set; }
        // Convenience field: what the menu/website should render.
        public decimal DisplayPrice { get; set; }
        public Guid? DeliveryPartnerId { get; set; }
        public string? DeliveryPartnerName { get; set; }
        public string? DeliveryPartnerNameAr { get; set; }
        public string? DeliveryPartnerCode { get; set; }
        public decimal? PartnerPrice { get; set; }
        public string? PriceSource { get; set; }
        public bool IsActive { get; set; }
        // UI-only "Coming Soon" flag — consumed by public menu & informative site.
        // POS, orders and pricing ignore this.
        public bool IsSoon { get; set; }
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryNameAr { get; set; }
        public string? CategoryDisplayName { get; set; }
        public Guid? SubcategoryId { get; set; }
        public string? SubcategoryName { get; set; }
        public string? SubcategoryNameAr { get; set; }
        public string? SubcategoryDisplayName { get; set; }
        public int Allergens { get; set; } // AllergenType as int
        public int StationRouting { get; set; } // StationRouting as int
        public string? PrinterIds { get; set; }
        public Guid? KitchenId { get; set; }
        public string? KitchenName { get; set; }
        public string? KitchenNameAr { get; set; }
        /// <summary>When true, the product prints on every active kitchen printer; KitchenPrinterIds is ignored.</summary>
        public bool AllKitchenPrinters { get; set; }
        /// <summary>Explicit multi-printer assignment. Empty when the product follows the legacy single-printer rule.</summary>
        public List<Guid> KitchenPrinterIds { get; set; } = new();
        public string? ImageUrl { get; set; }
        /// <summary>
        /// Optional optimized-image key. When present, frontend renders the WebP
        /// variant set (thumb/medium/full). When null, frontend falls back to
        /// <see cref="ImageUrl"/> so pre-pipeline rows keep rendering unchanged.
        /// </summary>
        public string? ImageKey { get; set; }
        public bool IsAvailableNow { get; set; }
        public DateOnly? AvailableStartDate { get; set; }
        public DateOnly? AvailableEndDate { get; set; }
        public TimeOnly? AvailableFrom { get; set; }
        public TimeOnly? AvailableTo { get; set; }
        public List<ModifierGroupDto> ModifierGroups { get; set; } = new();
        public List<RecipeItemDto> RecipeItems { get; set; } = new();
        /// <summary>Named variants with dedicated price and ingredient list.</summary>
        public List<ProductOptionDto> Options { get; set; } = new();

        /// <summary>
        /// Additional gallery images, ordered. Populated by the ADMIN product endpoints only —
        /// the cashier and public-menu projections leave it empty, exactly like <see cref="Retail"/>.
        /// Empty means the product has a single image (or none), which is the pre-gallery behaviour.
        /// </summary>
        public List<ProductImageDto> Images { get; set; } = new();

        /// <summary>
        /// Retail attributes (SKU, barcode, brand, supplier, retail costing/stock). Null for
        /// ordinary restaurant products — its presence is the only signal a consumer needs.
        /// Populated by the Retail module on the admin product endpoints only; the public
        /// menu and cashier payloads deliberately leave it null.
        /// </summary>
        public Modules.Retail.DTOs.RetailProductDetailDto? Retail { get; set; }
    }

    public class ModifierGroupDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public string DisplayName { get; set; }
        public int SelectionType { get; set; }
        public int DisplayType { get; set; }
        public int MinSelection { get; set; }
        public int MaxSelection { get; set; }
        public bool IsRequired { get; set; }
        public int AvailableForOrderTypes { get; set; }
        public bool PrintOnReceipt { get; set; }
        public bool PrintInKitchen { get; set; }
        public bool IsShared { get; set; }
        public List<ModifierDto> Modifiers { get; set; } = new();
    }

    public class ModifierDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public string DisplayName { get; set; }
        public int PricingType { get; set; }
        public decimal PriceAdjustment { get; set; }
        public bool IsFree { get; set; }
        public int FreeQuantityLimit { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int MaxQuantity { get; set; }
        public Guid? LinkedRawMaterialId { get; set; }
        public Guid? LinkedProductId { get; set; }
        public decimal LinkedMaterialAmount { get; set; }
        public List<ModifierRecipeItemDto> RecipeItems { get; set; } = new();
    }

    public class ModifierRecipeItemDto
    {
        public Guid Id { get; set; }
        public Guid RawMaterialId { get; set; }
        public string RawMaterialName { get; set; }
        public string? RawMaterialNameAr { get; set; }
        public decimal Amount { get; set; }
        public string Unit { get; set; }
    }

    public class RecipeItemDto
    {
        public Guid Id { get; set; }
        public Guid RawMaterialId { get; set; }
        public string RawMaterialName { get; set; }
        public string? RawMaterialNameAr { get; set; }
        public bool ShowInMenu { get; set; }
        public bool IsPostPrice { get; set; }
        public decimal Amount { get; set; }
        public string Unit { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        /// <summary>Ingredient-level alternatives the cashier can swap this row for.
        /// Empty list ⇒ this row has no swap choices and is always used as-is.</summary>
        public List<RecipeItemAlternativeDto> Alternatives { get; set; } = new();
    }

    public class RecipeItemCreateDto
    {
        [Required]
        public Guid RawMaterialId { get; set; }

        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "Recipe amount must be greater than zero.")]
        public decimal Amount { get; set; }

        public List<RecipeItemAlternativeCreateForRecipeDto> Alternatives { get; set; } = new();
    }

    public class RecipeItemAlternativeCreateForRecipeDto
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

        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
    }

    // --- CREATE DTOs (For POST) ---
    public class ProductCreateDto
    {
        [Required(ErrorMessage = "Product name (EN) is required.")]
        [MaxLength(200, ErrorMessage = "Product name cannot exceed 200 characters.")]
        [MinLength(1, ErrorMessage = "Product name cannot be empty.")]
        public string Name { get; set; }

        [MaxLength(200, ErrorMessage = "Product name (AR) cannot exceed 200 characters.")]
        public string? NameAr { get; set; }

        [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        public string? Description { get; set; }

        [MaxLength(1000, ErrorMessage = "Arabic description cannot exceed 1000 characters.")]
        public string? DescriptionAr { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Calories must be 0 or greater.")]
        public int? Calories { get; set; }

        // BasePrice range allows 0 so admins can save a product whose pricing is
        // driven by CustomPrice. Service-layer validation (ValidateProductAsync)
        // enforces "> 0" only when UseCustomPrice = false.
        [Range(0, 999999.99, ErrorMessage = "Base price must be between 0 and 999999.99.")]
        public decimal BasePrice { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Cost price must be between 0 and 999999.99.")]
        public decimal? CostPrice { get; set; }

        [MaxLength(20, ErrorMessage = "Pricing mode cannot exceed 20 characters.")]
        public string PricingMode { get; set; } = ProductPricingModes.Manual;

        // Promotional pricing — shown on /menu and informative website only.
        [Range(0, 999999.99, ErrorMessage = "Custom price must be between 0 and 999999.99.")]
        public decimal? CustomPrice { get; set; }

        public bool UseCustomPrice { get; set; } = false;

        [Range(0, 100, ErrorMessage = "Markup must be between 0 and 100.")]
        public decimal Markup { get; set; } = 1;

        [MaxLength(20, ErrorMessage = "Markup type cannot exceed 20 characters.")]
        public string MarkupType { get; set; } = "multiplier"; // "multiplier", "percentage", or "fixed"

        [Range(0, 100, ErrorMessage = "Discount percentage must be between 0 and 100.")]
        public decimal DiscountPercentage { get; set; } = 0;

        [Range(0, 999999.99, ErrorMessage = "Discounted price must be between 0 and 999999.99.")]
        public decimal? DiscountedPrice { get; set; }

        public Guid? CategoryId { get; set; }
        public Guid? SubcategoryId { get; set; }
        public Guid TenantId { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsSoon { get; set; } = false;
        public int Allergens { get; set; } = 0;

        [Range(0, 2, ErrorMessage = "Station routing must be 0 (Kitchen), 1 (Bar), or 2 (Both).")]
        public int StationRouting { get; set; } = 0;

        public string? PrinterIds { get; set; }

        /// <summary>
        /// Multi-kitchen routing target. Nullable — items without a kitchen fall
        /// back to the tenant's default (lowest-SortOrder active <see cref="Kitchen"/>).
        /// </summary>
        public Guid? KitchenId { get; set; }

        /// <summary>When true, the product prints on every active kitchen printer; KitchenPrinterIds is ignored.</summary>
        public bool AllKitchenPrinters { get; set; } = false;

        /// <summary>Explicit per-printer assignment. Empty list ⇒ fall back to the legacy single-printer rule.</summary>
        public List<Guid> KitchenPrinterIds { get; set; } = new();

        public string? ImageUrl { get; set; }
        /// <summary>Optimized-image key returned by POST /api/uploads/image. Optional.</summary>
        [MaxLength(64)]
        public string? ImageKey { get; set; }
        public DateOnly? AvailableStartDate { get; set; }
        public DateOnly? AvailableEndDate { get; set; }
        public TimeOnly? AvailableFrom { get; set; }
        public TimeOnly? AvailableTo { get; set; }

        // Full Nested Structure for Creation
        public List<ModifierGroupCreateDto> ModifierGroups { get; set; } = new();
        public List<RecipeItemCreateDto> RecipeItems { get; set; } = new();

        /// <summary>
        /// Additional gallery images, in display order.
        ///
        /// NULL means "this caller does not manage the gallery" and the stored gallery is left
        /// untouched — that is what every existing client sends, so none of them can silently
        /// wipe images. A non-null list (including an empty one) REPLACES the gallery.
        /// </summary>
        public List<ProductImageInputDto>? Images { get; set; }

        /// <summary>
        /// Optional retail attributes. Omitted by every existing restaurant caller, in which
        /// case nothing about retail is created or changed.
        /// </summary>
        public Modules.Retail.DTOs.RetailProductDetailUpsertDto? Retail { get; set; }
    }

    /// <summary>One gallery image as the admin screen and storefront read it.</summary>
    public class ProductImageDto
    {
        public Guid Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? ImageKey { get; set; }
        public string? AltText { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>One gallery image as the admin screen submits it.</summary>
    public class ProductImageInputDto
    {
        [Required(ErrorMessage = "Image URL is required.")]
        [MaxLength(2000, ErrorMessage = "Image URL cannot exceed 2000 characters.")]
        public string ImageUrl { get; set; } = string.Empty;

        [MaxLength(64, ErrorMessage = "Image key cannot exceed 64 characters.")]
        public string? ImageKey { get; set; }

        [MaxLength(200, ErrorMessage = "Image description cannot exceed 200 characters.")]
        public string? AltText { get; set; }
    }

    public class ModifierGroupCreateDto
    {
        /// <summary>If set, link to an existing library group instead of creating a new one.</summary>
        public Guid? ExistingGroupId { get; set; }

        [MaxLength(200)]
        public string Name { get; set; } = "";

        [MaxLength(200)]
        public string? NameAr { get; set; }

        [Range(0, 1, ErrorMessage = "Selection type must be 0 (Single) or 1 (Multiple).")]
        public int SelectionType { get; set; } // 0 or 1

        [Range(0, 2, ErrorMessage = "Display type must be 0 (Radio), 1 (Checkbox), or 2 (QuantitySelector).")]
        public int DisplayType { get; set; } = 0;

        [Range(0, 100)]
        public int MinSelection { get; set; }

        [Range(0, 100)]
        public int MaxSelection { get; set; }

        public bool IsRequired { get; set; } = false;

        public int AvailableForOrderTypes { get; set; } = 7; // All by default

        public bool PrintOnReceipt { get; set; } = true;
        public bool PrintInKitchen { get; set; } = true;

        public List<ModifierCreateDto> Modifiers { get; set; } = new();
    }

    public class ModifierCreateDto
    {
        [Required(ErrorMessage = "Modifier name is required.")]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(200)]
        public string? NameAr { get; set; }

        public int PricingType { get; set; } = 0; // Fixed

        [Range(-999999.99, 999999.99)]
        public decimal PriceAdjustment { get; set; }

        public bool IsFree { get; set; } = false;
        public int FreeQuantityLimit { get; set; } = 0;

        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public int MaxQuantity { get; set; } = 0;

        public Guid? LinkedRawMaterialId { get; set; }
        public Guid? LinkedProductId { get; set; }
        public decimal LinkedMaterialAmount { get; set; } = 0;
        public List<RecipeItemCreateDto> RecipeItems { get; set; } = new();
    }

    // --- Library DTOs (for ModifierGroupsController) ---
    public class ModifierGroupLibraryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public int SelectionType { get; set; }
        public int DisplayType { get; set; }
        public int MinSelection { get; set; }
        public int MaxSelection { get; set; }
        public bool IsRequired { get; set; }
        public int AvailableForOrderTypes { get; set; }
        public bool PrintOnReceipt { get; set; }
        public bool PrintInKitchen { get; set; }
        public List<ModifierDto> Modifiers { get; set; } = new();
        public List<ModifierGroupProductRefDto> UsedByProducts { get; set; } = new();
    }

    public class ModifierGroupProductRefDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
    }
}
