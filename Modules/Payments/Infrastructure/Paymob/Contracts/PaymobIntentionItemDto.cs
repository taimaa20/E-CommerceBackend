using System.Text.Json.Serialization;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob.Contracts;

public sealed record PaymobIntentionItemDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("amount")]
    public required long Amount { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; } = 1;

    [JsonPropertyName("image")]
    public string? Image { get; init; }
}
