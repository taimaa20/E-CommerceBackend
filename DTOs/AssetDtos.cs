using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public sealed class AssetQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string? Search { get; set; }
        public Guid? CategoryId { get; set; }
        public string? Status { get; set; }
        public string? Condition { get; set; }
        public Guid? AssignedEmployeeId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? MaintenanceStatus { get; set; }
        public string? WarrantyExpiry { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
    }

    public class AssetCreateDto
    {
        [Required, MaxLength(200)]
        public string AssetNameEn { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string AssetNameAr { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? DescriptionEn { get; set; }

        [MaxLength(2000)]
        public string? DescriptionAr { get; set; }

        [Required]
        public Guid AssetCategoryId { get; set; }

        [MaxLength(100)]
        public string? AssetType { get; set; }

        [MaxLength(120)]
        public string? Brand { get; set; }

        [MaxLength(120)]
        public string? Model { get; set; }

        [MaxLength(120)]
        public string? SerialNumber { get; set; }

        [MaxLength(120)]
        public string? Barcode { get; set; }

        public DateTime? PurchaseDate { get; set; }
        [Range(0, double.MaxValue)] public decimal? PurchaseCost { get; set; }
        [Range(0, double.MaxValue)] public decimal? CurrentValue { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }

        [MaxLength(200)]
        public string? SupplierName { get; set; }

        public string? Status { get; set; }
        public string? Condition { get; set; }
        public Guid? AssignedToEmployeeId { get; set; }

        [MaxLength(200)]
        public string? AssignedLocation { get; set; }

        [MaxLength(120)]
        public string? Department { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public sealed class AssetUpdateDto : AssetCreateDto { }

    public class AssetListItemDto
    {
        public Guid Id { get; set; }
        public string AssetCode { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string AssetNameEn { get; set; } = string.Empty;
        public string AssetNameAr { get; set; } = string.Empty;
        public Guid AssetCategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryColor { get; set; }
        public string? CategoryIcon { get; set; }
        public string? AssetType { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public string? Barcode { get; set; }
        public string? QRCode { get; set; }
        public decimal? PurchaseCost { get; set; }
        public decimal? CurrentValue { get; set; }
        public DateTime? WarrantyEndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public Guid? AssignedToEmployeeId { get; set; }
        public string? AssignedToEmployeeName { get; set; }
        public string? AssignedLocation { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; }
        public int AttachmentsCount { get; set; }
        public int MaintenanceCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class AssetDetailsDto : AssetListItemDto
    {
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public string? SupplierName { get; set; }
        public string? Notes { get; set; }
        public Guid? CreatedById { get; set; }
        public string? CreatedBy { get; set; }
        public Guid? UpdatedById { get; set; }
        public string? UpdatedBy { get; set; }
        public List<AssetAttachmentDto> Attachments { get; set; } = new();
        public List<AssetMaintenanceRecordDto> MaintenanceHistory { get; set; } = new();
        public List<AssetActivityLogDto> ActivityTimeline { get; set; } = new();
    }

    public sealed class AssetCategoryDto
    {
        public Guid Id { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public int AssetCount { get; set; }
    }

    public class AssetCategoryCreateDto
    {
        [Required, MaxLength(120)]
        public string NameEn { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string NameAr { get; set; } = string.Empty;

        [MaxLength(80)]
        public string? Icon { get; set; }

        [MaxLength(20)]
        public string? Color { get; set; }

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }

    public sealed class AssetCategoryUpdateDto : AssetCategoryCreateDto { }

    public sealed class AssetAttachmentDto
    {
        public Guid Id { get; set; }
        public Guid AssetId { get; set; }
        public Guid? MaintenanceRecordId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string AttachmentType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public Guid? UploadedById { get; set; }
        public string? UploadedBy { get; set; }
    }

    public sealed class AssetMaintenanceQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public Guid? AssetId { get; set; }
        public string? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool? DueOnly { get; set; }
    }

    public sealed class AssetMaintenanceCreateDto
    {
        [Required, MaxLength(120)]
        public string MaintenanceType { get; set; } = string.Empty;

        [Required, MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Range(0, double.MaxValue)]
        public decimal Cost { get; set; }

        [MaxLength(150)]
        public string? PerformedBy { get; set; }

        [MaxLength(200)]
        public string? Vendor { get; set; }

        [Required]
        public DateTime MaintenanceDate { get; set; }

        public DateTime? NextMaintenanceDate { get; set; }
        public string? Status { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    public sealed class AssetMaintenanceRecordDto
    {
        public Guid Id { get; set; }
        public Guid AssetId { get; set; }
        public string AssetCode { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string MaintenanceType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public string? PerformedBy { get; set; }
        public string? Vendor { get; set; }
        public DateTime MaintenanceDate { get; set; }
        public DateTime? NextMaintenanceDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int AttachmentsCount { get; set; }
    }

    public sealed class AssetActivityLogQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public Guid? AssetId { get; set; }
        public string? ActionType { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }

    public sealed class AssetActivityLogDto
    {
        public Guid Id { get; set; }
        public Guid AssetId { get; set; }
        public string AssetCode { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public Guid? PerformedById { get; set; }
        public string? PerformedBy { get; set; }
        public DateTime Timestamp { get; set; }
        public string? IpAddress { get; set; }
        public string? DeviceInfo { get; set; }
    }

    public sealed class AssetDashboardDto
    {
        public int TotalAssets { get; set; }
        public int ActiveAssets { get; set; }
        public int AssetsInMaintenance { get; set; }
        public int DamagedAssets { get; set; }
        public int ExpiringWarranties { get; set; }
        public decimal AssetValue { get; set; }
        public decimal MaintenanceCostThisMonth { get; set; }
        public List<AssetChartPointDto> AssetsByCategory { get; set; } = new();
        public List<AssetChartPointDto> AssetsByStatus { get; set; } = new();
        public List<AssetTrendPointDto> MaintenanceTrend { get; set; } = new();
        public List<AssetTrendPointDto> AssetCostTrend { get; set; } = new();
    }

    public sealed class AssetChartPointDto
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string? Color { get; set; }
    }

    public sealed class AssetTrendPointDto
    {
        public string Period { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public sealed class AssetActor
    {
        public Guid? UserId { get; set; }
        public string? Name { get; set; }
        public bool CanManageAssets { get; set; }
        public string? IpAddress { get; set; }
        public string? DeviceInfo { get; set; }
    }
}
