using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RestaurantPos.Api.DTOs
{
    public class CashierShiftQueryDto
    {
        public bool? IsClosed { get; set; }
        public Guid? CashierId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public DateTime? ToExclusive { get; set; }
    }

    public class CashierShiftOwnerOptionDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class OpenShiftRequest
    {
        [Required]
        public Guid CashierId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Opening balance must be non-negative.")]
        public decimal OpeningBalance { get; set; }

        public string? Notes { get; set; }

        [JsonPropertyName("clientActionId")]
        public string? ClientActionId { get; set; }

        /// <summary>
        /// Optional client-supplied POS device identifier. Required only when the
        /// tenant has selected ActiveShiftRule = SinglePerPosDevice.
        /// </summary>
        [MaxLength(120)]
        public string? PosDeviceId { get; set; }
    }

    public class OpenOwnShiftRequest
    {
        [Range(0, double.MaxValue, ErrorMessage = "Opening balance must be non-negative.")]
        public decimal OpeningBalance { get; set; }

        public string? Notes { get; set; }

        [JsonPropertyName("clientActionId")]
        public string? ClientActionId { get; set; }

        [MaxLength(120)]
        public string? PosDeviceId { get; set; }
    }

    public class CloseShiftRequest
    {
        [Range(0, double.MaxValue, ErrorMessage = "Closing balance must be non-negative.")]
        public decimal ClosingBalance { get; set; }

        public string? Notes { get; set; }

        [JsonPropertyName("clientActionId")]
        public string? ClientActionId { get; set; }

        /// <summary>
        /// When true and ShiftRulesConfig.AllowForcedShiftClose is true, the close
        /// proceeds even if open orders would otherwise block it. The reason is
        /// captured into the close audit log.
        /// </summary>
        public bool Force { get; set; } = false;

        [MaxLength(500)]
        public string? ForceReason { get; set; }
    }

    /// <summary>
    /// Result returned when a close attempt is blocked by the validation engine.
    /// Carries per-status counts so the UI can render the spec'd popup.
    /// </summary>
    public class ShiftCloseValidationDto
    {
        public bool IsAllowed { get; set; }
        public List<ShiftCloseBlockerDto> Blockers { get; set; } = new();
        public bool ForceCloseAllowed { get; set; }
    }

    public class ShiftCloseBlockerDto
    {
        public string Code { get; set; } = string.Empty; // "PendingPayment" / "Preparing" / ...
        public int Count { get; set; }
    }

    public class CashierBalanceShiftDto
    {
        public Guid Id { get; set; }
        public Guid BranchId { get; set; }
        public Guid CashierId { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public Guid OpenedByManagerId { get; set; }
        public int? ShiftNumber { get; set; }
        public DateTime OpenedAt { get; set; }
        public decimal OpeningBalance { get; set; }
        public DateTime? ClosedAt { get; set; }
        public decimal? ClosingBalance { get; set; }
        public decimal? Variance { get; set; }
        public string? Classification { get; set; }
        public string? Notes { get; set; }
        public string? ClosingComment { get; set; }
        public bool IsClosed { get; set; }
        public bool IsOpen { get; set; }
        public int PaidOrderCount { get; set; }
        public decimal OrdersTotal { get; set; }
        public decimal CashOrdersTotal { get; set; }
        public decimal CardOrdersTotal { get; set; }
        public decimal ExpectedBalance { get; set; }
        public decimal RefundsTotal { get; set; }
        public decimal NetTotal { get; set; }
        public decimal FoodRevenue { get; set; }
        public decimal DeliveryCollected { get; set; }
        public decimal DeliveryCost { get; set; }
        public decimal DeliveryProfit { get; set; }
        public decimal MarketplaceFees { get; set; }
        public decimal MarketplaceServiceFees { get; set; }
        public decimal NetRestaurantRevenue { get; set; }

        // Documented shift expenses. CashExpensesTotal is already subtracted from
        // ExpectedBalance above — it is surfaced separately so the UI can show the
        // payout as a line in the cash formula instead of an unexplained gap.
        public int ExpenseCount { get; set; }
        public decimal ExpensesTotal { get; set; }
        public decimal CashExpensesTotal { get; set; }
        public decimal NonCashExpensesTotal { get; set; }

        public List<CashierShiftPartnerSalesDto> PartnerSalesBreakdown { get; set; } = new();
        public List<CashierShiftPaymentMethodBreakdownDto> PaymentMethodsBreakdown { get; set; } = new();
    }

    public class CashierShiftPartnerSalesDto
    {
        public Guid? DeliveryPartnerId { get; set; }
        public string PartnerName { get; set; } = string.Empty;
        public string? PartnerCode { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal FoodRevenue { get; set; }
        public decimal DeliveryCollected { get; set; }
        public decimal DeliveryCost { get; set; }
        public decimal DeliveryProfit { get; set; }
        public decimal MarketplaceFees { get; set; }
        public decimal MarketplaceServiceFees { get; set; }
        public decimal NetRevenue { get; set; }
    }

    public class CashierShiftPaymentMethodBreakdownDto
    {
        public string Method { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal Amount { get; set; }
        public bool IsCash { get; set; }
    }
}
