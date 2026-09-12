using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    /// <summary>Full management view of a discount group (Admin/Manager settings screen).</summary>
    public class DiscountGroupDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public DiscountValueType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public bool IsActive { get; set; }
        public string? Description { get; set; }
        public string? VerificationNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Lightweight option surfaced to the cashier/order screen — only what is needed
    /// to render the selector and (for display) the applied value. The backend still
    /// re-resolves the authoritative type + value at order creation.</summary>
    public class DiscountGroupOptionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public DiscountValueType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public string? VerificationNote { get; set; }
    }

    /// <summary>Create/update input. TenantId, audit fields and Id are owned by the service.</summary>
    public class DiscountGroupCreateDto
    {
        [Required, StringLength(120, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [StringLength(120)]
        public string? NameAr { get; set; }

        public DiscountValueType DiscountType { get; set; } = DiscountValueType.Percentage;

        // Percentage (0–100) or flat currency amount, interpreted per DiscountType.
        [Range(0, 1000000)]
        public decimal DiscountValue { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(300)]
        public string? Description { get; set; }

        [StringLength(120)]
        public string? VerificationNote { get; set; }
    }

    public class DiscountGroupUpdateDto : DiscountGroupCreateDto { }
}
