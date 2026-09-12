using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    public enum PaymentAdjustmentStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public class PaymentAdjustment : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid? RequestedByUserId { get; set; }
        public User? RequestedByUser { get; set; }

        [Required, MaxLength(150)]
        public string RequestedByUserName { get; set; } = string.Empty;

        public Guid? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }

        [MaxLength(150)]
        public string? ApprovedByUserName { get; set; }

        [Required, MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public PaymentAdjustmentStatus Status { get; set; } = PaymentAdjustmentStatus.Pending;
        public DateTime? ApprovedAt { get; set; }

        [MaxLength(500)]
        public string? DecisionNotes { get; set; }

        public ICollection<PaymentAdjustmentDetail> Details { get; set; } = new List<PaymentAdjustmentDetail>();
    }

    public class PaymentAdjustmentDetail : BaseEntity
    {
        public Guid AdjustmentId { get; set; }
        public PaymentAdjustment Adjustment { get; set; } = null!;

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid PaymentId { get; set; }
        public Payment Payment { get; set; } = null!;

        public Guid? NewPaymentId { get; set; }
        public Payment? NewPayment { get; set; }

        public Guid? OldPaymentMethodId { get; set; }
        public PaymentMethod? OldPaymentMethod { get; set; }

        public Guid? NewPaymentMethodId { get; set; }
        public PaymentMethod? NewPaymentMethod { get; set; }

        [Required, MaxLength(120)]
        public string OldPaymentMethodName { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? OldPaymentMethodNameAr { get; set; }

        [Required, MaxLength(120)]
        public string NewPaymentMethodName { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? NewPaymentMethodNameAr { get; set; }

        [MaxLength(50)]
        public string? NewPaymentMethodCode { get; set; }

        [MaxLength(120)]
        public string? NewReferenceNumber { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
    }
}
