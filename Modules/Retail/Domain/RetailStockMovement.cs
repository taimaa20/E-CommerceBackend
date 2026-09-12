using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Retail.Domain
{
    /// <summary>
    /// One append-only entry in the retail finished-goods stock ledger.
    ///
    /// Stock on hand is <c>SUM(Quantity)</c> over the non-deleted movements of a product in a
    /// branch — there is no second, stored balance to drift from it. Rows are never edited or
    /// deleted to undo an effect: a mistake is corrected by posting a compensating movement,
    /// which is why <see cref="ReversesMovementId"/> exists.
    ///
    /// This is deliberately NOT <c>InventoryTransaction</c> (waste-only, raw-material keyed) and
    /// NOT <c>StockBatch</c> (FIFO batches of a RawMaterial). Retail stocks the sellable
    /// <see cref="Product"/> itself, so the ledger is keyed on ProductId.
    /// </summary>
    public class RetailStockMovement : BaseEntity
    {
        public Guid ProductId { get; set; }
        // Nullable navigation: rows are written with the FK set explicitly and read through
        // projections, so a required nav would force loads that are never needed.
        public Product? Product { get; set; }

        public Guid BranchId { get; set; }

        public RetailStockMovementType MovementType { get; set; }

        /// <summary>
        /// Signed quantity: positive increases stock, negative decreases it. The sign is not a
        /// caller decision — <see cref="SignedQuantity"/> derives it from the movement type.
        /// </summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; }

        /// <summary>Landed unit cost applying to this movement. Null when cost is not knowable
        /// (a pure quantity correction). For a Sale this is the cost snapshot that fixes the
        /// line's COGS forever.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal? UnitCost { get; set; }

        /// <summary>Signed extended cost, <c>Quantity × UnitCost</c>. Stored so valuation and
        /// COGS never have to re-multiply across millions of rows.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalCost { get; set; }

        public RetailStockSourceDocument SourceDocumentType { get; set; }

        /// <summary>Header id of the source document (retail PO, order).</summary>
        public Guid? SourceDocumentId { get; set; }

        /// <summary>
        /// Line id inside the source document (PO line, OrderItem). This is the exactly-once
        /// key: a unique index over (TenantId, MovementType, SourceLineId) makes a second
        /// posting for the same line physically impossible.
        /// </summary>
        public Guid? SourceLineId { get; set; }

        /// <summary>Human-readable document reference ("PO-2026-0007", order number) so the
        /// ledger reads correctly even after the source row is archived.</summary>
        [MaxLength(80)]
        public string? SourceReference { get; set; }

        /// <summary>Business time of the movement. Differs from <c>CreatedAt</c> for imported
        /// history, which is back-dated to when the goods actually moved.</summary>
        public DateTime OccurredAtUtc { get; set; }

        public Guid? PerformedByUserId { get; set; }

        /// <summary>Non-interactive origin when there is no user: "workbook-import",
        /// "order-fulfilment", "purchase-receipt".</summary>
        [MaxLength(40)]
        public string? PerformedBySystem { get; set; }

        /// <summary>Set on a reversal to point at the movement it compensates.</summary>
        public Guid? ReversesMovementId { get; set; }

        [MaxLength(300)]
        public string? Notes { get; set; }

        /// <summary>True for the movement types that add stock.</summary>
        public static bool IsIncrease(RetailStockMovementType type)
            => type is RetailStockMovementType.OpeningBalance
                    or RetailStockMovementType.PurchaseReceipt
                    or RetailStockMovementType.SaleReversal
                    or RetailStockMovementType.AdjustmentIncrease;

        /// <summary>
        /// Converts an absolute quantity into the signed value the ledger stores. Callers pass
        /// magnitudes; the movement type decides the direction, so a caller cannot accidentally
        /// post a sale that increases stock.
        /// </summary>
        public static decimal SignedQuantity(RetailStockMovementType type, decimal absoluteQuantity)
        {
            var magnitude = Math.Abs(absoluteQuantity);
            return IsIncrease(type) ? magnitude : -magnitude;
        }
    }
}
