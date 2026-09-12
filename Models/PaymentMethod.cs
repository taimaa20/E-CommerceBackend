using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Models
{
    public class PaymentMethod : BaseEntity
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

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDefault { get; set; }

        public bool RequiresReferenceNumber { get; set; }

        public CostSharingMode CostSharingMode { get; set; } = CostSharingMode.RestaurantBearsAll;

        public CostSharingScope CostSharingScope { get; set; } = CostSharingScope.PerPartnerOrCard;

        public decimal CostSharingCommissionPercentage { get; set; }

        public decimal CostSharingRestaurantPercentage { get; set; } = 100m;

        public decimal CostSharingCounterpartyPercentage { get; set; }

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
