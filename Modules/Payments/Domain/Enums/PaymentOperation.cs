namespace RestaurantPos.Api.Modules.Payments.Domain.Enums;

/// <summary>
/// Describes the operation attempted against a payment.
/// Phase 1 only creates the local foundation; later providers can support more operations.
/// </summary>
public enum PaymentOperation
{
    /// <summary>Authorize and collect funds as one operation.</summary>
    Purchase = 0,

    /// <summary>Reserve funds without collecting them.</summary>
    Authorize = 1,

    /// <summary>Collect funds from a previous authorization.</summary>
    Capture = 2,

    /// <summary>Return collected funds to the customer.</summary>
    Refund = 3,

    /// <summary>Release a previous authorization before capture.</summary>
    Void = 4
}
