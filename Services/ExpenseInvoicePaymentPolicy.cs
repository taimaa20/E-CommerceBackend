using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services;

public static class ExpenseInvoicePaymentPolicy
{
    public static decimal ResolvePaidAmount(
        decimal totalAmount,
        decimal? paidAmount,
        ExpenseInvoiceStatus requestedStatus)
    {
        var resolved = paidAmount
            ?? (requestedStatus == ExpenseInvoiceStatus.Paid ? totalAmount : 0m);

        if (resolved < 0)
            throw new ValidationException("Paid amount must be zero or greater.");

        if (resolved > totalAmount)
            throw new ValidationException("Paid amount cannot exceed the invoice total.");

        return decimal.Round(resolved, 2, MidpointRounding.AwayFromZero);
    }

    public static ExpenseInvoiceStatus ResolveStatus(
        decimal totalAmount,
        decimal paidAmount,
        ExpenseInvoiceStatus requestedStatus)
    {
        if (requestedStatus is ExpenseInvoiceStatus.Draft or ExpenseInvoiceStatus.Cancelled)
            return requestedStatus;

        if (paidAmount <= 0)
            return ExpenseInvoiceStatus.Unpaid;

        return paidAmount >= totalAmount
            ? ExpenseInvoiceStatus.Paid
            : ExpenseInvoiceStatus.PartiallyPaid;
    }

    public static decimal Balance(decimal totalAmount, decimal paidAmount)
        => Math.Max(0m, decimal.Round(totalAmount - paidAmount, 2, MidpointRounding.AwayFromZero));
}
