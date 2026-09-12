using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    public enum DeliveryPartnerStatus
    {
        Active = 0,
        Disabled = 1
    }

    public enum DeliveryPartnerPricingRuleType
    {
        None = 0,
        PercentageIncrease = 1,
        PercentageDiscount = 2,
        FixedIncrease = 3,
        FixedDiscount = 4,
        CustomPrice = 5
    }

    public enum DeliveryPartnerDeliveryCostRuleType
    {
        ManualEntry = 0,
        FixedAmount = 1,
        PercentageOfDeliveryFee = 2
    }

    public class DeliveryPartner : BaseEntity
    {
        [Required]
        [MaxLength(140)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(140)]
        public string? NameAr { get; set; }

        [Required]
        [MaxLength(60)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(1000)]
        public string? DescriptionAr { get; set; }

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        public DeliveryPartnerStatus Status { get; set; } = DeliveryPartnerStatus.Active;

        public DeliveryPartnerPricingRuleType DefaultPricingRuleType { get; set; } = DeliveryPartnerPricingRuleType.None;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultPricingRuleValue { get; set; }

        public DeliveryPartnerDeliveryCostRuleType DeliveryCostRuleType { get; set; } = DeliveryPartnerDeliveryCostRuleType.ManualEntry;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultDeliveryCostValue { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal CostSharingCommissionPercentage { get; set; }

        public CostSharingMode CostSharingMode { get; set; } = CostSharingMode.RestaurantBearsAll;

        public CostSharingScope CostSharingScope { get; set; } = CostSharingScope.PerPartnerOrCard;

        [Column(TypeName = "decimal(5,2)")]
        public decimal CostSharingRestaurantPercentage { get; set; } = 100m;

        [Column(TypeName = "decimal(5,2)")]
        public decimal CostSharingCounterpartyPercentage { get; set; }

        public bool IntegrationEnabled { get; set; }

        [Column(TypeName = "text")]
        public string? IntegrationSettingsJson { get; set; }

        [Column(TypeName = "text")]
        public string? CredentialsJson { get; set; }

        public int SortOrder { get; set; }

        public ICollection<DeliveryPartnerProduct> ProductMappings { get; set; } = new List<DeliveryPartnerProduct>();
    }

    public class DeliveryPartnerProduct : BaseEntity
    {
        public Guid DeliveryPartnerId { get; set; }
        public DeliveryPartner DeliveryPartner { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        [MaxLength(120)]
        public string? PartnerProductId { get; set; }

        public bool IsEnabled { get; set; }

        public bool IsAvailable { get; set; } = true;

        public DeliveryPartnerPricingRuleType? PricingRuleType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PricingRuleValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CustomPrice { get; set; }

        public DateOnly? AvailableStartDate { get; set; }
        public DateOnly? AvailableEndDate { get; set; }
        public TimeOnly? AvailableFrom { get; set; }
        public TimeOnly? AvailableTo { get; set; }
    }

    public class DeliveryPartnerActivityLog : BaseEntity
    {
        public Guid? DeliveryPartnerId { get; set; }
        public DeliveryPartner? DeliveryPartner { get; set; }

        public Guid? ProductId { get; set; }
        public Product? Product { get; set; }

        [Required]
        [MaxLength(80)]
        public string ActionType { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? DetailsJson { get; set; }

        public Guid? PerformedById { get; set; }

        [MaxLength(160)]
        public string? PerformedByName { get; set; }
    }
}
