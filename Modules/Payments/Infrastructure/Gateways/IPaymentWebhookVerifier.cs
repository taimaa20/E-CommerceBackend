namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;

public interface IPaymentWebhookVerifier
{
    string ProviderCode { get; }

    Task<PaymentWebhookVerificationResult> VerifyAsync(
        PaymentWebhookVerificationRequest request,
        CancellationToken ct);
}

public sealed record PaymentWebhookVerificationRequest
{
    public required string RawBody { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Query { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public required string CorrelationId { get; init; }

    public string? RemoteIpAddress { get; init; }
}

public sealed record PaymentWebhookVerificationResult
{
    private PaymentWebhookVerificationResult(
        bool isValid,
        PaymentWebhookRejectionReason reason,
        string message)
    {
        IsValid = isValid;
        Reason = reason;
        Message = message;
    }

    public bool IsValid { get; }

    public PaymentWebhookRejectionReason Reason { get; }

    public string Message { get; }

    public static PaymentWebhookVerificationResult Valid()
        => new(true, PaymentWebhookRejectionReason.None, string.Empty);

    public static PaymentWebhookVerificationResult Invalid(
        PaymentWebhookRejectionReason reason,
        string message)
        => new(false, reason, message);
}

public enum PaymentWebhookRejectionReason
{
    None = 0,
    ProviderDisabled = 1,
    ProviderMisconfigured = 2,
    PayloadInvalid = 3,
    SignatureMissing = 4,
    SignatureInvalid = 5,
    ReplayWindowExceeded = 6
}
