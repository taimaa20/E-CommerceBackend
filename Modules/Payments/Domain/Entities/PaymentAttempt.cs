using RestaurantPos.Api.Modules.Payments.Domain.Enums;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using BaseEntity = RestaurantPos.Api.Models.BaseEntity;

namespace RestaurantPos.Api.Modules.Payments.Domain.Entities;

/// <summary>
/// Represents one local or provider operation attempt for a payment.
/// Attempts are immutable in identity and amount so retries remain a reliable audit trail.
/// </summary>
public sealed class PaymentAttempt : BaseEntity
{
    private PaymentAttempt()
    {
        Amount = null!;
    }

    private PaymentAttempt(
        Guid tenantId,
        Guid paymentId,
        int attemptNumber,
        PaymentOperation operation,
        Money amount,
        DateTime startedAtUtc,
        string? requestHash)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CreatedAt = startedAtUtc;
        UpdatedAt = startedAtUtc;
        PaymentId = paymentId;
        AttemptNumber = attemptNumber;
        Operation = operation;
        Amount = amount;
        StartedAtUtc = startedAtUtc;
        RequestHash = NormalizeOptionalHash(requestHash);
        Status = PaymentAttemptStatus.Created;
        FailureReason = PaymentFailureReason.None;
    }

    /// <summary>Payment aggregate that owns this attempt.</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>Monotonic sequence number inside a payment aggregate.</summary>
    public int AttemptNumber { get; private set; }

    /// <summary>Provider-neutral operation represented by this attempt.</summary>
    public PaymentOperation Operation { get; private set; }

    /// <summary>Current lifecycle status for this attempt.</summary>
    public PaymentAttemptStatus Status { get; private set; }

    /// <summary>Amount attempted. It is fixed to make retries auditable.</summary>
    public Money Amount { get; private set; }

    /// <summary>Gateway reference returned by a provider after successful processing.</summary>
    public GatewayReference? GatewayReference { get; private set; }

    /// <summary>Provider correlation id used for support without exposing raw provider payloads.</summary>
    public string? ProviderCorrelationId { get; private set; }

    /// <summary>Normalized failure reason understood by Application and Domain.</summary>
    public PaymentFailureReason FailureReason { get; private set; }

    /// <summary>Sanitized provider/local failure code for diagnostics.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>Sanitized failure message. It must never contain card data, tokens, or secrets.</summary>
    public string? FailureMessage { get; private set; }

    /// <summary>UTC timestamp when the attempt was started.</summary>
    public DateTime StartedAtUtc { get; private set; }

    /// <summary>UTC timestamp when the attempt reached a terminal status.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Provider-directed retry time for transient failures.</summary>
    public DateTime? RetryAfterUtc { get; private set; }

    /// <summary>Hash of the outbound request, stored instead of raw sensitive payload.</summary>
    public string? RequestHash { get; private set; }

    /// <summary>Hash of the inbound response, stored instead of raw sensitive payload.</summary>
    public string? ResponseHash { get; private set; }

    /// <summary>PostgreSQL optimistic concurrency token configured through EF.</summary>
    public uint RowVersion { get; private set; }

    /// <summary>True while the attempt can still produce a gateway/local outcome.</summary>
    public bool IsActive => Status is PaymentAttemptStatus.Created or PaymentAttemptStatus.Processing or PaymentAttemptStatus.RequiresAction;

    /// <summary>Factory method used by the payment aggregate to keep attempt numbering consistent.</summary>
    internal static PaymentAttempt Create(
        Guid tenantId,
        Guid paymentId,
        int attemptNumber,
        PaymentOperation operation,
        Money amount,
        DateTime startedAtUtc,
        string? requestHash)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        }

        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment id is required.", nameof(paymentId));
        }

        if (attemptNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), "Attempt number must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(amount);
        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Attempt amount must be greater than zero.");
        }

        return new PaymentAttempt(tenantId, paymentId, attemptNumber, operation, amount, startedAtUtc, requestHash);
    }

    /// <summary>Marks the attempt as being processed by the local module or future gateway adapter.</summary>
    public void MarkProcessing()
    {
        EnsureTransitionTo(PaymentAttemptStatus.Processing);
        Status = PaymentAttemptStatus.Processing;
    }

    /// <summary>Marks the attempt as waiting for external customer action.</summary>
    public void RequireAction(GatewayReference gatewayReference, string? providerCorrelationId)
    {
        ArgumentNullException.ThrowIfNull(gatewayReference);

        EnsureTransitionTo(PaymentAttemptStatus.RequiresAction);
        GatewayReference = gatewayReference;
        ProviderCorrelationId = NormalizeOptional(providerCorrelationId);
        Status = PaymentAttemptStatus.RequiresAction;
    }

    /// <summary>Marks a successful attempt and captures the sanitized gateway identifiers.</summary>
    public void MarkSucceeded(GatewayReference? gatewayReference, DateTime completedAtUtc)
    {
        EnsureTransitionTo(PaymentAttemptStatus.Succeeded);
        GatewayReference = gatewayReference;
        Status = PaymentAttemptStatus.Succeeded;
        FailureReason = PaymentFailureReason.None;
        CompletedAtUtc = completedAtUtc;
    }

    /// <summary>Marks a failed attempt using provider-neutral failure classification.</summary>
    public void MarkFailed(PaymentFailureReason reason, string? failureCode, string? failureMessage, DateTime failedAtUtc)
    {
        if (reason == PaymentFailureReason.None)
        {
            throw new ArgumentException("A failed attempt requires a failure reason.", nameof(reason));
        }

        EnsureTransitionTo(PaymentAttemptStatus.Failed);
        Status = PaymentAttemptStatus.Failed;
        FailureReason = reason;
        FailureCode = NormalizeOptional(failureCode);
        FailureMessage = NormalizeOptional(failureMessage);
        CompletedAtUtc = failedAtUtc;
    }

    /// <summary>Marks a transient attempt failure with provider-directed retry timing.</summary>
    public void MarkTimedOut(DateTime timedOutAtUtc, DateTime? retryAfterUtc)
    {
        EnsureTransitionTo(PaymentAttemptStatus.TimedOut);
        Status = PaymentAttemptStatus.TimedOut;
        FailureReason = PaymentFailureReason.Timeout;
        CompletedAtUtc = timedOutAtUtc;
        RetryAfterUtc = retryAfterUtc;
    }

    /// <summary>Marks an attempt as duplicate without reprocessing the same work.</summary>
    public void MarkDuplicate(DateTime detectedAtUtc)
    {
        EnsureTransitionTo(PaymentAttemptStatus.Duplicate);
        Status = PaymentAttemptStatus.Duplicate;
        FailureReason = PaymentFailureReason.Duplicate;
        CompletedAtUtc = detectedAtUtc;
    }

    /// <summary>Marks an attempt as cancelled by the provider or local workflow.</summary>
    public void Cancel(DateTime cancelledAtUtc)
    {
        EnsureTransitionTo(PaymentAttemptStatus.Cancelled);
        Status = PaymentAttemptStatus.Cancelled;
        CompletedAtUtc = cancelledAtUtc;
    }

    private void EnsureTransitionTo(PaymentAttemptStatus next)
    {
        if (IsTerminal(Status))
        {
            throw new InvalidOperationException("Terminal payment attempts cannot be modified.");
        }

        if (Status == next)
        {
            return;
        }

        if (Status == PaymentAttemptStatus.Created)
        {
            return;
        }

        if (Status == PaymentAttemptStatus.Processing && next is PaymentAttemptStatus.RequiresAction or PaymentAttemptStatus.Succeeded or PaymentAttemptStatus.Failed or PaymentAttemptStatus.Cancelled or PaymentAttemptStatus.TimedOut or PaymentAttemptStatus.Duplicate)
        {
            return;
        }

        if (Status == PaymentAttemptStatus.RequiresAction && next is PaymentAttemptStatus.Succeeded or PaymentAttemptStatus.Failed or PaymentAttemptStatus.Cancelled or PaymentAttemptStatus.TimedOut)
        {
            return;
        }

        throw new InvalidOperationException($"Payment attempt cannot transition from {Status} to {next}.");
    }

    private static bool IsTerminal(PaymentAttemptStatus status)
        => status is PaymentAttemptStatus.Succeeded or PaymentAttemptStatus.Failed or PaymentAttemptStatus.Cancelled or PaymentAttemptStatus.TimedOut or PaymentAttemptStatus.Duplicate;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptionalHash(string? hash)
        => NormalizeOptional(hash);
}
