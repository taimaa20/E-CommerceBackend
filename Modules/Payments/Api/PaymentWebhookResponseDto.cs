namespace RestaurantPos.Api.Modules.Payments.Api;

public sealed class PaymentWebhookResponseDto
{
    public string Status { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string? Reason { get; init; }

    public Guid? PaymentId { get; init; }

    public bool IsDuplicate { get; init; }
}
