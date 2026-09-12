namespace RestaurantPos.Api.Modules.Payments.Domain.Enums;

/// <summary>
/// Lifecycle for a hosted checkout/session object created by a provider-neutral payment flow.
/// The enum exists now so Paymob, Stripe, and other hosted checkout providers can be added without reshaping the domain.
/// </summary>
public enum PaymentSessionStatus
{
    /// <summary>The local session record has been created.</summary>
    Created = 0,

    /// <summary>The session is waiting for the customer or provider to complete the flow.</summary>
    Pending = 1,

    /// <summary>The customer must complete an external action such as hosted checkout or 3DS.</summary>
    RequiresAction = 2,

    /// <summary>The session completed successfully.</summary>
    Completed = 3,

    /// <summary>The session failed before producing a successful payment outcome.</summary>
    Failed = 4,

    /// <summary>The local workflow cancelled the session before completion.</summary>
    Cancelled = 5,

    /// <summary>The hosted session expired before completion.</summary>
    Expired = 6
}
