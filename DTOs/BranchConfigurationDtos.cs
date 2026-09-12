using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public class BranchConfigurationOptionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Code { get; set; }
        public string? SecondaryLabel { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>Offers keep their legacy int PK, so their option list uses an int id.</summary>
    public class BranchConfigurationOfferOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class BranchConfigurationOptionsDto
    {
        public IReadOnlyList<BranchConfigurationOptionDto> Branches { get; set; } = Array.Empty<BranchConfigurationOptionDto>();
        public IReadOnlyList<BranchConfigurationOptionDto> Products { get; set; } = Array.Empty<BranchConfigurationOptionDto>();
        public IReadOnlyList<BranchConfigurationOptionDto> Categories { get; set; } = Array.Empty<BranchConfigurationOptionDto>();
        public IReadOnlyList<BranchConfigurationOptionDto> Subcategories { get; set; } = Array.Empty<BranchConfigurationOptionDto>();
        public IReadOnlyList<BranchConfigurationOptionDto> Modifiers { get; set; } = Array.Empty<BranchConfigurationOptionDto>();
        public IReadOnlyList<BranchConfigurationOptionDto> ModifierGroups { get; set; } = Array.Empty<BranchConfigurationOptionDto>();
        public IReadOnlyList<BranchConfigurationOptionDto> ProductOptions { get; set; } = Array.Empty<BranchConfigurationOptionDto>();
        public IReadOnlyList<BranchConfigurationOptionDto> PaymentMethods { get; set; } = Array.Empty<BranchConfigurationOptionDto>();
        public IReadOnlyList<BranchConfigurationOptionDto> DeliveryPartners { get; set; } = Array.Empty<BranchConfigurationOptionDto>();
        public IReadOnlyList<BranchConfigurationOptionDto> Printers { get; set; } = Array.Empty<BranchConfigurationOptionDto>();
        public IReadOnlyList<BranchConfigurationOfferOptionDto> Offers { get; set; } = Array.Empty<BranchConfigurationOfferOptionDto>();
    }

    public class BranchConfigurationPagedResultDto<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    public abstract class BranchConfigurationDtoBase
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string? BranchNameAr { get; set; }
        public string BranchCode { get; set; } = string.Empty;
        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class BranchProductConfigurationDto : BranchConfigurationDtoBase
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductNameAr { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryNameAr { get; set; }
        public bool ProductIsActive { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsVisible { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class BranchProductConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        public bool IsAvailable { get; set; } = true;
        public bool IsVisible { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }
    }

    public class BranchCategoryConfigurationDto : BranchConfigurationDtoBase
    {
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryNameAr { get; set; }
        public bool CategoryIsActive { get; set; }
        public bool IsVisible { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class BranchSubcategoryConfigurationDto : BranchConfigurationDtoBase
    {
        public Guid SubcategoryId { get; set; }
        public string SubcategoryName { get; set; } = string.Empty;
        public string? SubcategoryNameAr { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryNameAr { get; set; }
        public bool SubcategoryIsActive { get; set; }
        public bool IsVisible { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class BranchSubcategoryConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid SubcategoryId { get; set; }

        public bool IsVisible { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }
    }

    public class BranchCategoryConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        public bool IsVisible { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }
    }

    public class BranchModifierConfigurationDto : BranchConfigurationDtoBase
    {
        public Guid ModifierId { get; set; }
        public string ModifierName { get; set; } = string.Empty;
        public string? ModifierNameAr { get; set; }
        public string ModifierGroupName { get; set; } = string.Empty;
        public string? ModifierGroupNameAr { get; set; }
        public bool ModifierIsActive { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class BranchProductOptionConfigurationDto : BranchConfigurationDtoBase
    {
        public Guid ProductOptionId { get; set; }
        public string ProductOptionName { get; set; } = string.Empty;
        public string? ProductOptionNameAr { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductNameAr { get; set; }
        public bool ProductOptionIsActive { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsVisible { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class BranchProductOptionConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid ProductOptionId { get; set; }

        public bool IsAvailable { get; set; } = true;
        public bool IsVisible { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }
    }

    public class BranchModifierConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid ModifierId { get; set; }

        public bool IsAvailable { get; set; } = true;
    }

    public class BranchModifierGroupConfigurationDto : BranchConfigurationDtoBase
    {
        public Guid ModifierGroupId { get; set; }
        public string ModifierGroupName { get; set; } = string.Empty;
        public string? ModifierGroupNameAr { get; set; }
        public int SelectionType { get; set; }
        public bool IsRequired { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsVisible { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class BranchModifierGroupConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid ModifierGroupId { get; set; }

        public bool IsAvailable { get; set; } = true;
        public bool IsVisible { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }
    }

    public class BranchOfferConfigurationDto : BranchConfigurationDtoBase
    {
        public int OfferId { get; set; }
        public string OfferName { get; set; } = string.Empty;
        public string? OfferNameAr { get; set; }
        public bool OfferIsActive { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class BranchOfferConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int OfferId { get; set; }

        public bool IsEnabled { get; set; } = true;
    }

    public class BranchPaymentMethodConfigurationDto : BranchConfigurationDtoBase
    {
        public Guid PaymentMethodId { get; set; }
        public string PaymentMethodName { get; set; } = string.Empty;
        public string? PaymentMethodNameAr { get; set; }
        public string PaymentMethodCode { get; set; } = string.Empty;
        public bool PaymentMethodIsActive { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class BranchPaymentMethodConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid PaymentMethodId { get; set; }

        public bool IsEnabled { get; set; } = true;
    }

    public class BranchDeliveryPartnerConfigurationDto : BranchConfigurationDtoBase
    {
        public Guid DeliveryPartnerId { get; set; }
        public string DeliveryPartnerName { get; set; } = string.Empty;
        public string? DeliveryPartnerNameAr { get; set; }
        public string DeliveryPartnerCode { get; set; } = string.Empty;
        public bool DeliveryPartnerIsActive { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class BranchDeliveryPartnerConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid DeliveryPartnerId { get; set; }

        public bool IsEnabled { get; set; } = true;
    }

    public class BranchDeliveryZoneConfigurationDto : BranchConfigurationDtoBase
    {
        public string NameEn { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal DeliveryFee { get; set; }
        public decimal DeliveryCost { get; set; }
        public DeliveryPaymentMode PaymentMode { get; set; }
        public decimal? CenterLatitude { get; set; }
        public decimal? CenterLongitude { get; set; }
        public int? RadiusMeters { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public string? Notes { get; set; }
    }

    public class BranchDeliveryZoneConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        [MaxLength(120)]
        public string NameEn { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? NameAr { get; set; }

        [Required]
        [MaxLength(40)]
        public string Code { get; set; } = string.Empty;

        [Range(0, 9999999999999999.99)]
        public decimal DeliveryFee { get; set; }

        [Range(0, 9999999999999999.99)]
        public decimal DeliveryCost { get; set; }

        [EnumDataType(typeof(DeliveryPaymentMode))]
        public DeliveryPaymentMode PaymentMode { get; set; } = DeliveryPaymentMode.CustomerPays;

        [Range(-90, 90)]
        public decimal? CenterLatitude { get; set; }

        [Range(-180, 180)]
        public decimal? CenterLongitude { get; set; }

        [Range(1, int.MaxValue)]
        public int? RadiusMeters { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class BranchPrinterConfigurationDto : BranchConfigurationDtoBase
    {
        public Guid PrinterId { get; set; }
        public string PrinterName { get; set; } = string.Empty;
        public string? PrinterNameAr { get; set; }
        public bool PrinterIsActive { get; set; }
        public bool PrinterIsReceiptPrinter { get; set; }
        public string? PrinterKitchenName { get; set; }
        public string? PrinterKitchenNameAr { get; set; }
        public bool IsEnabled { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class BranchPrinterConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid PrinterId { get; set; }

        public bool IsEnabled { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }
    }

    public class BranchSettingsConfigurationDto : BranchConfigurationDtoBase
    {
        public string SettingKey { get; set; } = string.Empty;
        public string? SettingValue { get; set; }
        public string ValueType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class BranchSettingsConfigurationUpsertDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        [MaxLength(120)]
        public string SettingKey { get; set; } = string.Empty;

        public string? SettingValue { get; set; }

        [MaxLength(40)]
        public string ValueType { get; set; } = "String";

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsEnabled { get; set; } = true;
    }
}
