using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Retail.Domain
{
    /// <summary>
    /// A historical purchase-order HEADER imported from the workbook's PURCHASE ORDER sheet.
    ///
    /// Deliberately header-only: the workbook has no SKU-level PO lines, so there is nothing
    /// to attribute to a product and none are invented. Kept in its own table rather than in
    /// <see cref="PurchaseOrder"/> because that entity is bound to RawMaterial lines and is
    /// load-bearing for restaurant procurement, stock batches and expense recognition —
    /// writing line-less rows into it would corrupt that model.
    ///
    /// These rows create no stock movements and no COGS. They exist so the buying history is
    /// visible. The real Retail PO + PO-line model replaces this later.
    /// </summary>
    public class RetailLegacyPurchase : BaseEntity
    {
        public Guid BranchId { get; set; }

        public Guid? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        /// <summary>Supplier name exactly as the workbook recorded it, kept even when the
        /// FK resolves so the imported document still reads like its source.</summary>
        [MaxLength(200)]
        public string SupplierNameSnapshot { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string PurchaseOrderNumber { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? InvoiceNumber { get; set; }

        public DateTime OrderDateUtc { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal QuantityPieces { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SupplierCost { get; set; }

        /// <summary>The workbook lumps shipping, customs and clearance into one column.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingCustomsClearance { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        /// <summary>Workbook status text (PENDING / RECEIVED / CANCELLED).</summary>
        [MaxLength(40)]
        public string? Status { get; set; }

        /// <summary>Source row in the workbook — makes the import traceable and idempotent.</summary>
        public int SourceRowNumber { get; set; }
    }
}
