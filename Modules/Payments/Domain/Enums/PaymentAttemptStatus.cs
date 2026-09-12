namespace RestaurantPos.Api.Modules.Payments.Domain.Enums;

/// <summary>
/// Tracks the lifecycle of a single provider/local operation attempt.
/// Attempts are separate from payment status so retries remain auditable.
/// </summary>
public enum PaymentAttemptStatus
{
    /// <summary>The attempt row exists but work has not started.</summary>
    Created = 0,

    /// <summary>The attempt is currently being processed.</summary>
    Processing = 1,

    /// <summary>The attempt requires customer action outside the local process.</summary>
    RequiresAction = 2,

    /// <summary>The attempt completed successfully.</summary>
    Succeeded = 3,

    /// <summary>The attempt failed with a known or unknown business/gateway reason.</summary>
    Failed = 4,

    /// <summary>The local process cancelled this attempt before completion.</summary>
    Cancelled = 5,

    /// <summary>The attempt exceeded the configured response window.</summary>
    TimedOut = 6,

    /// <summary>The attempt was detected as a duplicate and was not processed again.</summary>
    Duplicate = 7
}
