using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    public class KitchenDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public int PrinterCount { get; set; }
    }

    public class KitchenUpsertDto
    {
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [MaxLength(100)] public string? NameAr { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
    }

    public class PrinterDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public Guid? KitchenId { get; set; }
        public string? KitchenName { get; set; }
        public int Type { get; set; }
        public string? IpAddress { get; set; }
        public int Port { get; set; }
        public string? WindowsPrinterName { get; set; }
        public string? UsbPortName { get; set; }
        public int CodePage { get; set; }
        public bool IsActive { get; set; }
        public bool IsReceiptPrinter { get; set; }
        public bool IsDefault { get; set; }
        public bool UseLocalAgent { get; set; }
        public int CopiesPerJob { get; set; }
    }

    public class PrinterUpsertDto
    {
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [MaxLength(100)] public string? NameAr { get; set; }
        public Guid? KitchenId { get; set; }
        public int Type { get; set; } = 0;
        [MaxLength(64)] public string? IpAddress { get; set; }
        public int Port { get; set; } = 9100;
        [MaxLength(200)] public string? WindowsPrinterName { get; set; }
        [MaxLength(50)] public string? UsbPortName { get; set; }
        public int CodePage { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public bool IsReceiptPrinter { get; set; } = false;
        public bool IsDefault { get; set; } = false;
        public bool UseLocalAgent { get; set; } = false;
        public int CopiesPerJob { get; set; } = 1;
    }

    // ─── Agent wire format ────────────────────────────────────────────────
    /// <summary>One pending job, ready to be printed by the local agent.</summary>
    public class AgentJobDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public Guid PrinterId { get; set; }
        public string PrinterName { get; set; } = string.Empty;
        public int PrinterType { get; set; } // 0=Network, 1=Usb
        public string? IpAddress { get; set; }
        public int Port { get; set; }
        public string? WindowsPrinterName { get; set; }
        public int CopiesPerJob { get; set; }
        public int CodePage { get; set; }
        public int JobType { get; set; } // 0=KitchenTicket, 1=CustomerReceipt
        public int PayloadType { get; set; } // 0=EscPos, 1=Bitmap, 2=Html
        public string? KitchenName { get; set; }
        /// <summary>Base64 payload bytes. Html payloads are rendered by the sidecar.</summary>
        public string PayloadBase64 { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; }
    }

    public class AgentResultDto
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Body of POST /api/printing/orders/{id}/receipt-image — base64 PNG
    /// rendered by the frontend via html2canvas. Backend converts the PNG
    /// to ESC/POS raster bytes and enqueues a print job.
    /// </summary>
    public class ReceiptImageDto
    {
        [Required]
        public string ImageBase64 { get; set; } = string.Empty;
    }

    /// <summary>Cashier user with their assigned receipt printer (if any).</summary>
    public class CashierPrinterAssignmentDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? FullNameAr { get; set; }
        public string Role { get; set; } = string.Empty;
        public Guid? ReceiptPrinterId { get; set; }
        public string? ReceiptPrinterName { get; set; }
    }

    public class CashierPrinterUpdateDto
    {
        public Guid? ReceiptPrinterId { get; set; }
    }

    /// <summary>
    /// Slim printer-config DTO returned to the cashier UI so it can cache the
    /// assigned printer for offline (loopback) printing.
    /// </summary>
    public class MyReceiptPrinterDto
    {
        public int PrinterType { get; set; }   // 0=Network, 1=Usb
        public string? WindowsPrinterName { get; set; }
        public string? IpAddress { get; set; }
        public int Port { get; set; }
        public int Copies { get; set; } = 1;
    }

    public class PrintJobDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public Guid? KitchenId { get; set; }
        public string? KitchenName { get; set; }
        public Guid PrinterId { get; set; }
        public int JobType { get; set; }
        public bool IsReprint { get; set; }
        public int Status { get; set; }
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? LastError { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ResendKitchenTicketRequest
    {
        public Guid OrderId { get; set; }
        public List<Guid> KitchenIds { get; set; } = new();
    }

    public class ResendKitchenTicketJobDto
    {
        public Guid JobId { get; set; }
        public Guid KitchenId { get; set; }
        public string KitchenName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
    }

    public class ResendKitchenTicketResponse
    {
        public bool Enqueued { get; set; }
        public List<ResendKitchenTicketJobDto> Jobs { get; set; } = new();
        public List<Guid> SkippedKitchenIds { get; set; } = new();
        public string? Reason { get; set; }
    }
}
