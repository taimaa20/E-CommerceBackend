using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Modules.Retail.Domain;

namespace RestaurantPos.Api.Modules.Retail.DTOs
{
    public sealed class RetailPurchaseOrderLineDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public decimal QuantityOrdered { get; set; }
        public decimal QuantityReceived { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineCost { get; set; }
        public decimal AllocatedCharges { get; set; }
        public decimal LandedUnitCost { get; set; }
    }

    public sealed class RetailPurchaseOrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string? InvoiceNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public Guid SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierNameAr { get; set; }
        public DateTime OrderDateUtc { get; set; }
        public DateTime? ExpectedDateUtc { get; set; }
        public DateTime? ReceivedAtUtc { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal CustomsCost { get; set; }
        public decimal ClearanceCost { get; set; }
        public string LandedCostAllocation { get; set; } = string.Empty;
        public decimal GoodsCost { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalUnitsOrdered { get; set; }
        public decimal TotalUnitsReceived { get; set; }
        public string? Notes { get; set; }
        /// <summary>True once the receipt has posted its stock movements. A second receive
        /// request on the same order changes nothing.</summary>
        public bool StockPosted { get; set; }
        public List<RetailPurchaseOrderLineDto> Lines { get; set; } = new();
    }

    public sealed class RetailPurchaseOrderLineInputDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Range(0.001, 1_000_000)]
        public decimal Quantity { get; set; }

        [Range(0, 1_000_000)]
        public decimal UnitCost { get; set; }
    }

    public sealed class RetailPurchaseOrderCreateDto
    {
        [Required]
        public Guid SupplierId { get; set; }

        /// <summary>Optional; generated as RPO-{year}-{sequence} when omitted.</summary>
        [MaxLength(50)]
        public string? OrderNumber { get; set; }

        [MaxLength(50)]
        public string? InvoiceNumber { get; set; }

        public DateTime? OrderDateUtc { get; set; }
        public DateTime? ExpectedDateUtc { get; set; }

        [Range(0, 10_000_000)]
        public decimal ShippingCost { get; set; }

        [Range(0, 10_000_000)]
        public decimal CustomsCost { get; set; }

        [Range(0, 10_000_000)]
        public decimal ClearanceCost { get; set; }

        public RetailLandedCostAllocation LandedCostAllocation { get; set; } = RetailLandedCostAllocation.ByQuantity;

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "A purchase order needs at least one line.")]
        public List<RetailPurchaseOrderLineInputDto> Lines { get; set; } = new();
    }

    /// <summary>One confirmed received quantity. Omitted lines are received in full.</summary>
    public sealed class RetailPurchaseReceiptLineDto
    {
        [Required]
        public Guid LineId { get; set; }

        [Range(0, 1_000_000)]
        public decimal QuantityReceived { get; set; }
    }

    public sealed class RetailPurchaseReceiptDto
    {
        [MaxLength(50)]
        public string? InvoiceNumber { get; set; }

        public List<RetailPurchaseReceiptLineDto> Lines { get; set; } = new();
    }

    public sealed class RetailPurchaseOrderQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string? Search { get; set; }
        public Guid? SupplierId { get; set; }
        public RetailPurchaseOrderStatus? Status { get; set; }
    }

    /// <summary>
    /// A manual correction of finished-goods stock. The caller states the quantity the shelf
    /// should show, not a delta — so a resubmitted form computes a zero delta and posts
    /// nothing, and the operator never has to reason about which way the sign goes.
    /// </summary>
    public sealed class RetailStockAdjustmentCreateDto
    {
        [Range(0, 9999999999.99, ErrorMessage = "New stock cannot be negative.")]
        public decimal NewStock { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "Adjustment reason is required.")]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>What an adjustment did, so the caller can show it without re-reading.</summary>
    public sealed class RetailStockAdjustmentResultDto
    {
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public decimal PreviousStock { get; set; }
        public decimal NewStock { get; set; }
        public decimal Delta { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime AdjustedAtUtc { get; set; }
        /// <summary>Null when the requested stock already matched, so nothing was posted.</summary>
        public Guid? MovementId { get; set; }
    }

    /// <summary>One row of the finished-goods stock audit trail.</summary>
    public sealed class RetailStockMovementDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? TotalCost { get; set; }
        public string SourceDocumentType { get; set; } = string.Empty;
        public Guid? SourceDocumentId { get; set; }
        public string? SourceReference { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? PerformedBySystem { get; set; }
        public string? Notes { get; set; }
        /// <summary>Running stock on hand after this movement, oldest to newest.</summary>
        public decimal BalanceAfter { get; set; }
    }
}
