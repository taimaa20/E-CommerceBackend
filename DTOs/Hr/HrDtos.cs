using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs.Hr
{
    // ===== Employee (unified Staff = User + StaffProfile) =====

    public class HrEmployeeListItemDto
    {
        public Guid EmployeeId { get; set; }      // = StaffProfile.Id (canonical)
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? FullNameAr { get; set; }
        public string Role { get; set; } = string.Empty;
        public string? StaffNo { get; set; }
        public string? Phone { get; set; }
        public string? PhotoUrl { get; set; }
        public int ContractStatus { get; set; }
        public DateTime? StartDate { get; set; }
        public decimal MonthlySalary { get; set; }
        public decimal NetSalary { get; set; }
        public int OpenRequestsCount { get; set; }
        public int PendingBonusesCount { get; set; }
    }

    public class HrEmployeeListResponse
    {
        public IReadOnlyList<HrEmployeeListItemDto> Items { get; set; } = Array.Empty<HrEmployeeListItemDto>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class HrEmployeeDetailDto
    {
        public Guid EmployeeId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? FullNameAr { get; set; }
        public string Role { get; set; } = string.Empty;

        // Profile
        public string? StaffNo { get; set; }
        public string? PinCode { get; set; }
        public int BloodType { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? PhotoUrl { get; set; }
        public DateTime? StartDate { get; set; }
        public int ContractStatus { get; set; }

        // Finance
        public decimal MonthlySalary { get; set; }
        public decimal NetSalary { get; set; }
        public decimal SgkPremium { get; set; }
        public decimal HourlyWage { get; set; }
        public decimal CommissionRate { get; set; }
        public string? WeeklyShiftPattern { get; set; }

        // Aggregated
        public HrEmployeeStatsDto Stats { get; set; } = new();
    }

    public class HrEmployeeStatsDto
    {
        public int TotalBonuses { get; set; }
        public decimal TotalBonusAmount { get; set; }
        public int OpenLeaveRequests { get; set; }
        public int OpenLoanRequests { get; set; }
        public int OpenPermissionRequests { get; set; }
        public double TotalWorkingHours { get; set; }
    }

    public class HrEmployeeCreateDto
    {
        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? FullNameAr { get; set; }

        public int Role { get; set; }            // UserRole numeric

        [MaxLength(20)] public string? StaffNo { get; set; }
        [MaxLength(6)] public string? PinCode { get; set; }
        public int BloodType { get; set; }
        [MaxLength(20)] public string? Phone { get; set; }
        [MaxLength(500)] public string? Address { get; set; }
        [MaxLength(500)] public string? PhotoUrl { get; set; }
        public DateTime? StartDate { get; set; }
        public int ContractStatus { get; set; }
        public decimal MonthlySalary { get; set; }
        public decimal NetSalary { get; set; }
        public decimal SgkPremium { get; set; }
        public decimal HourlyWage { get; set; }
        public decimal CommissionRate { get; set; }
        [MaxLength(200)] public string? WeeklyShiftPattern { get; set; }
    }

    public class HrEmployeeUpdateDto
    {
        [MaxLength(100)] public string? FullName { get; set; }
        [MaxLength(100)] public string? FullNameAr { get; set; }
        [MaxLength(20)] public string? StaffNo { get; set; }
        [MaxLength(6)] public string? PinCode { get; set; }
        public int? BloodType { get; set; }
        [MaxLength(20)] public string? Phone { get; set; }
        [MaxLength(500)] public string? Address { get; set; }
        [MaxLength(500)] public string? PhotoUrl { get; set; }
        public DateTime? StartDate { get; set; }
        public int? ContractStatus { get; set; }
        public decimal? MonthlySalary { get; set; }
        public decimal? NetSalary { get; set; }
        public decimal? SgkPremium { get; set; }
        public decimal? HourlyWage { get; set; }
        public decimal? CommissionRate { get; set; }
        [MaxLength(200)] public string? WeeklyShiftPattern { get; set; }
        public string? Password { get; set; }
        public int? Role { get; set; }
    }

    // ===== Bonus =====

    public class HrBonusDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? StaffNo { get; set; }
        public string BonusType { get; set; } = string.Empty;
        public string BonusTypeLabel { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "QAR";
        public string AwardDate { get; set; } = string.Empty;   // yyyy-MM-dd
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string CreatedByUserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class HrBonusCreateDto
    {
        [Required] public Guid EmployeeId { get; set; }
        [Required, MaxLength(40)] public string BonusType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        [MaxLength(10)] public string? Currency { get; set; }
        [Required] public DateTime AwardDate { get; set; }
        [MaxLength(500)] public string? Reason { get; set; }
        [MaxLength(1000)] public string? Notes { get; set; }
    }

    public class HrBonusUpdateStatusDto
    {
        [Required, MaxLength(20)] public string Status { get; set; } = string.Empty;
    }

    public class HrBonusListResponse
    {
        public IReadOnlyList<HrBonusDto> Items { get; set; } = Array.Empty<HrBonusDto>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public decimal TotalAmount { get; set; }
    }

    // ===== Unified Employee Requests Aggregate =====

    public class HrEmployeeRequestsDto
    {
        public IReadOnlyList<HrEmployeeRequestItemDto> Leaves { get; set; } = Array.Empty<HrEmployeeRequestItemDto>();
        public IReadOnlyList<HrEmployeeRequestItemDto> Loans { get; set; } = Array.Empty<HrEmployeeRequestItemDto>();
        public IReadOnlyList<HrEmployeeRequestItemDto> Permissions { get; set; } = Array.Empty<HrEmployeeRequestItemDto>();
    }

    public class HrEmployeeRequestItemDto
    {
        public Guid Id { get; set; }
        public string Kind { get; set; } = string.Empty;          // leave | loan | permission
        public string TypeCode { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public string Status { get; set; } = string.Empty;        // canonical lowercase code
        public string StatusLabel { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? Date { get; set; }                          // primary display date
    }

    // ===== Admin: Mobile request follow-up =====

    public class HrRequestStatusUpdateDto
    {
        [Required, MaxLength(20)]
        public string Status { get; set; } = string.Empty;       // approved | rejected | cancelled
    }

    public class HrRequestEmployeeDto
    {
        public Guid EmployeeId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? FullNameAr { get; set; }
        public string? StaffNo { get; set; }
        public string? PhotoUrl { get; set; }
    }

    public abstract class HrRequestBaseDto
    {
        public Guid Id { get; set; }
        public HrRequestEmployeeDto Employee { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public Guid? ReviewedByUserId { get; set; }
    }

    public class HrLeaveRequestDto : HrRequestBaseDto
    {
        public string Type { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public int DurationInDays { get; set; }
        public string? Notes { get; set; }
    }

    public class HrLoanRequestDto : HrRequestBaseDto
    {
        public string LoanType { get; set; } = string.Empty;
        public string LoanTypeLabel { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string? OwnerLabel { get; set; }
        public string? DependentName { get; set; }
        public string RequestDate { get; set; } = string.Empty;
        public decimal RequestedAmount { get; set; }
        public decimal MonthlyInstallment { get; set; }
        public string Currency { get; set; } = string.Empty;
        public int PaymentPeriodMonths { get; set; }
        public string InstallmentStartDate { get; set; } = string.Empty;
        public string? InstallmentEndDate { get; set; }
        public string? MarriageDate { get; set; }
        public decimal BasicSalary { get; set; }
    }

    public class HrPermissionRequestDto : HrRequestBaseDto
    {
        public string PermissionType { get; set; } = string.Empty;
        public string PermissionTypeLabel { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string TimeFrom { get; set; } = string.Empty;
        public string TimeTo { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public string? Reason { get; set; }
        public bool HasAttachment { get; set; }
        public string? AttachmentFileName { get; set; }
        public string? AttachmentContentType { get; set; }
        public long? AttachmentSizeBytes { get; set; }
    }

    public class HrRequestPagedResponse<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public IReadOnlyDictionary<string, int> StatusCounts { get; set; } =
            new Dictionary<string, int>(StringComparer.Ordinal);
    }

    // ===== Lookups =====

    public class HrLookupsDto
    {
        public IReadOnlyList<HrCodeLabelDto> LeaveTypes { get; set; } = Array.Empty<HrCodeLabelDto>();
        public IReadOnlyList<HrCodeLabelDto> LoanTypes { get; set; } = Array.Empty<HrCodeLabelDto>();
        public IReadOnlyList<HrCodeLabelDto> PermissionTypes { get; set; } = Array.Empty<HrCodeLabelDto>();
        public IReadOnlyList<HrCodeLabelDto> BonusTypes { get; set; } = Array.Empty<HrCodeLabelDto>();
        public IReadOnlyList<HrCodeLabelDto> RequestStatuses { get; set; } = Array.Empty<HrCodeLabelDto>();
        public IReadOnlyList<HrCodeLabelDto> BonusStatuses { get; set; } = Array.Empty<HrCodeLabelDto>();
        public IReadOnlyList<HrCodeLabelDto> ContractStatuses { get; set; } = Array.Empty<HrCodeLabelDto>();
        public IReadOnlyList<HrCodeLabelDto> Roles { get; set; } = Array.Empty<HrCodeLabelDto>();
    }

    public class HrCodeLabelDto
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? LabelAr { get; set; }
    }

    // ===== Timesheet =====

    public class HrTimesheetDayDto
    {
        public string Date { get; set; } = string.Empty;     // yyyy-MM-dd
        public DateTime? CheckInAt { get; set; }
        public DateTime? CheckOutAt { get; set; }
        public int DurationMinutes { get; set; }
        public string Status { get; set; } = string.Empty;   // CheckedIn | CheckedOut | Absent
    }

    public class HrTimesheetDto
    {
        public Guid EmployeeId { get; set; }
        public string Month { get; set; } = string.Empty;     // yyyy-MM
        public int TotalMinutes { get; set; }
        public double TotalHours { get; set; }
        public int DaysWorked { get; set; }
        public int OpenSessions { get; set; }                 // sessions still without a check-out
        public IReadOnlyList<HrTimesheetDayDto> Days { get; set; } = Array.Empty<HrTimesheetDayDto>();
    }
}
