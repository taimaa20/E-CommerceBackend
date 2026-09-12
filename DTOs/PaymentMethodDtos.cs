using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public class PaymentMethodDto
    {
        public Guid Id { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public bool RequiresReferenceNumber { get; set; }
        public CostSharingMode CostSharingMode { get; set; }
        public CostSharingScope CostSharingScope { get; set; }
        public decimal CostSharingCommissionPercentage { get; set; }
        public decimal CostSharingRestaurantPercentage { get; set; }
        public decimal CostSharingCounterpartyPercentage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PaymentMethodCreateDto
    {
        [Required]
        [MaxLength(120)]
        public string NameEn { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? NameAr { get; set; }

        [Required]
        [MaxLength(60)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? Icon { get; set; }

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDefault { get; set; }

        public bool RequiresReferenceNumber { get; set; }

        [EnumDataType(typeof(CostSharingMode))]
        public CostSharingMode CostSharingMode { get; set; } = CostSharingMode.RestaurantBearsAll;

        [EnumDataType(typeof(CostSharingScope))]
        public CostSharingScope CostSharingScope { get; set; } = CostSharingScope.PerPartnerOrCard;

        [Range(0, 100)]
        public decimal CostSharingCommissionPercentage { get; set; }

        [Range(0, 100)]
        public decimal CostSharingRestaurantPercentage { get; set; } = 100m;

        [Range(0, 100)]
        public decimal CostSharingCounterpartyPercentage { get; set; }
    }

    public class PaymentMethodUpdateDto
    {
        [Required]
        [MaxLength(120)]
        public string NameEn { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? NameAr { get; set; }

        [Required]
        [MaxLength(60)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? Icon { get; set; }

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDefault { get; set; }

        public bool RequiresReferenceNumber { get; set; }

        [EnumDataType(typeof(CostSharingMode))]
        public CostSharingMode CostSharingMode { get; set; } = CostSharingMode.RestaurantBearsAll;

        [EnumDataType(typeof(CostSharingScope))]
        public CostSharingScope CostSharingScope { get; set; } = CostSharingScope.PerPartnerOrCard;

        [Range(0, 100)]
        public decimal CostSharingCommissionPercentage { get; set; }

        [Range(0, 100)]
        public decimal CostSharingRestaurantPercentage { get; set; } = 100m;

        [Range(0, 100)]
        public decimal CostSharingCounterpartyPercentage { get; set; }
    }
}
