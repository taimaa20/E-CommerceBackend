namespace RestaurantPos.Api.Modules.Payments.Domain.Enums;

/// <summary>
/// Normalized failure reasons that keep Application and Domain independent of provider errors.
/// Provider-specific codes are stored separately as sanitized diagnostic fields.
/// </summary>
public enum PaymentFailureReason
{
    /// <summary>No failure occurred.</summary>
    None = 0,

    /// <summary>The provider rejected authentication or credentials.</summary>
    AuthenticationFailed = 1,

    /// <summary>The request failed validation before processing.</summary>
    ValidationFailed = 2,

    /// <summary>The payment source had insufficient funds or equivalent decline.</summary>
    InsufficientFunds = 3,

    /// <summary>The provider or local operation timed out.</summary>
    Timeout = 4,

    /// <summary>The provider rate-limited the operation.</summary>
    RateLimited = 5,

    /// <summary>The provider was unavailable or returned a transient service failure.</summary>
    ProviderUnavailable = 6,

    /// <summary>The request or callback was a duplicate of already accepted work.</summary>
    Duplicate = 7,

    /// <summary>The provider or fraud controls rejected the operation as suspicious.</summary>
    FraudSuspected = 8,

    /// <summary>The failure could not be safely classified.</summary>
    Unknown = 9
}
