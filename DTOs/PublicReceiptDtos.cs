using RestaurantPos.Api.Helpers;

namespace RestaurantPos.Api.DTOs
{
    public sealed class PublicReceiptShareDto
    {
        public string ReceiptUrl { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime? ExpiresAtUtc { get; set; }
    }

    public sealed class PublicReceiptDto
    {
        public PublicReceiptRestaurantDto Restaurant { get; set; } = new();
        public string OrderNumber { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string OrderSource { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public string? BranchAddress { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? DeliveryAddress { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? PaidAtUtc { get; set; }
        public DateTime? ScheduledForUtc { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal ServiceChargeAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal VoucherDiscountAmount { get; set; }
        public decimal? DeliveryFee { get; set; }
        public string? DeliveryZoneName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public string PaymentStatus { get; set; } = OrderPaymentHelper.PendingStatus;
        public bool IsRefunded { get; set; }
        public List<PublicReceiptItemDto> Items { get; set; } = new();
    }

    public sealed class PublicReceiptRestaurantDto
    {
        public string Name { get; set; } = "Sandobox";
        public string? LogoUrl { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? TaxNumber { get; set; }
        public string Currency { get; set; } = "JOD";
        public string? FooterNote { get; set; }
    }

    public sealed class PublicReceiptItemDto
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotalAmount { get; set; }
        public bool IsComplimentary { get; set; }
        public List<PublicReceiptModifierDto> Modifiers { get; set; } = new();
    }

    public sealed class PublicReceiptModifierDto
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
