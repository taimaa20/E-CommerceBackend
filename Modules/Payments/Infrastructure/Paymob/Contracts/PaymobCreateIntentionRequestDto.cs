using System.Text.Json.Serialization;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob.Contracts;

public sealed record PaymobCreateIntentionRequestDto
{
    [JsonPropertyName("amount")]
    public required long Amount { get; init; }

    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    [JsonPropertyName("payment_methods")]
    public required IReadOnlyList<object> PaymentMethods { get; init; }

    [JsonPropertyName("items")]
    public required IReadOnlyList<PaymobIntentionItemDto> Items { get; init; }

    [JsonPropertyName("billing_data")]
    public required PaymobBillingDataDto BillingData { get; init; }

    [JsonPropertyName("customer")]
    public PaymobCustomerDto? Customer { get; init; }

    [JsonPropertyName("extras")]
    public IReadOnlyDictionary<string, string>? Extras { get; init; }

    [JsonPropertyName("special_reference")]
    public string? SpecialReference { get; init; }

    [JsonPropertyName("expiration")]
    public int? Expiration { get; init; }

    [JsonPropertyName("notification_url")]
    public string? NotificationUrl { get; init; }

    [JsonPropertyName("redirection_url")]
    public string? RedirectionUrl { get; init; }
}
