namespace RestaurantPos.Api.Modules.Payments.Domain.Enums;

/// <summary>
/// Represents the aggregate-level lifecycle for a payment.
/// Values are append-only because persisted enum ordinals can exist in historical rows.
/// </summary>
public enum PaymentStatus
{
    /// <summary>The payment was created locally but has not reached a terminal outcome.</summary>
    Pending = 0,

    /// <summary>The gateway requires an external customer action before completion.</summary>
    RequiresAction = 1,

    /// <summary>Funds were authorized but not yet captured.</summary>
    Authorized = 2,

    /// <summary>Funds were collected successfully.</summary>
    Captured = 3,

    /// <summary>Some, but not all, collected funds were returned to the customer.</summary>
    PartiallyRefunded = 4,

    /// <summary>All collected funds were returned to the customer.</summary>
    Refunded = 5,

    /// <summary>A previous authorization was released before collection.</summary>
    Voided = 6,

    /// <summary>The payment failed before funds were collected.</summary>
    Failed = 7,

    /// <summary>The local business flow cancelled the payment before completion.</summary>
    Cancelled = 8,

    /// <summary>The payment passed its allowed completion window.</summary>
    Expired = 9
}
