namespace RestaurantPos.Api.DTOs
{
    public class OfferProductDto
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DescriptionAr { get; set; }
        public int? Calories { get; set; }
        public int Allergens { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        // Surfaced so public clients can show product-level Coming Soon state inside an offer.
        // Offer visibility is decided by the offer schedule.
        public bool IsSoon { get; set; }
    }

    public class OfferResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DescriptionAr { get; set; }
        public string? ImageUrl { get; set; }
        public int DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal FinalPrice { get; set; }
        public decimal OriginalTotal { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public bool IsActive { get; set; }
        public bool IsAvailableNow { get; set; }
        public List<OfferProductDto> Products { get; set; } = new();
    }

    public class PublicOfferDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DescriptionAr { get; set; }
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public string? ImageUrl { get; set; }
        public string? Badge { get; set; }
        public string? BadgeAr { get; set; }
    }

    public class CreateOfferRequest
    {
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DescriptionAr { get; set; }
        public string? ImageUrl { get; set; }
        public int DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public List<OfferProductItemRequest> Products { get; set; } = new();
    }

    public class OfferProductItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
