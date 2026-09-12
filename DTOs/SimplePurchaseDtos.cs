using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs;

public sealed class SimplePurchaseCreateDto
{
    public Guid SupplierId { get; set; }

    [MaxLength(50)]
    public string? InvoiceNumber { get; set; }

    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }

    [MinLength(1)]
    public List<SimplePurchaseItemCreateDto> Items { get; set; } = [];

    [Range(0, double.MaxValue)]
    public decimal TaxAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PaidAmount { get; set; }

    [Required, MinLength(3), MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    public string PaymentMethod { get; set; } = "Unspecified";

    [MaxLength(120)]
    public string? PaymentMethodDetail { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool ReceiveIntoInventory { get; set; }
    public bool AcknowledgeDuplicateWarning { get; set; }
}

public sealed class SimplePurchaseItemCreateDto
{
    public Guid RawMaterialId { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")]
    public decimal UnitCost { get; set; }
}

public sealed class SimplePurchaseDetailsDto
{
    public Guid Id { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public Guid? InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Balance { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string ReceivingStatus { get; set; } = string.Empty;
    public DateTime? ReceivedDate { get; set; }
    public IReadOnlyList<SimplePurchaseItemDto> Items { get; set; } = [];
}

public sealed class SimplePurchaseItemDto
{
    public Guid RawMaterialId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}
