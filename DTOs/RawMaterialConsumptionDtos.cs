using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    public sealed class RawMaterialConsumptionQueryDto
    {
        [Required]
        public Guid RawMaterialId { get; set; }
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? ProductId { get; set; }
        public string? OrderChannel { get; set; }
        public string? OrderStatus { get; set; }
        [MaxLength(100)] public string? Search { get; set; }
        [MaxLength(30)] public string? SortBy { get; set; } = "date";
        [MaxLength(4)] public string? SortDirection { get; set; } = "desc";
        [MaxLength(10)] public string TimelineGrouping { get; set; } = "day";
        [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
        [Range(1, 100)] public int PageSize { get; set; } = 20;
        [Range(1, int.MaxValue)] public int DetailPage { get; set; } = 1;
        [Range(1, 100)] public int DetailPageSize { get; set; } = 25;
        internal bool ExportAll { get; set; }
    }

    public sealed class RawMaterialConsumptionReportDto
    {
        public Guid RawMaterialId { get; set; }
        public string RawMaterialName { get; set; } = string.Empty;
        public string? RawMaterialNameAr { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string? BranchNameAr { get; set; }
        public decimal TotalConsumed { get; set; }
        public int ProductCount { get; set; }
        public int CategoryCount { get; set; }
        public int OrderCount { get; set; }
        public int TotalSalesQuantity { get; set; }
        public decimal AverageDailyConsumption { get; set; }
        public decimal AverageConsumptionPerOrder { get; set; }
        public RawMaterialConsumptionLeaderDto? HighestConsumingProduct { get; set; }
        public RawMaterialConsumptionLeaderDto? HighestConsumingCategory { get; set; }
        public IReadOnlyList<RawMaterialCategoryConsumptionDto> Categories { get; set; } = [];
        public IReadOnlyList<RawMaterialProductConsumptionDto> Products { get; set; } = [];
        public IReadOnlyList<RawMaterialConsumptionDetailDto> Details { get; set; } = [];
        public IReadOnlyList<RawMaterialConsumptionTimelineDto> Timeline { get; set; } = [];
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int DetailPage { get; set; }
        public int DetailPageSize { get; set; }
        public int DetailTotalCount { get; set; }
        public int DetailTotalPages { get; set; }
    }

    public sealed class RawMaterialConsumptionLeaderDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public decimal Consumed { get; set; }
    }

    public sealed class RawMaterialCategoryConsumptionDto
    {
        public Guid? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryNameAr { get; set; }
        public decimal TotalConsumed { get; set; }
        public decimal Percentage { get; set; }
        public int OrderCount { get; set; }
    }

    public sealed class RawMaterialProductConsumptionDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductNameAr { get; set; }
        public Guid? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryNameAr { get; set; }
        public int QuantitySold { get; set; }
        public decimal RecipeQuantityPerProduct { get; set; }
        public decimal TotalConsumed { get; set; }
        public decimal Percentage { get; set; }
        public int OrderCount { get; set; }
    }

    public sealed class RawMaterialConsumptionDetailDto
    {
        public Guid SnapshotId { get; set; }
        public DateTime DeductedAtUtc { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductNameAr { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryNameAr { get; set; }
        public string RawMaterialName { get; set; } = string.Empty;
        public string? RawMaterialNameAr { get; set; }
        public decimal RecipeQuantity { get; set; }
        public int SoldQuantity { get; set; }
        public decimal TotalConsumed { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string? BranchNameAr { get; set; }
        public string OrderType { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
    }

    public sealed class RawMaterialConsumptionTimelineDto
    {
        public DateTime BucketStartUtc { get; set; }
        public decimal TotalConsumed { get; set; }
        public int OrderCount { get; set; }
    }
}
