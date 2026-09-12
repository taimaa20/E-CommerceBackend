using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public class CostSharingOverrideDto
    {
        [EnumDataType(typeof(CostSharingMode))]
        public CostSharingMode Mode { get; set; } = CostSharingMode.RestaurantBearsAll;

        [Range(0, 100)]
        public decimal RestaurantPercentage { get; set; } = 100m;

        [Range(0, 100)]
        public decimal CounterpartyPercentage { get; set; }
    }

    public class CreatePaymentRequest
    {
        [Required]
        public Guid OrderId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Amount cannot be negative.")]
        public decimal? Amount { get; set; }

        [Required]
        [MaxLength(50)]
        public string Method { get; set; } = string.Empty;

        public Guid? PaymentMethodId { get; set; }

        [MaxLength(120)]
        public string? ReferenceNumber { get; set; }

        public DateTime? PaidAt { get; set; }

        [Range(0, 100, ErrorMessage = "Service charge rate must be between 0 and 100.")]
        public decimal? ServiceChargeRate { get; set; }

        [Range(0, 100, ErrorMessage = "Tax rate must be between 0 and 100.")]
        public decimal? TaxRate { get; set; }

        public string? CustomerPhone { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount tendered must be greater than 0 when provided.")]
        public decimal? AmountTendered { get; set; }

        public bool ApplyVoucher { get; set; } = false;

        [JsonPropertyName("clientActionId")]
        public string? ClientActionId { get; set; }

        public CostSharingOverrideDto? DeliveryPartnerCostSharingOverride { get; set; }

        public CostSharingOverrideDto? PaymentMethodCostSharingOverride { get; set; }
    }

    public class PaymentDto
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public Guid? PaymentMethodId { get; set; }
        public string? PaymentMethodName { get; set; }
        public string? PaymentMethodNameAr { get; set; }
        public string? PaymentMethodCode { get; set; }
        public string? ReferenceNumber { get; set; }
        public CostSharingMode CostSharingMode { get; set; }
        public CostSharingScope CostSharingScope { get; set; }
        public decimal CostSharingCommissionPercentage { get; set; }
        public decimal CostSharingRestaurantPercentage { get; set; }
        public decimal CostSharingCounterpartyPercentage { get; set; }
        public decimal CostSharingCommissionAmount { get; set; }
        public decimal CostSharingRestaurantShareAmount { get; set; }
        public decimal CostSharingCounterpartyShareAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OrderPaymentResultDto
    {
        public Guid OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public bool IsPaid { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? PaymentMethod { get; set; }
        public decimal? AmountTendered { get; set; }
        public decimal? ChangeAmount { get; set; }
        public bool IsVoucherApplied { get; set; }
        public decimal VoucherDiscountAmount { get; set; }
        public DateTime? VoucherAppliedAt { get; set; }
        public int? VoucherRemainingToday { get; set; }
        public decimal CostSharingTotalCommission { get; set; }
        public decimal CostSharingRestaurantShare { get; set; }
        public decimal CostSharingCounterpartyShare { get; set; }
        public decimal CostSharingNetSettlement { get; set; }
        public PaymentDto Payment { get; set; } = new();
        public List<PaymentDto> Payments { get; set; } = new();
    }
}
