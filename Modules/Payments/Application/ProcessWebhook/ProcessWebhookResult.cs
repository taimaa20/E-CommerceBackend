namespace RestaurantPos.Api.Modules.Payments.Application.ProcessWebhook;

public sealed record ProcessWebhookResult
{
    public int StatusCode { get; init; }

    public string Status { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string? Reason { get; init; }

    public Guid? PaymentId { get; init; }

    public bool IsDuplicate { get; init; }

    public static ProcessWebhookResult Processed(string correlationId, Guid paymentId)
        => new()
        {
            StatusCode = StatusCodes.Status200OK,
            Status = "processed",
            CorrelationId = correlationId,
            PaymentId = paymentId
        };

    public static ProcessWebhookResult Duplicate(string correlationId, Guid? paymentId, string reason)
        => new()
        {
            StatusCode = StatusCodes.Status200OK,
            Status = "duplicate",
            CorrelationId = correlationId,
            Reason = reason,
            PaymentId = paymentId,
            IsDuplicate = true
        };

    public static ProcessWebhookResult Rejected(int statusCode, string correlationId, string reason)
        => new()
        {
            StatusCode = statusCode,
            Status = "rejected",
            CorrelationId = correlationId,
            Reason = reason
        };
}
