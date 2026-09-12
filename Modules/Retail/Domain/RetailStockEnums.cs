using System.Text.Json.Serialization;

namespace RestaurantPos.Api.Modules.Retail.Domain
{
    /// <summary>
    /// Kinds of retail stock movement. Append only — the ordinal is persisted.
    /// Every value carries a fixed sign, enforced by <see cref="RetailStockMovement"/>:
    /// increases are positive, decreases negative, so stock on hand is always SUM(Quantity).
    /// </summary>
    public enum RetailStockMovementType
    {
        /// <summary>The position the product started from. Exactly one per product.</summary>
        OpeningBalance = 0,
        PurchaseReceipt = 1,
        Sale = 2,
        SaleReversal = 3,
        AdjustmentIncrease = 4,
        AdjustmentDecrease = 5
    }

    /// <summary>What kind of business document produced a movement. Append only.</summary>
    public enum RetailStockSourceDocument
    {
        None = 0,
        /// <summary>Opening position imported from the DOHA LUXE workbook.</summary>
        WorkbookImport = 1,
        RetailPurchaseOrder = 2,
        Order = 3,
        ManualAdjustment = 4
    }

    /// <summary>Lifecycle of a retail purchase order. Append only.</summary>
    public enum RetailPurchaseOrderStatus
    {
        Draft = 0,
        /// <summary>Sent to the supplier; lines are frozen.</summary>
        Submitted = 1,
        /// <summary>Goods received and stock movements posted. Terminal.</summary>
        Received = 2,
        Cancelled = 3
    }

    /// <summary>How shipping / customs / clearance are spread over the ordered lines.</summary>
    /// <remarks>
    /// Named on the wire, because the purchase order form posts the choice the operator made
    /// ("ByQuantity") rather than its ordinal. Scoped to this enum rather than configured
    /// globally so no other contract changes shape. Numbers are still accepted.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RetailLandedCostAllocation
    {
        /// <summary>Charges ÷ total units, the same charge on every unit. The workbook's rule.</summary>
        ByQuantity = 0,
        /// <summary>Charges spread in proportion to each line's supplier cost.</summary>
        ByValue = 1
    }
}
