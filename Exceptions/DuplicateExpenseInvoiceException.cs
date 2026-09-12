using RestaurantPos.Api.DTOs.ExpenseInvoice;

namespace RestaurantPos.Api.Exceptions
{
    public sealed class DuplicateExpenseInvoiceException : ConflictException
    {
        public ExpenseInvoiceDuplicateWarningDto Warning { get; }

        public DuplicateExpenseInvoiceException(ExpenseInvoiceDuplicateWarningDto warning)
            : base(warning.CanOverride
                ? "A similar invoice already exists for this supplier and date."
                : "A similar invoice already exists and duplicate invoices are blocked.")
        {
            Warning = warning;
        }
    }
}
