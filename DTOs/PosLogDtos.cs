namespace RestaurantPos.Api.DTOs
{
    public static class PosLogTypes
    {
        public const string Cancel = "CANCEL";
        public const string Waste = "WASTE";
    }

    public static class PosLogTypeFilters
    {
        public const string Cancel = "cancel";
        public const string Cancelled = "cancelled";
        public const string Waste = "waste";
    }

    public static class PosLogDateFilters
    {
        public const string Today = "today";
        public const string All = "all";
    }

    public static class PosLogSources
    {
        public const string Order = "ORDER";
        public const string Item = "ITEM";
    }

    public class PosLogQueryDto
    {
        public string Date { get; set; } = PosLogDateFilters.Today;
        public string? Type { get; set; }
        public Guid? ShiftId { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 50;
    }

    public class PosLogEntryDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public Guid? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public Guid? ItemId { get; set; }
        public Guid? OrderItemId { get; set; }
        public string? ItemName { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid? ShiftId { get; set; }
        public int? ShiftNumber { get; set; }
        public string? ActorName { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public class PosLogPageDto
    {
        public List<PosLogEntryDto> Data { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
    }
}
