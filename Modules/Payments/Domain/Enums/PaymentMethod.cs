namespace RestaurantPos.Api.Modules.Payments.Domain.Enums;

/// <summary>
/// Provider-neutral payment method categories used by the payment aggregate.
/// This intentionally does not reference configured POS payment methods or gateway brands.
/// </summary>
public enum PaymentMethod
{
    /// <summary>The customer pays by card through a provider or terminal.</summary>
    Card = 0,

    /// <summary>The customer pays through a digital wallet.</summary>
    Wallet = 1,

    /// <summary>The payment is settled in cash inside the restaurant workflow.</summary>
    Cash = 2,

    /// <summary>The customer pays by bank transfer or equivalent account transfer.</summary>
    BankTransfer = 3,

    /// <summary>The payment method is provider-neutral but not represented by another value.</summary>
    Other = 4
}
