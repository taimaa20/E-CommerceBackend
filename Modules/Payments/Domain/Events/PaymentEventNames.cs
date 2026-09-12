namespace RestaurantPos.Api.Modules.Payments.Domain.Events;

/// <summary>
/// Internal event names persisted to the payment event stream.
/// Constants avoid magic strings while keeping the event table extensible.
/// </summary>
public static class PaymentEventNames
{
    /// <summary>A payment aggregate was created.</summary>
    public const string PaymentCreated = "PaymentCreated";

    /// <summary>A payment attempt was created.</summary>
    public const string PaymentAttemptCreated = "PaymentAttemptCreated";

    /// <summary>A payment attempt succeeded.</summary>
    public const string PaymentAttemptSucceeded = "PaymentAttemptSucceeded";

    /// <summary>A payment attempt failed.</summary>
    public const string PaymentAttemptFailed = "PaymentAttemptFailed";

    /// <summary>A hosted payment session was created.</summary>
    public const string PaymentSessionCreated = "PaymentSessionCreated";

    /// <summary>The payment succeeded and collected funds.</summary>
    public const string PaymentSucceeded = "PaymentSucceeded";

    /// <summary>The payment requires customer action outside the local process.</summary>
    public const string PaymentActionRequired = "PaymentActionRequired";

    /// <summary>The payment failed before collection.</summary>
    public const string PaymentFailed = "PaymentFailed";

    /// <summary>The local workflow cancelled the payment.</summary>
    public const string PaymentCancelled = "PaymentCancelled";

    /// <summary>The payment expired before completion.</summary>
    public const string PaymentExpired = "PaymentExpired";
}
