namespace RestaurantPos.Api.DTOs
{
    public class VoucherAvailabilityDto
    {
        public bool Enabled { get; set; }
        public decimal VoucherAmount { get; set; }
        public int DailyLimit { get; set; }
        public int UsedToday { get; set; }
        public int RemainingToday { get; set; }
        public DateOnly BusinessDate { get; set; }
    }

    public class VoucherAuditItemDto
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime UsedAt { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? CreatedByName { get; set; }
    }

    public class VoucherAuditSummaryDto : VoucherAvailabilityDto
    {
        public List<VoucherAuditItemDto> Orders { get; set; } = new();
    }
}
