using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    // ─── Enums ────────────────────────────────────────────────────────────────
    public enum PrinterType
    {
        Network = 0, // TCP/IP — port 9100 by default
        Usb = 1      // Local USB / serial — handled by an on-prem agent (out of scope here)
    }

    public enum PrintJobType
    {
        KitchenTicket = 0,   // Per-kitchen subset of an order's items
        CustomerReceipt = 1  // Full order receipt (for the cashier counter)
    }

    public enum PrintPayloadType
    {
        EscPos = 0,
        Bitmap = 1,
        Html = 2
    }

    public enum PrintJobStatus
    {
        Pending = 0,     // Waiting to be picked up by the processor
        Printing = 1,    // Currently being sent to the printer
        Completed = 2,   // Successfully printed
        Failed = 3,      // Last attempt failed; will retry while AttemptCount < MaxAttempts
        DeadLetter = 4   // Exhausted retries; manual reprint required
    }

    // ─── Kitchen ──────────────────────────────────────────────────────────────
    /// <summary>
    /// A logical preparation station that owns a subset of products and one or
    /// more printers (e.g. "Hot Line", "Cold Line", "Bar", "Pizza Oven").
    /// Tenant-scoped via BaseEntity.
    /// </summary>
    public class Kitchen : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? NameAr { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        public ICollection<Printer> Printers { get; set; } = new List<Printer>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    // ─── Printer ──────────────────────────────────────────────────────────────
    /// <summary>
    /// A physical printer assigned to a Kitchen (kitchen ticket printer) or to
    /// the cashier counter (receipt printer; KitchenId == null + IsReceiptPrinter == true).
    /// </summary>
    public class Printer : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? NameAr { get; set; }

        public Guid? KitchenId { get; set; }
        public Kitchen? Kitchen { get; set; }

        public PrinterType Type { get; set; } = PrinterType.Network;

        // Network printers
        [MaxLength(64)]
        public string? IpAddress { get; set; }

        public int Port { get; set; } = 9100;

        /// <summary>
        /// Windows print-spooler name for USB-connected printers
        /// (e.g. "XP-80C Cashir"). Used by the local print-agent service to
        /// resolve the spooler queue and write raw ESC/POS bytes via the
        /// Win32 OpenPrinter / WritePrinter API. Null for Network printers.
        /// </summary>
        [MaxLength(200)]
        public string? WindowsPrinterName { get; set; }

        /// <summary>
        /// Optional Windows port label (e.g. "USB002"). Informational only —
        /// the actual write target is <see cref="WindowsPrinterName"/>.
        /// Helpful for the operator to confirm which Windows entry maps to
        /// which row in our admin UI.
        /// </summary>
        [MaxLength(50)]
        public string? UsbPortName { get; set; }

        // ESC/POS code page — defaults to PC437 (USA, Standard Europe)
        public int CodePage { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// True when this printer prints the customer-facing full-order receipt.
        /// Kept for backward compatibility; the canonical role distinction is
        /// the (KitchenId == null) ⇒ Receipt, (KitchenId != null) ⇒ Kitchen
        /// rule, with this flag serving as the explicit Receipt marker for
        /// counter-only printers.
        /// </summary>
        public bool IsReceiptPrinter { get; set; } = false;

        /// <summary>
        /// True when this printer is the tenant's default receipt printer —
        /// used when an order's cashier has no <see cref="User.ReceiptPrinterId"/>
        /// assigned and we need a deterministic fallback.
        /// At most one printer per tenant should be flagged default; the
        /// PrintingController enforces the invariant on save by clearing the
        /// flag on every other receipt printer in the same tenant.
        /// Only meaningful when <see cref="IsReceiptPrinter"/> is true.
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>Number of copies to print per job. Defaults to 1.</summary>
        public int CopiesPerJob { get; set; } = 1;

        /// <summary>
        /// When true, the cloud-side <c>PrintQueueProcessorBackgroundService</c>
        /// skips this printer entirely; the in-process
        /// <c>LocalPrintAgentBackgroundService</c> (activated by
        /// <c>PrintAgent:Enabled = true</c> on the store PC) claims and
        /// prints its jobs.
        /// Required when the cloud host (e.g. Azure App Service) cannot
        /// reach the printer's IP — common for private LAN printers and the
        /// only path for USB-attached printers.
        /// Defaults to false: every existing printer behaves exactly as
        /// before this flag was introduced.
        /// </summary>
        public bool UseLocalAgent { get; set; } = false;
    }

    // ─── PrintJob ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Persisted print intent. The PrintQueueProcessor picks Pending/Failed jobs
    /// (where NextAttemptAt &lt;= now) and tries to send them to <see cref="Printer"/>.
    /// Storing payload bytes (base64) means a transient processor or printer
    /// outage never loses a job — it just defers it.
    /// </summary>
    public class PrintJob : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid OrderId { get; set; }

        public Guid? KitchenId { get; set; } // null for CustomerReceipt jobs

        public Guid PrinterId { get; set; }

        public PrintJobType JobType { get; set; }

        public PrintPayloadType PayloadType { get; set; } = PrintPayloadType.EscPos;

        public bool IsReprint { get; set; } = false;

        public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;

        /// <summary>
        /// Base64-encoded ESC/POS byte stream. Held in DB so a crashed processor
        /// can pick the job up after restart without re-deriving it from order state
        /// (which may have changed in the meantime).
        /// </summary>
        [Column(TypeName = "text")]
        public string PayloadBase64 { get; set; } = string.Empty;

        /// <summary>
        /// Idempotency token — uniqueness key per logical print intent
        /// (e.g. "order:{OrderId}:kitchen:{KitchenId}:batch:{BatchKey}").
        /// Prevents duplicate tickets if upstream retries the trigger.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string IdempotencyKey { get; set; } = string.Empty;

        public int AttemptCount { get; set; } = 0;
        public int MaxAttempts { get; set; } = 3;

        public DateTime? NextAttemptAt { get; set; }
        public DateTime? LastAttemptAt { get; set; }

        [Column(TypeName = "text")]
        public string? LastError { get; set; }

        public DateTime? CompletedAt { get; set; }

        // Display-only snapshot (so the queue UI doesn't need joins)
        [MaxLength(200)]
        public string? OrderNumberSnapshot { get; set; }

        [MaxLength(200)]
        public string? KitchenNameSnapshot { get; set; }
    }
}
