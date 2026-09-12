using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs.ExpenseInvoice
{
    // ──────────────────────────── INPUT DTOs ────────────────────────────────

    public class CreateExpenseInvoiceDto
    {
        [Required]
        [MaxLength(64)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [MaxLength(200)] public string? SupplierName { get; set; }
        [MaxLength(40)]  public string? SupplierPhone { get; set; }
        [MaxLength(64)]  public string? SupplierTaxNumber { get; set; }
        public Guid? SupplierId { get; set; }

        public Guid? ExpenseCategoryId { get; set; }
        [MaxLength(120)] public string? CategoryLabel { get; set; }

        [Required]
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }

        [Range(0, double.MaxValue)] public decimal Subtotal { get; set; }
        [Range(0, double.MaxValue)] public decimal TaxAmount { get; set; }
        [Range(0, double.MaxValue)] public decimal DiscountAmount { get; set; }
        [Range(0, double.MaxValue)] public decimal TotalAmount { get; set; }
        [Range(0, double.MaxValue)] public decimal? PaidAmount { get; set; }
        public Guid? PurchaseOrderId { get; set; }

        [Required]
        [MinLength(3), MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD";

        public string PaymentMethod { get; set; } = "Unspecified";
        [MaxLength(120)] public string? PaymentMethodDetail { get; set; }

        [MaxLength(2000)] public string? Notes { get; set; }

        public string Status { get; set; } = "Unpaid";

        public Guid? BranchId { get; set; }

        public bool AcknowledgeDuplicateWarning { get; set; }
    }

    public class UpdateExpenseInvoiceDto : CreateExpenseInvoiceDto { }

    public class UpdateExpenseInvoicePaymentDto
    {
        [Range(0, double.MaxValue)]
        public decimal PaidAmount { get; set; }

        public string? PaymentMethod { get; set; }

        [MaxLength(120)]
        public string? PaymentMethodDetail { get; set; }
    }

    /// <summary>
    /// Filter / pagination contract for the list endpoint.
    /// All filters are optional — combine freely.
    /// </summary>
    public class ExpenseInvoiceQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;

        public string? InvoiceNumber { get; set; }
        public string? Supplier { get; set; }
        public Guid? SupplierId { get; set; }
        public Guid? ExpenseCategoryId { get; set; }
        public string? Status { get; set; }
        public string? PaymentMethod { get; set; }
        public Guid? BranchId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }

    // ──────────────────────────── OUTPUT DTOs ───────────────────────────────

    /// <summary>Compact row for the list page. No nested attachments.</summary>
    public class ExpenseInvoiceListItemDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? SupplierName { get; set; }
        public Guid? SupplierId { get; set; }
        public string? CategoryLabel { get; set; }
        public Guid? ExpenseCategoryId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Balance { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public int AttachmentsCount { get; set; }
        public string? PrimaryAttachmentUrl { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
    }

    /// <summary>Full detail view. Includes lightweight attachment metadata.</summary>
    public class ExpenseInvoiceDetailsDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;

        public string? SupplierName { get; set; }
        public string? SupplierPhone { get; set; }
        public string? SupplierTaxNumber { get; set; }
        public Guid? SupplierId { get; set; }

        public Guid? ExpenseCategoryId { get; set; }
        public string? CategoryLabel { get; set; }

        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }

        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Balance { get; set; }
        public Guid? PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }

        public string CurrencyCode { get; set; } = "USD";
        public string PaymentMethod { get; set; } = string.Empty;
        public string? PaymentMethodDetail { get; set; }

        public string? Notes { get; set; }
        public string Status { get; set; } = string.Empty;

        public Guid? BranchId { get; set; }

        public Guid? CreatedById { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<ExpenseInvoiceAttachmentDto> Attachments { get; set; } = new();
    }

    public class ExpenseInvoiceAttachmentDto
    {
        public Guid Id { get; set; }
        public Guid ExpenseInvoiceId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;

        // Caller-facing absolute URL — built by the controller using the
        // request's scheme + host. Never trust the persisted FullUrl when
        // running behind a reverse proxy or after a host change.
        public string Url { get; set; } = string.Empty;

        // Secure-download route. Frontend uses this for PDFs / hidden links.
        public string DownloadUrl { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; }
        public string? UploadedByName { get; set; }
        public bool IsPrimary { get; set; }
    }

    /// <summary>Dashboard tile / statistics widget contract.</summary>
    public class ExpenseInvoiceStatisticsDto
    {
        public int TotalCount { get; set; }
        public int UnpaidCount { get; set; }
        public int OverdueCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal UnpaidAmount { get; set; }
        public decimal ThisMonthAmount { get; set; }
        public string CurrencyCode { get; set; } = "USD";
    }

    public class ExpenseInvoiceDuplicateWarningDto
    {
        public string Code { get; set; } = "duplicateInvoice";
        public bool CanOverride { get; set; }
        public string Behavior { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? SupplierName { get; set; }
        public DateTime InvoiceDate { get; set; }
    }
}
