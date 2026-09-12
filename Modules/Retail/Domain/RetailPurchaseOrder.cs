using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Retail.Domain
{
    /// <summary>
    /// An operational purchase order for retail finished goods, with real SKU-level lines.
    ///
    /// It uses the SHARED <see cref="Supplier"/> master — one supplier id means the same
    /// supplier in restaurant procurement and here. Only the lines are separate, because
    /// <c>PurchaseOrderItem.RawMaterialId</c> is non-nullable and load-bearing across
    /// procurement, warehouses, expense recognition and the financial-integrity tests;
    /// widening it would put the restaurant's costing chain at risk to save one table.
    ///
    /// Legacy header-only workbook history stays in <see cref="RetailLegacyPurchase"/> and is
    /// never migrated into this table — the workbook has no lines to migrate.
    /// </summary>
    public class RetailPurchaseOrder : BaseEntity
    {
        public Guid BranchId { get; set; }

        public Guid SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        /// <summary>Unique per tenant. Generated as RPO-{yyyy}-{sequence} when not supplied.</summary>
        [Required]
        [MaxLength(50)]
        public string OrderNumber { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? InvoiceNumber { get; set; }

        public RetailPurchaseOrderStatus Status { get; set; } = RetailPurchaseOrderStatus.Draft;

        public DateTime OrderDateUtc { get; set; }

        public DateTime? ExpectedDateUtc { get; set; }

        public DateTime? ReceivedAtUtc { get; set; }

        public Guid? ReceivedByUserId { get; set; }

        /// <summary>
        /// Set the moment the receipt posts its stock movements, and checked before posting.
        /// Makes receiving idempotent at the document level: a replayed submit finds the stamp
        /// and returns the same result instead of doubling stock.
        /// </summary>
        public DateTime? StockPostedAtUtc { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CustomsCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ClearanceCost { get; set; }

        public RetailLandedCostAllocation LandedCostAllocation { get; set; }
            = RetailLandedCostAllocation.ByQuantity;

        /// <summary>Sum of the line supplier costs, before landed charges.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal GoodsCost { get; set; }

        /// <summary>Goods cost plus shipping, customs and clearance.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid? CreatedByUserId { get; set; }

        public ICollection<RetailPurchaseOrderLine> Lines { get; set; } = new List<RetailPurchaseOrderLine>();

        /// <summary>Total of the charges that have to be spread across the lines.</summary>
        public decimal LandedCharges => ShippingCost + CustomsCost + ClearanceCost;
    }

    /// <summary>
    /// One SKU on a retail purchase order. Snapshots the SKU and product name so the document
    /// still reads correctly after the catalogue changes.
    /// </summary>
    public class RetailPurchaseOrderLine : BaseEntity
    {
        public Guid RetailPurchaseOrderId { get; set; }
        public RetailPurchaseOrder? RetailPurchaseOrder { get; set; }

        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        [MaxLength(64)]
        public string SkuSnapshot { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ProductNameSnapshot { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,3)")]
        public decimal QuantityOrdered { get; set; }

        /// <summary>Confirmed at receipt. Zero until then.</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal QuantityReceived { get; set; }

        /// <summary>Supplier price per unit, before landed charges.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitCost { get; set; }

        /// <summary>This line's share of the order's shipping / customs / clearance.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal AllocatedCharges { get; set; }

        /// <summary>Unit cost including the allocated charges — the cost that enters stock.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal LandedUnitCost { get; set; }

        public int SortOrder { get; set; }
    }
}
