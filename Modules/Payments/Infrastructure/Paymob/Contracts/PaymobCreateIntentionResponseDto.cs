using System.Text.Json;
using System.Text.Json.Serialization;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob.Contracts;

public sealed record PaymobCreateIntentionResponseDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("client_secret")]
    public string? ClientSecret { get; init; }

    [JsonPropertyName("intention_order_id")]
    public long? IntentionOrderId { get; init; }

    [JsonPropertyName("intention_detail")]
    public PaymobIntentionDetailDto? IntentionDetail { get; init; }

    [JsonPropertyName("amount")]
    public long Amount { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("created")]
    public DateTimeOffset? Created { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
