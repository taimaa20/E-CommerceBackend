using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    public class BranchProduct : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public bool IsAvailable { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public int DisplayOrder { get; set; }

        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
    }

    public class BranchCategory : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        public bool IsVisible { get; set; } = true;
        public int DisplayOrder { get; set; }

        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
    }

    public class BranchSubcategory : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid SubcategoryId { get; set; }
        public Subcategory Subcategory { get; set; } = null!;

        public bool IsVisible { get; set; } = true;
        public int DisplayOrder { get; set; }

        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
    }

    public class BranchModifier : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid ModifierId { get; set; }
        public Modifier Modifier { get; set; } = null!;

        public bool IsAvailable { get; set; } = true;

        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
    }

    public class BranchModifierGroup : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid ModifierGroupId { get; set; }
        public ModifierGroup ModifierGroup { get; set; } = null!;

        public bool IsAvailable { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public int DisplayOrder { get; set; }

        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
    }

    public class BranchProductOption : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid ProductOptionId { get; set; }
        public ProductOption ProductOption { get; set; } = null!;

        public bool IsAvailable { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public int DisplayOrder { get; set; }

        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
    }

    public class BranchPaymentMethod : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid PaymentMethodId { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = null!;

        public bool IsEnabled { get; set; } = true;

        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
    }

    public class BranchDeliveryPartner : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid DeliveryPartnerId { get; set; }
        public DeliveryPartner DeliveryPartner { get; set; } = null!;

        public bool IsEnabled { get; set; } = true;

        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
    }

    public class BranchPrinter : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid PrinterId { get; set; }
        public Printer Printer { get; set; } = null!;

        public bool IsEnabled { get; set; } = true;
        public int DisplayOrder { get; set; }

        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
    }

    // Offers keep their legacy int PK (not a BaseEntity), so this config row uses an
    // int FK unlike the other Guid-keyed configuration types.
    public class BranchOffer : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public int OfferId { get; set; }
        public Offer Offer { get; set; } = null!;

        public bool IsEnabled { get; set; } = true;

        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
    }

    public class BranchSettings : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        [Required]
        [MaxLength(120)]
        public string SettingKey { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? SettingValue { get; set; }

        [MaxLength(40)]
        public string ValueType { get; set; } = "String";

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsEnabled { get; set; } = true;

        public Guid? CreatedById { get; set; }
        public Guid? UpdatedById { get; set; }
    }
}
