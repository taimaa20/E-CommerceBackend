using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs.ExpenseInvoice
{
    /// <summary>
    /// Cashier-facing create contract. Branch, cashier, shift and timestamp are
    /// never client-supplied — they are resolved from the authenticated context.
    /// </summary>
    public class CreateShiftExpenseDto
    {
        [Required]
        public Guid ExpenseCategoryId { get; set; }

        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }

        /// <summary>Configured payment method. Omitted means cash — matching the
        /// POS-wide convention that an unspecified method is a cash settlement.</summary>
        public Guid? PaymentMethodId { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>Supplier / receipt reference. Auto-generated when blank.</summary>
        [MaxLength(64)]
        public string? ReferenceNumber { get; set; }
    }

    public class CancelShiftExpenseDto
    {
        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class ShiftExpenseDto
    {
        public Guid Id { get; set; }
        public Guid? CashierShiftId { get; set; }
        public Guid BranchId { get; set; }
        public Guid? ExpenseCategoryId { get; set; }
        public string? CategoryLabel { get; set; }
        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public Guid? PaymentMethodId { get; set; }
        public string? PaymentMethodName { get; set; }
        /// <summary>True when this expense reduces the physical drawer cash.</summary>
        public bool IsCash { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsCancelled { get; set; }
        public Guid? CreatedById { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelledByName { get; set; }
        public string? CancellationReason { get; set; }
    }

    /// <summary>Aggregates for the shift breakdown / Z-report expense block.</summary>
    public class ShiftExpenseSummaryDto
    {
        public int ExpenseCount { get; set; }
        public decimal ExpensesTotal { get; set; }
        public decimal CashExpensesTotal { get; set; }
        public decimal NonCashExpensesTotal { get; set; }
        public List<ShiftExpenseCategoryBreakdownDto> ByCategory { get; set; } = new();
    }

    public class ShiftExpenseCategoryBreakdownDto
    {
        public Guid? ExpenseCategoryId { get; set; }
        public string CategoryLabel { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Total { get; set; }
        public decimal CashTotal { get; set; }
    }

    /// <summary>Response for the cashier's "expenses this shift" panel.</summary>
    public class ShiftExpenseListDto
    {
        public Guid ShiftId { get; set; }
        public ShiftExpenseSummaryDto Summary { get; set; } = new();
        public List<ShiftExpenseDto> Expenses { get; set; } = new();
    }
}
