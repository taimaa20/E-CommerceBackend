using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    /// <summary>
    /// Tenant Menu Configuration - Stores per-client menu customization settings
    /// Inherits from BaseEntity for multi-tenancy support
    /// </summary>
    public class TenantMenuSettings : BaseEntity
    {
        [Required]
        [ForeignKey(nameof(Tenant))]
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        /// Menu Display Settings
        [MaxLength(500)]
        public string? MenuTitle { get; set; } = "Menu";
        
        [MaxLength(500)]
        public string? MenuTitleAr { get; set; } = "القائمة";
        
        [Column(TypeName = "text")]
        public string? MenuDescription { get; set; }
        
        [Column(TypeName = "text")]
        public string? MenuDescriptionAr { get; set; }

        /// Banner & Hero Settings
        [MaxLength(500)]
        public string? HeroBannerUrl { get; set; }
        
        [MaxLength(200)]
        public string? HeroBannerTitle { get; set; }
        
        [MaxLength(200)]
        public string? HeroBannerTitleAr { get; set; }

        /// Product Image Fallback
        [MaxLength(500)]
        public string? DefaultProductImageUrl { get; set; }

        /// QR Code Settings
        public bool EnableQrAccess { get; set; } = true;
        
        [MaxLength(500)]
        public string? QrCodeDisplayText { get; set; } = "Scan for Menu";
        
        [MaxLength(500)]
        public string? QrCodeDisplayTextAr { get; set; } = "امسح الرمز للقائمة";

        /// Availability & Visibility
        public bool ShowPrices { get; set; } = true;
        public bool ShowAllergenInfo { get; set; } = true;
        public bool ShowIngredientsBreakdown { get; set; } = false;
        public bool RequireLanguageSelection { get; set; } = true;
        
        /// Localization
        public bool AllowArabic { get; set; } = true;
        public bool AllowEnglish { get; set; } = true;

        /// Search & Filter Settings
        public bool EnableCategoryFilter { get; set; } = true;
        public bool EnableSearch { get; set; } = true;
        public bool EnableSort { get; set; } = false;

        /// Theme & Appearance
        [MaxLength(20)]
        public string? ThemePrimaryColor { get; set; } // Override tenant color for menu

        /// Menu Metadata
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// QR Code Access Tracking - Tracks QR code usage and rate limiting
    /// </summary>
    public class QrCodeAccess : BaseEntity
    {
        [Required]
        [ForeignKey(nameof(Tenant))]
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        /// QR Code Identifier
        [Required]
        [MaxLength(100)]
        public string QrCodeToken { get; set; } // Unique identifier for QR code

        /// Access Tracking
        public int AccessCount { get; set; } = 0;
        
        [MaxLength(50)]
        public string? SourceType { get; set; } // "admin", "waiter", "cashier", "customer"

        /// Timestamp Tracking
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; } // Optional expiration

        /// Status
        public bool IsActive { get; set; } = true;
        
        [MaxLength(255)]
        public string? Description { get; set; } // e.g., "Main Counter QR", "Table 5 QR"
    }

    /// <summary>
    /// Rate Limit Cache - Stores temporary rate limit data for public endpoints
    /// Can be cleared periodically (doesn't need persistence beyond a session)
    /// </summary>
    public class RateLimitEntry
    {
        [Key]
        public string ClientIdentifier { get; set; } // IP address or device ID
        
        [Required]
        public string Endpoint { get; set; } // e.g., "/api/menu/public", "/api/products/public"

        public int RequestCount { get; set; } = 1;
        
        public DateTime WindowStartTime { get; set; } = DateTime.UtcNow;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Menu Access Log - Optional logging for security auditing
    /// Stores detailed access information for compliance/analytics
    /// </summary>
    public class MenuAccessLog : BaseEntity
    {
        [Required]
        public Guid TenantId { get; set; }

        [MaxLength(50)]
        public string? ClientIpAddress { get; set; }

        [MaxLength(100)]
        public string? UserAgent { get; set; }

        [MaxLength(50)]
        public string? AccessType { get; set; } // "menu", "product", "category", "qr_scan"

        [MaxLength(20)]
        public string? Language { get; set; } // "en", "ar", etc.

        public DateTime AccessedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(255)]
        public string? ReferrerUrl { get; set; }
        
        public bool Success { get; set; } = true;
    }
}
