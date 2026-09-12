namespace RestaurantPos.Api.DTOs
{
    public class ValidatePriceRequest
    {
        public int? OfferId { get; set; }
        public int? OfferQty { get; set; }
        public List<OrderLineValidationItem> Items { get; set; } = new();
    }

    public class OrderLineValidationItem
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class ValidatePriceResponse
    {
        public bool IsValid { get; set; }
        public decimal ExpectedTotal { get; set; }
        public decimal SubmittedTotal { get; set; }
        public decimal Discrepancy { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> UnavailableItems { get; set; } = new();
    }
}
