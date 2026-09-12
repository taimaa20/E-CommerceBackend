using System.Text.Json.Serialization;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob.Contracts;

public sealed record PaymobIntentionDetailDto
{
    [JsonPropertyName("amount")]
    public long Amount { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}
