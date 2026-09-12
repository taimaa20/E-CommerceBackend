using System.Text.Json;
using System.Text.Json.Serialization;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob.Contracts;

public sealed record PaymobWebhookEnvelopeDto
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("obj")]
    public PaymobWebhookTransactionDto? Object { get; init; }

    [JsonPropertyName("transaction")]
    public PaymobWebhookTransactionDto? Transaction { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }

    public PaymobWebhookTransactionDto? GetTransaction()
        => Object ?? Transaction;
}

public sealed record PaymobWebhookTransactionDto
{
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("amount_cents")]
    public long? AmountCents { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("success")]
    public bool? Success { get; init; }

    [JsonPropertyName("pending")]
    public bool? Pending { get; init; }

    [JsonPropertyName("error_occured")]
    public bool? ErrorOccured { get; init; }

    [JsonPropertyName("is_voided")]
    public bool? IsVoided { get; init; }

    [JsonPropertyName("is_refunded")]
    public bool? IsRefunded { get; init; }

    [JsonPropertyName("integration_id")]
    public long? IntegrationId { get; init; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("order")]
    public PaymobWebhookOrderDto? Order { get; init; }

    [JsonPropertyName("source_data")]
    public PaymobWebhookSourceDataDto? SourceData { get; init; }

    [JsonPropertyName("extras")]
    public IDictionary<string, JsonElement>? Extras { get; init; }

    [JsonPropertyName("extra")]
    public IDictionary<string, JsonElement>? Extra { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record PaymobWebhookOrderDto
{
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("merchant_order_id")]
    public string? MerchantOrderId { get; init; }

    [JsonPropertyName("extras")]
    public IDictionary<string, JsonElement>? Extras { get; init; }

    [JsonPropertyName("extra")]
    public IDictionary<string, JsonElement>? Extra { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record PaymobWebhookSourceDataDto
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("sub_type")]
    public string? SubType { get; init; }

    [JsonPropertyName("pan")]
    public string? Pan { get; init; }
}
