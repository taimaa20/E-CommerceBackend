using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public class DeliveryPartnerDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DescriptionAr { get; set; }
        public string? LogoUrl { get; set; }
        public DeliveryPartnerStatus Status { get; set; }
        public DeliveryPartnerPricingRuleType DefaultPricingRuleType { get; set; }
        public decimal DefaultPricingRuleValue { get; set; }
        public DeliveryPartnerDeliveryCostRuleType DeliveryCostRuleType { get; set; }
        public decimal DefaultDeliveryCostValue { get; set; }
        public CostSharingMode CostSharingMode { get; set; }
        public CostSharingScope CostSharingScope { get; set; }
        public decimal CostSharingCommissionPercentage { get; set; }
        public decimal CostSharingRestaurantPercentage { get; set; }
        public decimal CostSharingCounterpartyPercentage { get; set; }
        public bool IntegrationEnabled { get; set; }
        public string? IntegrationSettingsJson { get; set; }
        public bool HasCredentials { get; set; }
        public int SortOrder { get; set; }
        public int ProductMappingCount { get; set; }
        public int EnabledProductCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class DeliveryPartnerUpsertDto
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

        [EnumDataType(typeof(DeliveryPartnerStatus))]
        public DeliveryPartnerStatus Status { get; set; } = DeliveryPartnerStatus.Active;

        [EnumDataType(typeof(DeliveryPartnerPricingRuleType))]
        public DeliveryPartnerPricingRuleType DefaultPricingRuleType { get; set; } = DeliveryPartnerPricingRuleType.None;

        [Range(0, 9999999999999999.99)]
        public decimal DefaultPricingRuleValue { get; set; }

        [EnumDataType(typeof(DeliveryPartnerDeliveryCostRuleType))]
        public DeliveryPartnerDeliveryCostRuleType DeliveryCostRuleType { get; set; } = DeliveryPartnerDeliveryCostRuleType.ManualEntry;

        [Range(0, 9999999999999999.99)]
        public decimal DefaultDeliveryCostValue { get; set; }

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

        public bool IntegrationEnabled { get; set; }

        public string? IntegrationSettingsJson { get; set; }

        public string? CredentialsJson { get; set; }

        [Range(0, int.MaxValue)]
        public int SortOrder { get; set; }
    }

    public class DeliveryPartnerPagedResultDto
    {
        public IReadOnlyList<DeliveryPartnerDto> Items { get; set; } = new List<DeliveryPartnerDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    public class DeliveryPartnerProductMappingDto
    {
        public Guid? Id { get; set; }
        public Guid PartnerId { get; set; }
        public string PartnerName { get; set; } = string.Empty;
        public string? PartnerNameAr { get; set; }
        public string PartnerCode { get; set; } = string.Empty;
        public DeliveryPartnerStatus PartnerStatus { get; set; }
        public DeliveryPartnerPricingRuleType PartnerDefaultPricingRuleType { get; set; }
        public decimal PartnerDefaultPricingRuleValue { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductNameAr { get; set; }
        public Guid? ProductCategoryId { get; set; }
        public string? ProductCategoryName { get; set; }
        public string? ProductCategoryNameAr { get; set; }
        public decimal ProductBasePrice { get; set; }
        public bool ProductIsActive { get; set; }
        public string? PartnerProductId { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsAvailable { get; set; }
        public DeliveryPartnerPricingRuleType? PricingRuleType { get; set; }
        public decimal? PricingRuleValue { get; set; }
        public decimal? CustomPrice { get; set; }
        public decimal EffectivePrice { get; set; }
        public DeliveryPartnerPricingRuleType EffectivePricingRuleType { get; set; }
        public decimal EffectivePricingRuleValue { get; set; }
        public string PriceSource { get; set; } = "PartnerDefault";
        public DateOnly? AvailableStartDate { get; set; }
        public DateOnly? AvailableEndDate { get; set; }
        public TimeOnly? AvailableFrom { get; set; }
        public TimeOnly? AvailableTo { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class DeliveryPartnerProductMappingUpsertDto
    {
        [MaxLength(120)]
        public string? PartnerProductId { get; set; }

        public bool IsEnabled { get; set; }

        public bool IsAvailable { get; set; } = true;

        [EnumDataType(typeof(DeliveryPartnerPricingRuleType))]
        public DeliveryPartnerPricingRuleType? PricingRuleType { get; set; }

        [Range(0, 9999999999999999.99)]
        public decimal? PricingRuleValue { get; set; }

        [Range(0, 9999999999999999.99)]
        public decimal? CustomPrice { get; set; }

        public DateOnly? AvailableStartDate { get; set; }
        public DateOnly? AvailableEndDate { get; set; }
        public TimeOnly? AvailableFrom { get; set; }
        public TimeOnly? AvailableTo { get; set; }
    }

    public class DeliveryPartnerProductPagedResultDto
    {
        public IReadOnlyList<DeliveryPartnerProductMappingDto> Items { get; set; } = new List<DeliveryPartnerProductMappingDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
