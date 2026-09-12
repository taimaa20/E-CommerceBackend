namespace RestaurantPos.Api.Modules.Purchasing.DTOs
{
    /// <summary>What kind of stock a purchase order brings in. Decides the receiving path.</summary>
    public static class PurchaseItemKinds
    {
        public const string RawMaterial = "RawMaterial";
        public const string FinishedProduct = "FinishedProduct";
    }

    /// <summary>
    /// One operator-facing purchasing stage, shared by both document types so the screen reads
    /// as one workflow. <see cref="PurchaseOrderStages.Completed"/> is the only stage in which
    /// the goods are in inventory — that is what makes it safe to merge the two vocabularies.
    /// </summary>
    public static class PurchaseOrderStages
    {
        public const string Draft = "Draft";
        public const string Ordered = "Ordered";
        public const string Received = "Received";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
    }

    /// <summary>
    /// One row of the merged purchase-order list. Deliberately a projection: it carries only
    /// what the list renders plus the flags that decide which actions a row offers, so the
    /// screen never has to know which table the row came from.
    /// </summary>
    public sealed class PurchaseOrderRowDto
    {
        public Guid Id { get; set; }

        /// <summary>RawMaterial | FinishedProduct — see <see cref="PurchaseItemKinds"/>.</summary>
        public string ItemKind { get; set; } = string.Empty;

        public string DocumentNumber { get; set; } = string.Empty;
        public string? InvoiceNumber { get; set; }

        public Guid SupplierId { get; set; }
        public string SupplierDisplayName { get; set; } = string.Empty;

        public DateTime DocumentDateUtc { get; set; }
        public int LineCount { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Balance { get; set; }

        /// <summary>Unpaid | PartiallyPaid | Paid. Finished-goods orders carry no invoice yet,
        /// so they report Unpaid rather than pretending to a payment state they do not have.</summary>
        public string PaymentStatus { get; set; } = string.Empty;

        public string Stage { get; set; } = string.Empty;

        /// <summary>Set when the goods are in inventory. The one fact both models agree on.</summary>
        public bool StockPosted { get; set; }

        public Guid? InvoiceId { get; set; }

        // Row actions, resolved on the server so the screen never re-derives workflow rules.
        public bool CanSubmit { get; set; }
        public bool CanReceive { get; set; }
        public bool CanApprove { get; set; }
        public bool CanCancel { get; set; }
    }

    public sealed class PurchaseOrderDirectoryQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }

        /// <summary>Null for every kind the caller is allowed to see.</summary>
        public string? ItemKind { get; set; }

        /// <summary>Null for every stage. See <see cref="PurchaseOrderStages"/>.</summary>
        public string? Stage { get; set; }

        /// <summary>
        /// Unpaid | PartiallyPaid | Paid. Only a raw-material purchase raises a supplier
        /// invoice, so setting this narrows the list to those documents rather than guessing a
        /// payment state for orders that have none.
        /// </summary>
        public string? PaymentStatus { get; set; }
    }
}
