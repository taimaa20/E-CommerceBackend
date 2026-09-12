using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    public static class MobileRequestStatuses
    {
        public const string Pending = "pending";
        public const string Approved = "approved";
        public const string Rejected = "rejected";
        public const string Cancelled = "cancelled";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            Pending,
            Approved,
            Rejected,
            Cancelled
        };

        // Pending is the only state that may transition. Terminal states are immutable.
        public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [Pending] = new HashSet<string>(StringComparer.Ordinal) { Approved, Rejected, Cancelled }
            };
    }

    public static class MobileLeaveRequestTypes
    {
        public const string Annual = "annual";
        public const string Sick = "sick";
        public const string Personal = "personal";
        public const string Emergency = "emergency";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            Annual,
            Sick,
            Personal,
            Emergency
        };
    }

    public static class MobileLoanRequestTypes
    {
        public const string MarriageLoan = "marriage_loan";
        public const string LifeExpensesLoan = "life_expenses_loan";
        public static readonly string[] AllOrdered = [MarriageLoan, LifeExpensesLoan];

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            MarriageLoan,
            LifeExpensesLoan
        };
    }

    public static class MobileLoanOwners
    {
        public const string Self = "self";
        public const string Dependent = "dependent";
        public static readonly string[] AllOrdered = [Self, Dependent];

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            Self,
            Dependent
        };
    }

    public static class MobilePermissionRequestTypes
    {
        public const string Personal = "personal";
        public const string Official = "official";
        public const string Medical = "medical";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            Personal,
            Official,
            Medical
        };
    }

    public class MobileLeaveRequest : BaseEntity
    {
        public Guid StaffProfileId { get; set; }
        public StaffProfile StaffProfile { get; set; } = null!;

        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int DurationInDays { get; set; }

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = MobileLeaveRequestTypes.Annual;

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = MobileRequestStatuses.Pending;

        public Guid CreatedByUserId { get; set; }

        [Required]
        [MaxLength(150)]
        public string CreatedByUserName { get; set; } = string.Empty;

        public DateTime? ReviewedAt { get; set; }
        public Guid? ReviewedByUserId { get; set; }
    }

    public class MobileLoanRequest : BaseEntity
    {
        public Guid StaffProfileId { get; set; }
        public StaffProfile StaffProfile { get; set; } = null!;

        public DateOnly RequestDate { get; set; }
        public int PaymentPeriodMonths { get; set; }
        public DateOnly? EmploymentStartDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PreviousLoanBalance { get; set; }

        [Required]
        [MaxLength(40)]
        public string LoanType { get; set; } = MobileLoanRequestTypes.MarriageLoan;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RequestedAmount { get; set; }

        [Required]
        [MaxLength(10)]
        public string Currency { get; set; } = MobileLoanPolicies.DefaultCurrency;

        public DateOnly InstallmentStartDate { get; set; }
        public DateOnly? InstallmentEndDate { get; set; }
        public DateOnly? MarriageDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BasicSalary { get; set; }

        [Required]
        [MaxLength(20)]
        public string Owner { get; set; } = MobileLoanOwners.Self;

        [MaxLength(100)]
        public string? Nationality { get; set; }

        [MaxLength(150)]
        public string? DependentName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyInstallment { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = MobileRequestStatuses.Pending;

        public Guid CreatedByUserId { get; set; }

        [Required]
        [MaxLength(150)]
        public string CreatedByUserName { get; set; } = string.Empty;

        public DateTime? ReviewedAt { get; set; }
        public Guid? ReviewedByUserId { get; set; }
    }

    public class MobilePermissionRequest : BaseEntity
    {
        public Guid StaffProfileId { get; set; }
        public StaffProfile StaffProfile { get; set; } = null!;

        public DateOnly Date { get; set; }

        [Required]
        [MaxLength(20)]
        public string PermissionType { get; set; } = MobilePermissionRequestTypes.Personal;

        public TimeOnly TimeFrom { get; set; }
        public TimeOnly TimeTo { get; set; }
        public int DurationMinutes { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = MobileRequestStatuses.Pending;

        [MaxLength(500)]
        public string? AttachmentStorageKey { get; set; }

        [MaxLength(255)]
        public string? AttachmentOriginalFileName { get; set; }

        [MaxLength(100)]
        public string? AttachmentContentType { get; set; }

        public long? AttachmentSizeBytes { get; set; }

        public Guid CreatedByUserId { get; set; }

        [Required]
        [MaxLength(150)]
        public string CreatedByUserName { get; set; } = string.Empty;

        public DateTime? ReviewedAt { get; set; }
        public Guid? ReviewedByUserId { get; set; }
    }

    public static class MobileLoanPolicies
    {
        public const string DefaultCurrency = "QAR";
        public const int MaxTenureMonths = 60;
        public const decimal MinRequestedAmount = 1000m;
        public const decimal MaxRequestedAmount = 500000m;
        public const decimal LifeExpensesMaxMultipleOfSalary = 5m;
    }

    public static class MobilePermissionPolicies
    {
        public const int MonthlyAllowanceMinutes = 480;
        public const long MaxAttachmentBytes = 5 * 1024 * 1024;
        public const long MaxMultipartBytes = MaxAttachmentBytes + 1024 * 1024;
    }
}
