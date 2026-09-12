using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    /// <summary>
    /// PUBLIC MENU DTOs - Sanitized for anonymous/public access
    /// Only exposes customer-facing information (NO costs, markup, internal fields)
    /// </summary>

    // ─── CATEGORY ──────────────────────────────────────────
    public class PublicCategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public string DisplayName { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public List<PublicSubcategoryDto> Subcategories { get; set; } = new();
    }

    public class PublicSubcategoryDto
    {
        public Guid Id { get; set; }
        public Guid CategoryId { get; set; }
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public string DisplayName { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }

    // ─── PRODUCT (PUBLIC) ──────────────────────────────────
    public class PublicProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public string DisplayName { get; set; }
        public string? Description { get; set; }
        public string? DescriptionAr { get; set; }
        public int? Calories { get; set; }
        
        /// Price shown to customer (discounted if available)
        public decimal DisplayPrice { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? OriginalPrice { get; set; } // For showing "was" pricing
        public decimal? DiscountPercentage { get; set; }
        
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryDisplayName { get; set; }
        public Guid? SubcategoryId { get; set; }
        public string? SubcategoryName { get; set; }
        public string? SubcategoryNameAr { get; set; }
        public string? SubcategoryDisplayName { get; set; }
        
        public string? ImageUrl { get; set; }
        /// <summary>Optimized-image key. Optional; when null, consumers use <see cref="ImageUrl"/>.</summary>
        public string? ImageKey { get; set; }
        public int Allergens { get; set; } // Flags for Gluten, Dairy, Nuts, etc.
        public List<PublicModifierGroupDto> ModifierGroups { get; set; } = new();
        public bool IsAvailable { get; set; } = true;
        /// "Coming Soon" marker — consumers must hide price & disable interaction.
        public bool IsSoon { get; set; } = false;
        
        /// Optional: Ingredients list for customers
        public List<IngredientSummaryDto>? Ingredients { get; set; }
    }

    // ─── MODIFIER GROUP (PUBLIC) ────────────────────────────
    public class PublicModifierGroupDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public string DisplayName { get; set; }
        public int SelectionType { get; set; } // 0=Single, 1=Multiple
        public int MinSelection { get; set; }
        public int MaxSelection { get; set; }
        public List<PublicModifierDto> Modifiers { get; set; } = new();
    }

    // ─── MODIFIER (PUBLIC) ──────────────────────────────────
    public class PublicModifierDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public string DisplayName { get; set; }
        public decimal PriceAdjustment { get; set; }
        public bool IsDefault { get; set; }
    }

    // ─── INGREDIENT SUMMARY (PUBLIC) ────────────────────────
    public class IngredientSummaryDto
    {
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public string DisplayName { get; set; }
    }

    // ─── MENU RESPONSE (COMPLETE PUBLIC MENU) ──────────────
    public class PublicMenuDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<PublicCategoryDto> Categories { get; set; } = new();
        public List<PublicProductDto> Products { get; set; } = new();
    }

    // ─── TENANT BRANDING CONFIG (PUBLIC) ────────────────────
    public class TenantBrandingDto
    {
        public Guid TenantId { get; set; }
        public string SiteName { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? BannerUrl { get; set; }
        public string? PrimaryColor { get; set; }
        /// <summary>Configured secondary brand colour (Tenants.AccentColor). Nullable — a
        /// consumer without one simply renders no accent, exactly as before this field.</summary>
        public string? AccentColor { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public bool AllowQrAccess { get; set; } = true;
        public bool RequireLanguageSelection { get; set; } = true;
    }

    // ─── QR CODE METADATA ──────────────────────────────────
    public class QrMenuMetadataDto
    {
        public Guid TenantId { get; set; }
        public string MenuUrl { get; set; }
        public string QrCodeToken { get; set; } // Secure token for QR tracking
        public DateTime GeneratedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int AccessCount { get; set; }
        public bool IsActive { get; set; }
    }

    // ─── MENU RATE LIMIT CHECK ────────────────────────────
    public class MenuAccessCheckDto
    {
        [Required]
        public string ClientIdentifier { get; set; } // IP or device ID
        
        [Required]
        public string Endpoint { get; set; } // /api/menu/public, /api/menu/categories, etc.
    }

    public class MenuAccessCheckResponseDto
    {
        public bool IsAllowed { get; set; }
        public int RemainingRequests { get; set; }
        public int ResetSeconds { get; set; } // Seconds until limit resets
        public string Message { get; set; }
    }
}
