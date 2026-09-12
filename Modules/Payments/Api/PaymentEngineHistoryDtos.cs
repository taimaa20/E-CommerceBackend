using RestaurantPos.Api.Modules.Payments.Domain.Enums;

namespace RestaurantPos.Api.Modules.Payments.Api;

public sealed record PaymentEnginePaymentDetailsDto
{
    public Guid PaymentId { get; init; }

    public Guid OrderId { get; init; }

    public Guid BranchId { get; init; }

    public string MerchantReference { get; init; } = string.Empty;

    public string ProviderCode { get; init; } = string.Empty;

    public string? GatewayReference { get; init; }

    public PaymentStatus Status { get; init; }

    public PaymentMethod Method { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public decimal CapturedAmount { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public string? ActiveCheckoutUrl { get; init; }

    public IReadOnlyList<PaymentEngineAttemptDto> Attempts { get; init; } = [];

    public IReadOnlyList<PaymentEngineSessionDto> Sessions { get; init; } = [];
}

public sealed record PaymentEngineOrderPaymentDto
{
    public Guid OrderId { get; init; }

    public IReadOnlyList<PaymentEnginePaymentDetailsDto> Payments { get; init; } = [];

    public PaymentEnginePaymentDetailsDto? CurrentPayment { get; init; }
}

public sealed record PaymentEngineAttemptDto
{
    public Guid AttemptId { get; init; }

    public int AttemptNumber { get; init; }

    public PaymentOperation Operation { get; init; }

    public PaymentAttemptStatus Status { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public string? GatewayProvider { get; init; }

    public string? GatewayReference { get; init; }

    public string? FailureCode { get; init; }

    public PaymentFailureReason FailureReason { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public DateTime StartedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }
}

public sealed record PaymentEngineSessionDto
{
    public Guid SessionId { get; init; }

    public PaymentSessionStatus Status { get; init; }

    public string ProviderCode { get; init; } = string.Empty;

    public string? GatewayProvider { get; init; }

    public string? GatewayReference { get; init; }

    public string? CheckoutUrl { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }
}
