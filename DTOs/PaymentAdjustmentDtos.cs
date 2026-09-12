using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    public static class PaymentAdjustmentReasonCodes
    {
        public const string WrongPaymentType = "WRONG_PAYMENT_TYPE";
        public const string CustomerChangedMethod = "CUSTOMER_CHANGED_METHOD";
        public const string SplitPaymentCorrection = "SPLIT_PAYMENT_CORRECTION";
        public const string CashByMistake = "CASH_BY_MISTAKE";
        public const string CardByMistake = "CARD_BY_MISTAKE";
        public const string Other = "OTHER";
    }

    public sealed class PaymentAdjustmentCreateDto
    {
        [Required, MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ReasonText { get; set; }

        public Guid ManagerUserId { get; set; }

        [MaxLength(128)]
        public string ManagerPassword { get; set; } = string.Empty;

        [Required, MinLength(1), MaxLength(20)]
        public List<PaymentAdjustmentAllocationDto> Payments { get; set; } = new();
    }

    public sealed class PaymentAdjustmentAllocationDto
    {
        public Guid PaymentMethodId { get; set; }
        public bool IsCash { get; set; }

        [Range(typeof(decimal), "0.01", "9999999999999999")]
        public decimal Amount { get; set; }

        [MaxLength(120)]
        public string? ReferenceNumber { get; set; }
    }

    public sealed class PaymentAdjustmentApproverDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? DisplayNameAr { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public sealed class PaymentAdjustmentResultDto
    {
        public Guid AdjustmentId { get; set; }
        public Guid OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedByUserId { get; set; }
        public string? ApprovedBy { get; set; }
        public List<PaymentDto> Payments { get; set; } = new();
    }

    public sealed class PaymentAdjustmentDecisionDto
    {
        [MaxLength(500)]
        public string? ManagerNotes { get; set; }
    }

    public sealed class PaymentAdjustmentReviewDto
    {
        public Guid AdjustmentId { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public List<PaymentAdjustmentReviewPaymentDto> CurrentPayments { get; set; } = new();
        public List<PaymentAdjustmentReviewPaymentDto> ProposedPayments { get; set; } = new();
    }

    public sealed class PaymentAdjustmentReviewPaymentDto
    {
        public Guid? PaymentMethodId { get; set; }
        public string PaymentMethodName { get; set; } = string.Empty;
        public string? PaymentMethodNameAr { get; set; }
        public string? PaymentMethodCode { get; set; }
        public decimal Amount { get; set; }
        public string? ReferenceNumber { get; set; }
    }

    public sealed class PaymentAdjustmentReportFilterDto
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        [MaxLength(150)]
        public string? User { get; set; }

        [MaxLength(150)]
        public string? Manager { get; set; }

        public Guid? PaymentMethodId { get; set; }

        [MaxLength(30)]
        public string? OrderType { get; set; }
    }

    public sealed class PaymentAdjustmentHistoryDto
    {
        public Guid AdjustmentId { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string OldPaymentMethod { get; set; } = string.Empty;
        public string? OldPaymentMethodAr { get; set; }
        public string NewPaymentMethod { get; set; } = string.Empty;
        public string? NewPaymentMethodAr { get; set; }
        public decimal Amount { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime ApprovedAt { get; set; }
    }
}
