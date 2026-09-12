using RestaurantPos.Api.Modules.Payments.Domain.Enums;

namespace RestaurantPos.Api.Modules.Payments.Application.CreatePayment;

public sealed record CreatePaymentResult
{
    public Guid PaymentId { get; init; }

    public Guid SessionId { get; init; }

    public string MerchantReference { get; init; } = string.Empty;

    public string ProviderCode { get; init; } = string.Empty;

    public string GatewayReference { get; init; } = string.Empty;

    public string? CheckoutUrl { get; init; }

    public string? CheckoutClientSecret { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public PaymentStatus PaymentStatus { get; init; }

    public PaymentSessionStatus SessionStatus { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public bool IsDuplicate { get; init; }
}
