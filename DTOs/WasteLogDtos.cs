using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    public class WasteLogCreateDto
    {
        [Required]
        public string WasteType { get; set; } = string.Empty;

        public string? WasteCategory { get; set; }

        public DateTime? WasteDate { get; set; }

        public Guid? ProductId { get; set; }

        public Guid? MaterialId { get; set; }

        /// <summary>Consuming staff members. At least one required when WasteType == STAFF_MEAL.</summary>
        public List<Guid>? EmployeeIds { get; set; }

        [Range(typeof(decimal), "0.001", "999999999")]
        public decimal Quantity { get; set; }

        [MaxLength(30)]
        public string? Unit { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        public decimal? SalePriceLoss { get; set; }

        public Guid? BranchId { get; set; }

        public Guid? OrderId { get; set; }

        [MaxLength(500)]
        public string? AttachmentUrl { get; set; }

        public bool IsAffectingInventory { get; set; } = true;
    }

    public class WasteLogUpdateDto : WasteLogCreateDto
    {
    }

    public class WasteLogDecisionDto
    {
        [MaxLength(1000)]
        public string? Notes { get; set; }
    }

    public class WasteLogQueryDto
    {
        public string? Category { get; set; }
        public string? WasteType { get; set; }
        public string? Status { get; set; }
        public Guid? EmployeeId { get; set; }
        /// <summary>Filters by a staff-meal participant. Distinct from <see cref="EmployeeId"/>, which filters by recorder.</summary>
        public Guid? StaffMealEmployeeId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? MaterialId { get; set; }
        public Guid? BranchId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 50;
    }

    public class WasteLogDto
    {
        public Guid Id { get; set; }
        public string? WasteNumber { get; set; }
        public string? WasteType { get; set; }
        public string Category { get; set; } = string.Empty;
        public string WasteCategory { get; set; } = string.Empty;
        public Guid? ItemId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? MaterialId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? ItemNameAr { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? ReasonAr { get; set; }
        public string? Notes { get; set; }
        public decimal CostAmount { get; set; }
        public decimal SalePriceLoss { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid? CreatedById { get; set; }
        public string? CreatedBy { get; set; }
        public string LoggedBy { get; set; } = string.Empty;
        public List<WasteParticipantDto> Employees { get; set; } = new();
        public Guid? ApprovedById { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? DecisionAt { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? SourceOrderId { get; set; }
        public string? OrderNumber { get; set; }
        public Guid? SourceBatchId { get; set; }
        public string? BatchNumber { get; set; }
        public string? AttachmentUrl { get; set; }
        public bool IsAffectingInventory { get; set; }
        public DateTime WasteDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<WasteLogAuditDto> AuditTrail { get; set; } = new();
    }

    public class WasteLogAuditDto
    {
        public Guid Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public Guid? PerformedById { get; set; }
        public string? PerformedByName { get; set; }
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public string? Notes { get; set; }
        public string? ChangedFields { get; set; }
        public string? PreviousValues { get; set; }
        public string? NewValues { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class WasteLogPageDto
    {
        public List<WasteLogDto> Data { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
    }

    public class WasteLogSummaryDto
    {
        public int TotalCount { get; set; }
        public int CancelCount { get; set; }
        public int ExpiryCount { get; set; }
        public int ManualCount { get; set; }
        public int ManualPendingCount { get; set; }
        public int ManualApprovedCount { get; set; }
        public decimal TotalWasteCost { get; set; }
        public decimal TotalSalePriceLoss { get; set; }
        public decimal TotalQuantity { get; set; }
    }

    public class WasteLogAnalyticsDto
    {
        public int TotalCount { get; set; }
        public int ManualCount { get; set; }
        public int ExpiryCount { get; set; }
        public int CancellationCount { get; set; }
        public decimal TotalWasteCost { get; set; }
        public decimal TotalSalePriceLoss { get; set; }
        public decimal TotalQuantity { get; set; }
        public int PendingApprovalCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }

        // Staff-meal analytics (approved STAFF_MEAL records only). Additive — does not affect existing widgets.
        public int StaffMealCount { get; set; }
        public decimal StaffMealQuantity { get; set; }
        public decimal StaffMealCost { get; set; }

        public List<WasteMetricDto> WasteByType { get; set; } = new();
        public List<WasteMetricDto> WasteByEmployee { get; set; } = new();
        public List<WasteMetricDto> TopWastedProducts { get; set; } = new();
        public List<WasteMetricDto> StaffMealsByEmployee { get; set; } = new();
    }

    public class WasteEmployeeOptionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? StaffNo { get; set; }
    }

    public class WasteParticipantDto
    {
        public Guid? EmployeeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
    }

    public class WasteMetricDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? LabelAr { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostAmount { get; set; }
        public decimal SalePriceLoss { get; set; }
        public int Count { get; set; }
    }
}
