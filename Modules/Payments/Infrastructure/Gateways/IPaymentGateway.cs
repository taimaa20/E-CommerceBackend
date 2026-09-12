using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;

/// <summary>
/// Provider-neutral payment gateway contract.
/// </summary>
public interface IPaymentGateway
{
    string ProviderCode { get; }

    Task<GatewayPaymentResult> CreatePaymentIntentionAsync(
        PaymentIntentionCreateRequest request,
        CancellationToken ct);
}

/// <summary>
/// Provider-neutral request to create a hosted payment intention.
/// </summary>
public sealed record PaymentIntentionCreateRequest
{
    public Guid PaymentId { get; init; }

    public Guid PaymentAttemptId { get; init; }

    public required MerchantReference MerchantReference { get; init; }

    public required Money Amount { get; init; }

    public required PaymentGatewayBillingData BillingData { get; init; }

    public PaymentGatewayCustomerData? Customer { get; init; }

    public IReadOnlyList<PaymentGatewayLineItem> Items { get; init; } = [];

    public IReadOnlyList<string> PaymentMethods { get; init; } = [];

    public string? NotificationUrl { get; init; }

    public string? RedirectionUrl { get; init; }

    public int? ExpirationSeconds { get; init; }

    public string? CorrelationId { get; init; }
}

/// <summary>
/// Billing data required by hosted payment providers.
/// </summary>
public sealed record PaymentGatewayBillingData
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public required string PhoneNumber { get; init; }

    public required string Country { get; init; }

    public string? City { get; init; }

    public string? State { get; init; }

    public string? PostalCode { get; init; }

    public string? Street { get; init; }

    public string? Building { get; init; }

    public string? Floor { get; init; }

    public string? Apartment { get; init; }
}

/// <summary>
/// Optional customer identity supplied to the gateway.
/// </summary>
public sealed record PaymentGatewayCustomerData
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }
}

/// <summary>
/// Payment intention line item. Amount is the line total in the payment currency.
/// </summary>
public sealed record PaymentGatewayLineItem
{
    public required string Name { get; init; }

    public required Money Amount { get; init; }

    public string? Description { get; init; }

    public int Quantity { get; init; } = 1;

    public string? ImageUrl { get; init; }
}

/// <summary>
/// Provider-neutral result returned after creating a hosted payment intention.
/// </summary>
public sealed record GatewayPaymentResult(
    GatewayReference GatewayReference,
    string CheckoutClientSecret,
    string? CheckoutUrl,
    long AmountMinorUnits,
    string Currency,
    string? Status,
    DateTimeOffset? CreatedAt,
    string CorrelationId,
    string? ProviderCorrelationId);
