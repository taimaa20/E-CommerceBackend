using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers
{
    /// <summary>
    /// Single source of truth for how a cashier shift expense behaves financially.
    /// Every read path (shift metrics, Z-report) and the write path resolve cash
    /// impact through here, so the drawer arithmetic can never drift between the
    /// screen the cashier sees and the number the close validates against.
    /// </summary>
    public static class ShiftExpenseRules
    {
        /// <summary>Prefix for the system-generated reference used when the cashier
        /// has no supplier invoice number to type in.</summary>
        public const string GeneratedReferencePrefix = "EXP";

        /// <summary>Shift expenses are settled the moment they are recorded — the
        /// money has already left the drawer (or the card account).</summary>
        public const ExpenseInvoiceStatus RecordedStatus = ExpenseInvoiceStatus.Paid;

        /// <summary>
        /// Maps the configured payment method's cash-ness onto the stable reporting
        /// enum. Non-cash settles as <see cref="ExpensePaymentMethod.Other"/> — the
        /// real provider name lives in PaymentMethodDetail + PaymentMethodId, which
        /// keeps this enum free of per-provider churn.
        /// </summary>
        public static ExpensePaymentMethod ResolvePaymentMethod(bool isCash)
            => isCash ? ExpensePaymentMethod.Cash : ExpensePaymentMethod.Other;

        /// <summary>Only cash settlements physically leave the cashier's drawer.</summary>
        public static bool ReducesDrawerCash(ExpensePaymentMethod method)
            => method == ExpensePaymentMethod.Cash;

        /// <summary>A cancelled expense is retained for audit but stops counting.</summary>
        public static bool CountsTowardTotals(ExpenseInvoiceStatus status)
            => status != ExpenseInvoiceStatus.Cancelled;

        /// <summary>
        /// Builds a unique, human-readable reference for expenses recorded without
        /// a supplier document. Deterministic prefix + UTC stamp keeps it sortable
        /// and well inside the 64-char InvoiceNumber column.
        /// </summary>
        public static string BuildGeneratedReference(DateTime occurredAtUtc)
            => $"{GeneratedReferencePrefix}-{occurredAtUtc:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..28];
    }
}
