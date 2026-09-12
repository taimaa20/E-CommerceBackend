using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using BaseEntity = RestaurantPos.Api.Models.BaseEntity;

namespace RestaurantPos.Api.Modules.Payments.Domain.Entities;

/// <summary>
/// Append-only audit event for payment lifecycle changes.
/// It is separate from MediatR domain events so financial audit survives process boundaries.
/// </summary>
public sealed class PaymentEvent : BaseEntity
{
    /// <summary>Maximum event name length kept small for index efficiency.</summary>
    public const int EventNameMaxLength = 80;

    private PaymentEvent()
    {
        ProviderCode = string.Empty;
        EventName = string.Empty;
    }

    private PaymentEvent(
        Guid tenantId,
        Guid paymentId,
        Guid? paymentAttemptId,
        string providerCode,
        string eventName,
        GatewayReference? gatewayReference,
        DateTime occurredAtUtc)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CreatedAt = occurredAtUtc;
        UpdatedAt = occurredAtUtc;
        PaymentId = paymentId;
        PaymentAttemptId = paymentAttemptId;
        ProviderCode = GatewayReference.NormalizeProviderCode(providerCode);
        EventName = NormalizeEventName(eventName);
        GatewayReference = gatewayReference;
        OccurredAtUtc = occurredAtUtc;
    }

    /// <summary>Payment aggregate that produced the event.</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>Attempt related to the event, when the event came from attempt processing.</summary>
    public Guid? PaymentAttemptId { get; private set; }

    /// <summary>Provider configuration key active when the event was recorded.</summary>
    public string ProviderCode { get; private set; }

    /// <summary>Internal event name from <see cref="Events.PaymentEventNames"/>.</summary>
    public string EventName { get; private set; }

    /// <summary>Gateway reference related to this event, if a provider produced one.</summary>
    public GatewayReference? GatewayReference { get; private set; }

    /// <summary>External provider event id used later for webhook duplicate protection.</summary>
    public string? ExternalEventId { get; private set; }

    /// <summary>Hash of an external payload; raw payload is intentionally not stored in Domain.</summary>
    public string? PayloadHash { get; private set; }

    /// <summary>UTC timestamp when the business event occurred.</summary>
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>PostgreSQL optimistic concurrency token configured through EF.</summary>
    public uint RowVersion { get; private set; }

    /// <summary>Creates an append-only payment audit event.</summary>
    internal static PaymentEvent Create(
        Guid tenantId,
        Guid paymentId,
        Guid? paymentAttemptId,
        string providerCode,
        string eventName,
        GatewayReference? gatewayReference,
        DateTime occurredAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        }

        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment id is required.", nameof(paymentId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(providerCode);

        return new PaymentEvent(tenantId, paymentId, paymentAttemptId, providerCode.Trim(), eventName, gatewayReference, occurredAtUtc);
    }

    /// <summary>Attaches sanitized external webhook metadata after signature verification.</summary>
    public void AttachExternalMetadata(string? externalEventId, string? payloadHash)
    {
        ExternalEventId = NormalizeOptional(externalEventId);
        PayloadHash = NormalizeOptional(payloadHash);
    }

    private static string NormalizeEventName(string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var normalized = eventName.Trim();
        if (normalized.Length > EventNameMaxLength)
        {
            throw new ArgumentException($"Event name cannot exceed {EventNameMaxLength} characters.", nameof(eventName));
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
