using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    public static class HrBonusTypes
    {
        public const string Performance = "performance";
        public const string Holiday = "holiday";
        public const string Annual = "annual";
        public const string Referral = "referral";
        public const string Spot = "spot";
        public const string Other = "other";

        public static readonly string[] AllOrdered =
        [
            Performance, Holiday, Annual, Referral, Spot, Other
        ];

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            Performance, Holiday, Annual, Referral, Spot, Other
        };
    }

    public static class HrBonusStatuses
    {
        public const string Pending = "pending";
        public const string Approved = "approved";
        public const string Paid = "paid";
        public const string Cancelled = "cancelled";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            Pending, Approved, Paid, Cancelled
        };

        // status transitions allowed from -> to
        public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [Pending] = new HashSet<string>(StringComparer.Ordinal) { Approved, Cancelled },
                [Approved] = new HashSet<string>(StringComparer.Ordinal) { Paid, Cancelled },
                [Paid] = new HashSet<string>(StringComparer.Ordinal),
                [Cancelled] = new HashSet<string>(StringComparer.Ordinal),
            };
    }

    public class Bonus : BaseEntity
    {
        public Guid StaffProfileId { get; set; }
        public StaffProfile StaffProfile { get; set; } = null!;

        [Required]
        [MaxLength(40)]
        public string BonusType { get; set; } = HrBonusTypes.Performance;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(10)]
        public string Currency { get; set; } = "QAR";

        public DateOnly AwardDate { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = HrBonusStatuses.Pending;

        public Guid CreatedByUserId { get; set; }

        [Required]
        [MaxLength(150)]
        public string CreatedByUserName { get; set; } = string.Empty;

        public Guid? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
