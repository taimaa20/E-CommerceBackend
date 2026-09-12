using RestaurantPos.Api.Modules.Payments.Domain.Enums;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using BaseEntity = RestaurantPos.Api.Models.BaseEntity;

namespace RestaurantPos.Api.Modules.Payments.Domain.Entities;

/// <summary>
/// Provider-neutral hosted checkout session for payment flows that redirect the customer outside the POS.
/// It is modeled now to avoid later refactoring when the first hosted provider is added.
/// </summary>
public sealed class PaymentSession : BaseEntity
{
    /// <summary>Maximum URL length kept generous for hosted checkout URLs while still bounded for storage.</summary>
    public const int CheckoutUrlMaxLength = 2048;

    private PaymentSession()
    {
        ProviderCode = string.Empty;
    }

    private PaymentSession(
        Guid tenantId,
        Guid paymentId,
        string providerCode,
        GatewayReference? sessionReference,
        string? checkoutUrl,
        DateTime createdAtUtc,
        DateTime? expiresAtUtc)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CreatedAt = createdAtUtc;
        UpdatedAt = createdAtUtc;
        PaymentId = paymentId;
        ProviderCode = GatewayReference.NormalizeProviderCode(providerCode);
        SessionReference = sessionReference;
        CheckoutUrl = NormalizeCheckoutUrl(checkoutUrl);
        Status = PaymentSessionStatus.Created;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Payment aggregate that owns the hosted session.</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>Provider configuration key. It is text, not an enum, so new providers do not require domain changes.</summary>
    public string ProviderCode { get; private set; }

    /// <summary>External hosted checkout/session reference returned by a provider.</summary>
    public GatewayReference? SessionReference { get; private set; }

    /// <summary>Public hosted checkout URL. Secrets and tokens must never be logged from this value.</summary>
    public string? CheckoutUrl { get; private set; }

    /// <summary>Current session lifecycle status.</summary>
    public PaymentSessionStatus Status { get; private set; }

    /// <summary>UTC timestamp when the hosted session was created locally.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>UTC timestamp when the hosted provider session expires.</summary>
    public DateTime? ExpiresAtUtc { get; private set; }

    /// <summary>UTC timestamp when the hosted session reached a terminal state.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>PostgreSQL optimistic concurrency token configured through EF.</summary>
    public uint RowVersion { get; private set; }

    /// <summary>True while the session can still produce a payment outcome.</summary>
    public bool IsActive => Status is PaymentSessionStatus.Created or PaymentSessionStatus.Pending or PaymentSessionStatus.RequiresAction;

    /// <summary>Creates a hosted payment session owned by the payment aggregate.</summary>
    internal static PaymentSession Create(
        Guid tenantId,
        Guid paymentId,
        string providerCode,
        GatewayReference? sessionReference,
        string? checkoutUrl,
        DateTime createdAtUtc,
        DateTime? expiresAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        }

        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment id is required.", nameof(paymentId));
        }

        return new PaymentSession(tenantId, paymentId, providerCode, sessionReference, checkoutUrl, createdAtUtc, expiresAtUtc);
    }

    /// <summary>Marks the session as waiting on the customer or provider.</summary>
    public void MarkPending()
    {
        EnsureTransitionTo(PaymentSessionStatus.Pending);
        Status = PaymentSessionStatus.Pending;
    }

    /// <summary>Marks the session as requiring external customer action.</summary>
    public void RequireAction()
    {
        EnsureTransitionTo(PaymentSessionStatus.RequiresAction);
        Status = PaymentSessionStatus.RequiresAction;
    }

    /// <summary>Marks the hosted checkout session as completed.</summary>
    public void Complete(DateTime completedAtUtc)
    {
        EnsureTransitionTo(PaymentSessionStatus.Completed);
        Status = PaymentSessionStatus.Completed;
        CompletedAtUtc = completedAtUtc;
    }

    /// <summary>Marks the hosted checkout session as failed.</summary>
    public void Fail(DateTime failedAtUtc)
    {
        EnsureTransitionTo(PaymentSessionStatus.Failed);
        Status = PaymentSessionStatus.Failed;
        CompletedAtUtc = failedAtUtc;
    }

    /// <summary>Cancels the hosted checkout session locally before completion.</summary>
    public void Cancel(DateTime cancelledAtUtc)
    {
        EnsureTransitionTo(PaymentSessionStatus.Cancelled);
        Status = PaymentSessionStatus.Cancelled;
        CompletedAtUtc = cancelledAtUtc;
    }

    /// <summary>Expires the hosted checkout session after its provider deadline.</summary>
    public void Expire(DateTime expiredAtUtc)
    {
        EnsureTransitionTo(PaymentSessionStatus.Expired);
        Status = PaymentSessionStatus.Expired;
        CompletedAtUtc = expiredAtUtc;
    }

    private void EnsureTransitionTo(PaymentSessionStatus next)
    {
        if (IsTerminal(Status))
        {
            throw new InvalidOperationException("Terminal payment sessions cannot be modified.");
        }

        if (Status == next)
        {
            return;
        }

        if (Status == PaymentSessionStatus.Created)
        {
            return;
        }

        if (Status is PaymentSessionStatus.Pending or PaymentSessionStatus.RequiresAction
            && next is PaymentSessionStatus.Completed or PaymentSessionStatus.Failed or PaymentSessionStatus.Cancelled or PaymentSessionStatus.Expired)
        {
            return;
        }

        throw new InvalidOperationException($"Payment session cannot transition from {Status} to {next}.");
    }

    private static bool IsTerminal(PaymentSessionStatus status)
        => status is PaymentSessionStatus.Completed or PaymentSessionStatus.Failed or PaymentSessionStatus.Cancelled or PaymentSessionStatus.Expired;

    private static string? NormalizeCheckoutUrl(string? checkoutUrl)
    {
        if (string.IsNullOrWhiteSpace(checkoutUrl))
        {
            return null;
        }

        var normalized = checkoutUrl.Trim();
        if (normalized.Length > CheckoutUrlMaxLength)
        {
            throw new ArgumentException($"Checkout URL cannot exceed {CheckoutUrlMaxLength} characters.", nameof(checkoutUrl));
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Checkout URL must be an absolute HTTPS URL.", nameof(checkoutUrl));
        }

        return normalized;
    }
}
