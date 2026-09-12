using RestaurantPos.Api.Modules.Payments.Domain.Enums;
using RestaurantPos.Api.Modules.Payments.Domain.Events;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using BaseEntity = RestaurantPos.Api.Models.BaseEntity;

namespace RestaurantPos.Api.Modules.Payments.Domain.Entities;

/// <summary>
/// Aggregate root for provider-neutral payment processing.
/// It stores local payment truth and prevents invalid status transitions before EF persistence.
/// </summary>
public sealed class Payment : BaseEntity
{
    private readonly List<PaymentAttempt> _attempts = [];
    private readonly List<PaymentSession> _sessions = [];
    private readonly List<PaymentEvent> _events = [];

    private Payment()
    {
        MerchantReference = null!;
        RequestedAmount = null!;
        AuthorizedAmount = null!;
        CapturedAmount = null!;
    }

    private Payment(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        MerchantReference merchantReference,
        Money requestedAmount,
        PaymentMethod method,
        string providerCode,
        string? idempotencyKeyHash,
        string? requestFingerprintHash,
        Guid? createdByUserId,
        DateTime createdAtUtc,
        DateTime? expiresAtUtc)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CreatedAt = createdAtUtc;
        UpdatedAt = createdAtUtc;
        BranchId = branchId;
        OrderId = orderId;
        MerchantReference = merchantReference;
        RequestedAmount = requestedAmount;
        AuthorizedAmount = Money.Zero(requestedAmount.Currency);
        CapturedAmount = Money.Zero(requestedAmount.Currency);
        RefundedAmount = Money.Zero(requestedAmount.Currency);
        Method = method;
        ProviderCode = NormalizeProviderCode(providerCode);
        Status = PaymentStatus.Pending;
        IdempotencyKeyHash = NormalizeOptionalHash(idempotencyKeyHash);
        RequestFingerprintHash = NormalizeOptionalHash(requestFingerprintHash);
        CreatedByUserId = createdByUserId;
        ExpiresAtUtc = expiresAtUtc;

        AddEvent(PaymentEventNames.PaymentCreated, null, null, createdAtUtc);
    }

    /// <summary>Branch that owns the payment for reporting and operational isolation.</summary>
    public Guid BranchId { get; private set; }

    /// <summary>Order paid by this aggregate. Kept as an id to avoid coupling the payment module to order internals.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Merchant-side reference used for idempotency, support, and gateway reconciliation.</summary>
    public MerchantReference MerchantReference { get; private set; }

    /// <summary>Provider configuration key. This is not a provider enum so new gateways do not change Domain code.</summary>
    public string ProviderCode { get; private set; } = string.Empty;

    /// <summary>Provider-neutral method category selected by the checkout flow.</summary>
    public PaymentMethod Method { get; private set; }

    /// <summary>Current aggregate lifecycle status.</summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>Original amount requested for payment.</summary>
    public Money RequestedAmount { get; private set; }

    /// <summary>Amount authorized but not collected. Phase 1 stores it for future providers.</summary>
    public Money AuthorizedAmount { get; private set; }

    /// <summary>Amount successfully collected.</summary>
    public Money CapturedAmount { get; private set; }

    /// <summary>Amount returned to the customer. Phase 1 stores it for future refund support only.</summary>
    public Money RefundedAmount { get; private set; } = null!;

    /// <summary>Hash of the client idempotency key; raw keys are never stored.</summary>
    public string? IdempotencyKeyHash { get; private set; }

    /// <summary>Hash of the payment request payload used to reject same-key/different-payload replays.</summary>
    public string? RequestFingerprintHash { get; private set; }

    /// <summary>Optional completion deadline used to expire abandoned payment flows.</summary>
    public DateTime? ExpiresAtUtc { get; private set; }

    /// <summary>UTC timestamp when the payment reached a terminal outcome.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>User who created the payment; null means system actor or unauthenticated customer flow.</summary>
    public Guid? CreatedByUserId { get; private set; }

    /// <summary>PostgreSQL optimistic concurrency token configured through EF.</summary>
    public uint RowVersion { get; private set; }

    /// <summary>Provider/local operation attempts. The backing field keeps mutation inside the aggregate.</summary>
    public IReadOnlyCollection<PaymentAttempt> Attempts => _attempts;

    /// <summary>Hosted checkout sessions owned by this payment. Only one session may be active at a time.</summary>
    public IReadOnlyCollection<PaymentSession> Sessions => _sessions;

    /// <summary>Append-only payment event stream for audit and reconciliation.</summary>
    public IReadOnlyCollection<PaymentEvent> Events => _events;

    /// <summary>Factory method that centralizes aggregate invariants at creation time.</summary>
    public static Payment Create(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        MerchantReference merchantReference,
        Money requestedAmount,
        PaymentMethod method,
        string providerCode,
        string? idempotencyKeyHash,
        string? requestFingerprintHash,
        Guid? createdByUserId,
        DateTime createdAtUtc,
        DateTime? expiresAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        }

        if (branchId == Guid.Empty)
        {
            throw new ArgumentException("Branch id is required.", nameof(branchId));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        ArgumentNullException.ThrowIfNull(merchantReference);
        ArgumentNullException.ThrowIfNull(requestedAmount);

        if (requestedAmount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedAmount), "Requested payment amount must be greater than zero.");
        }

        return new Payment(
            tenantId,
            branchId,
            orderId,
            merchantReference,
            requestedAmount,
            method,
            providerCode,
            idempotencyKeyHash,
            requestFingerprintHash,
            createdByUserId,
            createdAtUtc,
            expiresAtUtc);
    }

    /// <summary>Creates a single auditable attempt and rejects parallel active attempts.</summary>
    public PaymentAttempt AddAttempt(PaymentOperation operation, Money amount, DateTime startedAtUtc, string? requestHash)
    {
        EnsureNotTerminal();
        EnsureSameCurrency(amount);

        if (_attempts.Any(static attempt => attempt.IsActive))
        {
            throw new InvalidOperationException("A payment cannot have more than one active attempt.");
        }

        var attempt = PaymentAttempt.Create(
            TenantId,
            Id,
            _attempts.Count + 1,
            operation,
            amount,
            startedAtUtc,
            requestHash);

        _attempts.Add(attempt);
        AddEvent(PaymentEventNames.PaymentAttemptCreated, attempt.Id, null, startedAtUtc);
        return attempt;
    }

    /// <summary>Creates a hosted checkout session for future provider implementations.</summary>
    public PaymentSession CreateSession(
        GatewayReference? sessionReference,
        string? checkoutUrl,
        DateTime createdAtUtc,
        DateTime? expiresAtUtc)
    {
        EnsureNotTerminal();

        if (_sessions.Any(static session => session.IsActive))
        {
            throw new InvalidOperationException("A payment cannot have more than one active hosted checkout session.");
        }

        var session = PaymentSession.Create(
            TenantId,
            Id,
            ProviderCode,
            sessionReference,
            checkoutUrl,
            createdAtUtc,
            expiresAtUtc);

        _sessions.Add(session);
        AddEvent(PaymentEventNames.PaymentSessionCreated, null, sessionReference, createdAtUtc);
        return session;
    }

    /// <summary>Marks a local purchase flow as captured. Capture/refund/void orchestration is outside Phase 1.</summary>
    public void MarkPurchaseSucceeded(PaymentAttempt attempt, Money capturedAmount, GatewayReference? gatewayReference, DateTime completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        EnsureOwnedAttempt(attempt);
        EnsureSameCurrency(capturedAmount);
        EnsureTransitionTo(PaymentStatus.Captured);

        if (capturedAmount.Amount != RequestedAmount.Amount)
        {
            throw new InvalidOperationException("Phase 1 only supports full purchase success.");
        }

        attempt.MarkSucceeded(gatewayReference, completedAtUtc);
        CapturedAmount = capturedAmount;
        Status = PaymentStatus.Captured;
        CompletedAtUtc = completedAtUtc;
        AddEvent(PaymentEventNames.PaymentAttemptSucceeded, attempt.Id, gatewayReference, completedAtUtc);
        AddEvent(PaymentEventNames.PaymentSucceeded, attempt.Id, gatewayReference, completedAtUtc);
    }

    /// <summary>Marks the payment as failed without losing the attempt-level failure reason.</summary>
    public void MarkFailed(PaymentAttempt attempt, PaymentFailureReason reason, string? failureCode, string? failureMessage, DateTime failedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        EnsureOwnedAttempt(attempt);
        EnsureTransitionTo(PaymentStatus.Failed);

        attempt.MarkFailed(reason, failureCode, failureMessage, failedAtUtc);
        Status = PaymentStatus.Failed;
        CompletedAtUtc = failedAtUtc;
        AddEvent(PaymentEventNames.PaymentAttemptFailed, attempt.Id, null, failedAtUtc);
        AddEvent(PaymentEventNames.PaymentFailed, attempt.Id, null, failedAtUtc);
    }

    /// <summary>Cancels a pending local payment before funds are collected.</summary>
    public void Cancel(DateTime cancelledAtUtc)
    {
        EnsureTransitionTo(PaymentStatus.Cancelled);
        Status = PaymentStatus.Cancelled;
        CompletedAtUtc = cancelledAtUtc;
        AddEvent(PaymentEventNames.PaymentCancelled, null, null, cancelledAtUtc);
    }

    /// <summary>Expires an abandoned payment flow after its configured completion window.</summary>
    public void Expire(DateTime expiredAtUtc)
    {
        EnsureTransitionTo(PaymentStatus.Expired);
        Status = PaymentStatus.Expired;
        CompletedAtUtc = expiredAtUtc;
        AddEvent(PaymentEventNames.PaymentExpired, null, null, expiredAtUtc);
    }

    /// <summary>Moves the payment into a customer-action state for future hosted checkout flows.</summary>
    public void RequireAction(DateTime occurredAtUtc)
    {
        EnsureTransitionTo(PaymentStatus.RequiresAction);
        Status = PaymentStatus.RequiresAction;
        AddEvent(PaymentEventNames.PaymentActionRequired, null, null, occurredAtUtc);
    }

    /// <summary>Records that a hosted checkout attempt now requires customer action while keeping the aggregate consistent.</summary>
    public void RecordAttemptRequiresAction(
        PaymentAttempt attempt,
        GatewayReference gatewayReference,
        string? providerCorrelationId,
        DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(gatewayReference);
        EnsureOwnedAttempt(attempt);

        attempt.RequireAction(gatewayReference, providerCorrelationId);
        if (Status != PaymentStatus.RequiresAction)
        {
            EnsureTransitionTo(PaymentStatus.RequiresAction);
            Status = PaymentStatus.RequiresAction;
        }

        AddEvent(PaymentEventNames.PaymentActionRequired, attempt.Id, gatewayReference, occurredAtUtc);
    }

    /// <summary>Records a retryable failed attempt without making the whole payment terminal.</summary>
    public void RecordAttemptFailed(
        PaymentAttempt attempt,
        PaymentFailureReason reason,
        string? failureCode,
        string? failureMessage,
        DateTime failedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        EnsureOwnedAttempt(attempt);

        attempt.MarkFailed(reason, failureCode, failureMessage, failedAtUtc);
        AddEvent(PaymentEventNames.PaymentAttemptFailed, attempt.Id, null, failedAtUtc);
    }

    /// <summary>Records a retryable attempt timeout without making the whole payment terminal.</summary>
    public void RecordAttemptTimedOut(PaymentAttempt attempt, DateTime timedOutAtUtc, DateTime? retryAfterUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        EnsureOwnedAttempt(attempt);

        attempt.MarkTimedOut(timedOutAtUtc, retryAfterUtc);
        AddEvent(PaymentEventNames.PaymentAttemptFailed, attempt.Id, null, timedOutAtUtc);
    }

    /// <summary>Applies a verified provider success webhook to the aggregate, attempt, and hosted session.</summary>
    public void RecordWebhookPaymentSucceeded(
        PaymentAttempt attempt,
        PaymentSession session,
        Money capturedAmount,
        GatewayReference gatewayReference,
        string externalEventId,
        string payloadHash,
        DateTime completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(capturedAmount);
        ArgumentNullException.ThrowIfNull(gatewayReference);
        EnsureOwnedAttempt(attempt);
        EnsureOwnedSession(session);
        EnsureSameCurrency(capturedAmount);
        EnsureTransitionTo(PaymentStatus.Captured);

        if (capturedAmount.Amount != RequestedAmount.Amount)
        {
            throw new InvalidOperationException("Webhook purchase success must match the requested payment amount.");
        }

        attempt.MarkSucceeded(gatewayReference, completedAtUtc);
        session.Complete(completedAtUtc);
        CapturedAmount = capturedAmount;
        Status = PaymentStatus.Captured;
        CompletedAtUtc = completedAtUtc;
        AddEvent(PaymentEventNames.PaymentAttemptSucceeded, attempt.Id, gatewayReference, completedAtUtc);
        AddEvent(PaymentEventNames.PaymentSucceeded, attempt.Id, gatewayReference, completedAtUtc)
            .AttachExternalMetadata(externalEventId, payloadHash);
    }

    /// <summary>Applies a verified provider failed webhook to the aggregate, attempt, and hosted session.</summary>
    public void RecordWebhookPaymentFailed(
        PaymentAttempt attempt,
        PaymentSession session,
        PaymentFailureReason reason,
        string? failureCode,
        string? failureMessage,
        GatewayReference? gatewayReference,
        string externalEventId,
        string payloadHash,
        DateTime failedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(session);
        EnsureOwnedAttempt(attempt);
        EnsureOwnedSession(session);
        EnsureTransitionTo(PaymentStatus.Failed);

        attempt.MarkFailed(reason == PaymentFailureReason.None ? PaymentFailureReason.Unknown : reason, failureCode, failureMessage, failedAtUtc);
        session.Fail(failedAtUtc);
        Status = PaymentStatus.Failed;
        CompletedAtUtc = failedAtUtc;
        AddEvent(PaymentEventNames.PaymentAttemptFailed, attempt.Id, gatewayReference, failedAtUtc);
        AddEvent(PaymentEventNames.PaymentFailed, attempt.Id, gatewayReference, failedAtUtc)
            .AttachExternalMetadata(externalEventId, payloadHash);
    }

    /// <summary>Applies a verified provider expired webhook to the aggregate, attempt, and hosted session.</summary>
    public void RecordWebhookPaymentExpired(
        PaymentAttempt attempt,
        PaymentSession session,
        GatewayReference? gatewayReference,
        string externalEventId,
        string payloadHash,
        DateTime expiredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(session);
        EnsureOwnedAttempt(attempt);
        EnsureOwnedSession(session);
        EnsureTransitionTo(PaymentStatus.Expired);

        attempt.MarkTimedOut(expiredAtUtc, retryAfterUtc: null);
        session.Expire(expiredAtUtc);
        Status = PaymentStatus.Expired;
        CompletedAtUtc = expiredAtUtc;
        AddEvent(PaymentEventNames.PaymentAttemptFailed, attempt.Id, gatewayReference, expiredAtUtc);
        AddEvent(PaymentEventNames.PaymentExpired, attempt.Id, gatewayReference, expiredAtUtc)
            .AttachExternalMetadata(externalEventId, payloadHash);
    }

    /// <summary>Applies a verified provider cancellation webhook to the aggregate, attempt, and hosted session.</summary>
    public void RecordWebhookPaymentCancelled(
        PaymentAttempt attempt,
        PaymentSession session,
        GatewayReference? gatewayReference,
        string externalEventId,
        string payloadHash,
        DateTime cancelledAtUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(session);
        EnsureOwnedAttempt(attempt);
        EnsureOwnedSession(session);
        EnsureTransitionTo(PaymentStatus.Cancelled);

        attempt.Cancel(cancelledAtUtc);
        session.Cancel(cancelledAtUtc);
        Status = PaymentStatus.Cancelled;
        CompletedAtUtc = cancelledAtUtc;
        AddEvent(PaymentEventNames.PaymentCancelled, attempt.Id, gatewayReference, cancelledAtUtc)
            .AttachExternalMetadata(externalEventId, payloadHash);
    }

    private PaymentEvent AddEvent(string eventName, Guid? paymentAttemptId, GatewayReference? gatewayReference, DateTime occurredAtUtc)
    {
        var paymentEvent = PaymentEvent.Create(
            TenantId,
            Id,
            paymentAttemptId,
            ProviderCode,
            eventName,
            gatewayReference,
            occurredAtUtc);

        _events.Add(paymentEvent);
        return paymentEvent;
    }

    private void EnsureSameCurrency(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!string.Equals(RequestedAmount.Currency, amount.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Payment amounts must use the same currency.");
        }
    }

    private void EnsureOwnedAttempt(PaymentAttempt attempt)
    {
        if (attempt.PaymentId != Id)
        {
            throw new InvalidOperationException("Attempt does not belong to this payment.");
        }
    }

    private void EnsureOwnedSession(PaymentSession session)
    {
        if (session.PaymentId != Id)
        {
            throw new InvalidOperationException("Session does not belong to this payment.");
        }
    }

    private void EnsureNotTerminal()
    {
        if (IsTerminal(Status))
        {
            throw new InvalidOperationException("Terminal payments cannot be modified.");
        }
    }

    private void EnsureTransitionTo(PaymentStatus next)
    {
        if (!CanTransition(Status, next))
        {
            throw new InvalidOperationException($"Payment cannot transition from {Status} to {next}.");
        }
    }

    private static bool CanTransition(PaymentStatus current, PaymentStatus next)
        => current switch
        {
            PaymentStatus.Pending => next is PaymentStatus.RequiresAction or PaymentStatus.Authorized or PaymentStatus.Captured or PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Expired,
            PaymentStatus.RequiresAction => next is PaymentStatus.Authorized or PaymentStatus.Captured or PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Expired,
            PaymentStatus.Authorized => next is PaymentStatus.Captured or PaymentStatus.Voided or PaymentStatus.Expired,
            PaymentStatus.Captured => next is PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded,
            PaymentStatus.PartiallyRefunded => next is PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded,
            _ => false
        };

    private static bool IsTerminal(PaymentStatus status)
        => status is PaymentStatus.Refunded or PaymentStatus.Voided or PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Expired;

    private static string NormalizeProviderCode(string providerCode)
        => GatewayReference.NormalizeProviderCode(providerCode);

    private static string? NormalizeOptionalHash(string? hash)
        => string.IsNullOrWhiteSpace(hash) ? null : hash.Trim();
}
