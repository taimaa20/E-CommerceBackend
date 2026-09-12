using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    public enum AssetStatus
    {
        Active = 0,
        InMaintenance = 1,
        Damaged = 2,
        Lost = 3,
        Retired = 4,
        Disposed = 5,
        InStorage = 6
    }

    public enum AssetCondition
    {
        Excellent = 0,
        Good = 1,
        Fair = 2,
        Damaged = 3,
        Critical = 4
    }

    public enum AssetAttachmentType
    {
        Invoice = 0,
        WarrantyDocument = 1,
        Manual = 2,
        Photo = 3,
        MaintenanceReport = 4,
        Receipt = 5,
        Video = 6,
        Other = 7
    }

    public enum AssetMaintenanceStatus
    {
        Scheduled = 0,
        InProgress = 1,
        Completed = 2,
        Cancelled = 3,
        Overdue = 4
    }

    public enum AssetActivityActionType
    {
        Created = 0,
        Updated = 1,
        Assigned = 2,
        MaintenanceAdded = 3,
        AttachmentUploaded = 4,
        AttachmentDeleted = 5,
        StatusChanged = 6,
        Deleted = 7,
        Restored = 8,
        CategoryCreated = 9,
        CategoryUpdated = 10,
        CategoryDeleted = 11
    }

    public sealed class AssetCategory : BaseEntity
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

        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }

    public sealed class Asset : BaseEntity
    {
        /// <summary>Owning branch — assets are physical equipment that lives at one branch.</summary>
        public Guid BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required, MaxLength(40)]
        public string AssetCode { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string AssetNameEn { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string AssetNameAr { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? DescriptionEn { get; set; }

        [MaxLength(2000)]
        public string? DescriptionAr { get; set; }

        public Guid AssetCategoryId { get; set; }
        public AssetCategory AssetCategory { get; set; } = null!;

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

        [MaxLength(512)]
        public string? QRCode { get; set; }

        public DateTime? PurchaseDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PurchaseCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CurrentValue { get; set; }

        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }

        [MaxLength(200)]
        public string? SupplierName { get; set; }

        public AssetStatus Status { get; set; } = AssetStatus.Active;
        public AssetCondition Condition { get; set; } = AssetCondition.Good;

        public Guid? AssignedToEmployeeId { get; set; }
        public StaffProfile? AssignedToEmployee { get; set; }

        [MaxLength(200)]
        public string? AssignedLocation { get; set; }

        [MaxLength(120)]
        public string? Department { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public Guid? CreatedById { get; set; }

        [MaxLength(150)]
        public string? CreatedBy { get; set; }

        public Guid? UpdatedById { get; set; }

        [MaxLength(150)]
        public string? UpdatedBy { get; set; }

        public ICollection<AssetAttachment> Attachments { get; set; } = new List<AssetAttachment>();
        public ICollection<AssetMaintenanceRecord> MaintenanceRecords { get; set; } = new List<AssetMaintenanceRecord>();
        public ICollection<AssetActivityLog> ActivityLogs { get; set; } = new List<AssetActivityLog>();
    }

    public sealed class AssetAttachment : BaseEntity
    {
        public Guid AssetId { get; set; }
        public Asset Asset { get; set; } = null!;

        public Guid? MaintenanceRecordId { get; set; }
        public AssetMaintenanceRecord? MaintenanceRecord { get; set; }

        [Required, MaxLength(180)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(260)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required, MaxLength(16)]
        public string FileExtension { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [Required, MaxLength(120)]
        public string MimeType { get; set; } = string.Empty;

        [Required, MaxLength(512)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(1024)]
        public string? FileUrl { get; set; }

        public AssetAttachmentType AttachmentType { get; set; } = AssetAttachmentType.Other;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public Guid? UploadedById { get; set; }

        [MaxLength(150)]
        public string? UploadedBy { get; set; }
    }

    public sealed class AssetMaintenanceRecord : BaseEntity
    {
        public Guid AssetId { get; set; }
        public Asset Asset { get; set; } = null!;

        [Required, MaxLength(120)]
        public string MaintenanceType { get; set; } = string.Empty;

        [Required, MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }

        [MaxLength(150)]
        public string? PerformedBy { get; set; }

        [MaxLength(200)]
        public string? Vendor { get; set; }

        public DateTime MaintenanceDate { get; set; }
        public DateTime? NextMaintenanceDate { get; set; }

        public AssetMaintenanceStatus Status { get; set; } = AssetMaintenanceStatus.Completed;

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public Guid? CreatedById { get; set; }

        [MaxLength(150)]
        public string? CreatedBy { get; set; }

        public ICollection<AssetAttachment> Attachments { get; set; } = new List<AssetAttachment>();
    }

    public sealed class AssetActivityLog : BaseEntity
    {
        public Guid AssetId { get; set; }
        public Asset Asset { get; set; } = null!;

        public AssetActivityActionType ActionType { get; set; }

        [Column(TypeName = "text")]
        public string? OldValue { get; set; }

        [Column(TypeName = "text")]
        public string? NewValue { get; set; }

        public Guid? PerformedById { get; set; }

        [MaxLength(150)]
        public string? PerformedBy { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(80)]
        public string? IpAddress { get; set; }

        [MaxLength(300)]
        public string? DeviceInfo { get; set; }
    }
}
