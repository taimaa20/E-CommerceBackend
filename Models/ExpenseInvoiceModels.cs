using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    // Lifecycle states for an expense / supplier / store-expense invoice.
    // Append-only — order matters for migrations.
    public enum ExpenseInvoiceStatus
    {
        Draft     = 0,
        Unpaid    = 1,
        PartiallyPaid = 2,
        Paid      = 3,
        Cancelled = 4
    }

    // How the expense was settled. Free-form payment method is captured separately
    // in PaymentMethodDetail to keep this enum stable for reporting.
    public enum ExpensePaymentMethod
    {
        Unspecified  = 0,
        Cash         = 1,
        Card         = 2,
        BankTransfer = 3,
        Cheque       = 4,
        Other        = 5
    }

    public enum DuplicateInvoiceBehavior
    {
        WarningOnly = 0,
        BlockSave   = 1,
        Disabled    = 2
    }

    public enum ExpenseInvoiceAuditEventType
    {
        DuplicateWarningAcknowledged = 0,
        Cancelled = 1,
        PaymentUpdated = 2
    }

    /// <summary>
    /// Managed lookup for expense categories (Rent / Utilities / Food Stock / ...).
    /// Bilingual so the dropdown can render in the active locale without a
    /// separate translation table. SortOrder + IsActive let admins curate the
    /// picker UX (hide retired categories without losing historical references —
    /// invoices snapshot the label into ExpenseInvoice.CategoryLabel).
    /// </summary>
    public class ExpenseCategory : BaseEntity
    {
        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? NameAr { get; set; }

        // Optional accent — used by the list view to colour-tag chips.
        // Validation is client-side; we never trust this for security.
        [MaxLength(20)]
        public string? Color { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Expense / purchase / supplier-bill invoice. Lightweight (no line items yet);
    /// designed to extend toward full ERP — purchase orders, approvals, accounting,
    /// inventory integration — without schema rewrites.
    ///
    /// File contents are NEVER stored here — see <see cref="ExpenseInvoiceAttachment"/>
    /// which holds only filesystem/CDN references.
    /// </summary>
    public class ExpenseInvoice : BaseEntity
    {
        // Human-readable invoice number from the supplier. It is not a system identifier
        // and may repeat across suppliers or after supplier numbering restarts.
        [Required]
        [MaxLength(64)]
        public string InvoiceNumber { get; set; } = string.Empty;

        // Supplier identity captured as a snapshot so historical invoices remain
        // readable even after a future Suppliers module is introduced and FKs swap.
        [MaxLength(200)]
        public string? SupplierName { get; set; }

        [MaxLength(40)]
        public string? SupplierPhone { get; set; }

        [MaxLength(64)]
        public string? SupplierTaxNumber { get; set; }

        // FK to the existing Supplier (Procurement module). Nullable — manual
        // expenses (e.g. one-off store costs) may have no formal supplier on file.
        // SupplierName above remains as a denormalised snapshot so historical
        // invoices stay readable even if the supplier row is deleted later.
        public Guid? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        // FK to ExpenseCategory lookup. Nullable so manual one-offs can skip it.
        // SetNull on delete keeps historical rows intact — they retain the
        // CategoryLabel snapshot for reporting even after the category is removed.
        public Guid? ExpenseCategoryId { get; set; }
        public ExpenseCategory? ExpenseCategory { get; set; }

        // Denormalised label captured at write-time. Lets reports group historic
        // rows when the category row has been deleted or renamed.
        [MaxLength(120)]
        public string? CategoryLabel { get; set; }

        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        // Stored value (not recomputed) — locks the historical total against any
        // future rule changes on subtotal/tax/discount handling.
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        public Guid? PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }

        // ISO 4217 currency code. Defaults are tenant-driven from SystemSettings
        // at write-time, not derived here.
        [Required]
        [MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD";

        public ExpensePaymentMethod PaymentMethod { get; set; } = ExpensePaymentMethod.Unspecified;

        // Free-text "Wire ref #12345" / "Cheque 00789" — kept separate from the
        // enum so reporting groups still work.
        [MaxLength(120)]
        public string? PaymentMethodDetail { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public ExpenseInvoiceStatus Status { get; set; } = ExpenseInvoiceStatus.Unpaid;

        // FK to the configurable PaymentMethods registry. Nullable — back-office
        // invoices entered before/outside the registry keep the enum only. The
        // resolved display label is snapshotted into PaymentMethodDetail.
        public Guid? PaymentMethodId { get; set; }

        // ── Cashier shift linkage ──────────────────────────────────────────
        // Non-null only for expenses recorded by a cashier from an open drawer
        // session. Back-office supplier bills leave this null and therefore
        // never touch any shift's expected cash.
        // SetNull on shift delete — same rule as CreatedById: losing the parent
        // must never wipe the financial record.
        public Guid? CashierShiftId { get; set; }

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        // Who entered this invoice. SetNull on user delete so audit trail stays.
        public Guid? CreatedById { get; set; }
        public User? CreatedByUser { get; set; }

        // Snapshot — survives user renames/deletes so the shift history stays readable.
        [MaxLength(150)]
        public string? CreatedByName { get; set; }

        // ── Void / cancel trail ────────────────────────────────────────────
        // A cancelled expense stops affecting active financial totals but the
        // original amount, author, shift and timestamps are never mutated.
        public DateTime? CancelledAt { get; set; }

        public Guid? CancelledById { get; set; }

        [MaxLength(150)]
        public string? CancelledByName { get; set; }

        [MaxLength(500)]
        public string? CancellationReason { get; set; }

        public ICollection<ExpenseInvoiceAttachment> Attachments { get; set; }
            = new List<ExpenseInvoiceAttachment>();

        public ICollection<ExpenseInvoiceAuditLog> AuditLogs { get; set; }
            = new List<ExpenseInvoiceAuditLog>();
    }

    public class ExpenseInvoiceAuditLog : BaseEntity
    {
        public Guid ExpenseInvoiceId { get; set; }
        public ExpenseInvoice? ExpenseInvoice { get; set; }

        public ExpenseInvoiceAuditEventType EventType { get; set; }

        public Guid? ActorUserId { get; set; }

        [MaxLength(150)]
        public string? ActorUserName { get; set; }

        [Required]
        [MaxLength(64)]
        public string InvoiceNumber { get; set; } = string.Empty;

        public Guid? SupplierId { get; set; }

        [MaxLength(200)]
        public string? SupplierName { get; set; }

        public DateTime InvoiceDate { get; set; }

        /// <summary>Event timestamp. Named for the original duplicate-acknowledgement
        /// event; reused as the occurred-at stamp for every appended event type.</summary>
        public DateTime AcknowledgedAt { get; set; }

        /// <summary>Free-text justification supplied by the actor (cancellation reason).</summary>
        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// File reference for an <see cref="ExpenseInvoice"/>. Stores ONLY metadata
    /// and a logical path — never the bytes themselves. Swapping local disk for
    /// Azure Blob / S3 later requires changing only the storage service, not
    /// this schema.
    /// </summary>
    public class ExpenseInvoiceAttachment : BaseEntity
    {
        public Guid ExpenseInvoiceId { get; set; }
        public ExpenseInvoice? ExpenseInvoice { get; set; }

        // Original name supplied by the client — kept for download UX only.
        // NEVER trusted on disk: see StoredFileName.
        [Required]
        [MaxLength(260)]
        public string OriginalFileName { get; set; } = string.Empty;

        // Server-generated, sanitised name actually written to storage.
        [Required]
        [MaxLength(160)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string FileExtension { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [Required]
        [MaxLength(120)]
        public string MimeType { get; set; } = string.Empty;

        // Storage-provider-relative path. For local disk this is
        // "expense-invoices/2026/05/abc123.pdf"; for blob storage it becomes
        // the blob key — no schema change required.
        [Required]
        [MaxLength(512)]
        public string RelativePath { get; set; } = string.Empty;

        // Public URL when applicable (CDN / blob with public access). Nullable
        // because in secure-only mode we never expose the file publicly.
        [MaxLength(1024)]
        public string? FullUrl { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public Guid? UploadedById { get; set; }
        public User? UploadedByUser { get; set; }

        // First attachment uploaded is marked primary — used as the thumbnail
        // in the list view. At most one primary per invoice (enforced in service).
        public bool IsPrimary { get; set; }
    }
}
