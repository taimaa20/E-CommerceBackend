using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public enum ExpenseInvoiceDuplicateDisposition
    {
        Save = 0,
        RequireAcknowledgement = 1,
        Block = 2,
        SaveWithAcknowledgementAudit = 3
    }

    public static class ExpenseInvoiceDuplicatePolicy
    {
        public static ExpenseInvoiceDuplicateDisposition Evaluate(
            DuplicateInvoiceBehavior behavior,
            bool duplicateExists,
            bool acknowledged)
        {
            if (!duplicateExists || behavior == DuplicateInvoiceBehavior.Disabled)
                return ExpenseInvoiceDuplicateDisposition.Save;

            if (behavior == DuplicateInvoiceBehavior.BlockSave)
                return ExpenseInvoiceDuplicateDisposition.Block;

            return acknowledged
                ? ExpenseInvoiceDuplicateDisposition.SaveWithAcknowledgementAudit
                : ExpenseInvoiceDuplicateDisposition.RequireAcknowledgement;
        }
    }
}
