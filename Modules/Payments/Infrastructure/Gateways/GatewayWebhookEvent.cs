using RestaurantPos.Api.Modules.Payments.Domain.Enums;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;

public sealed record GatewayWebhookEvent
{
    public required string ProviderCode { get; init; }

    public required GatewayWebhookEventType EventType { get; init; }

    public required string ExternalEventId { get; init; }

    public required string PayloadHash { get; init; }

    public Guid? PaymentId { get; init; }

    public Guid? PaymentAttemptId { get; init; }

    public Guid? PaymentSessionId { get; init; }

    public string? MerchantReference { get; init; }

    public GatewayReference? GatewayReference { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public DateTime OccurredAtUtc { get; init; }

    public PaymentFailureReason FailureReason { get; init; } = PaymentFailureReason.None;

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    public string? ProviderStatus { get; init; }

    public string? ProviderCorrelationId { get; init; }

    public string CorrelationId { get; init; } = string.Empty;
}

public enum GatewayWebhookEventType
{
    PaymentSucceeded = 0,
    PaymentFailed = 1,
    PaymentExpired = 2,
    PaymentCancelled = 3
}

public sealed class PaymentWebhookMappingException : Exception
{
    public PaymentWebhookMappingException(string message)
        : base(message)
    {
    }
}
