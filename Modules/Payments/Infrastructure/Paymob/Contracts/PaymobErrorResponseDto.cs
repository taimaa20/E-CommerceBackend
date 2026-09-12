using System.Text.Json;
using System.Text.Json.Serialization;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob.Contracts;

public sealed record PaymobErrorResponseDto
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
