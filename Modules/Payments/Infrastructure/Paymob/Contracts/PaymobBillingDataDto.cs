using System.Text.Json.Serialization;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob.Contracts;

public sealed record PaymobBillingDataDto
{
    [JsonPropertyName("first_name")]
    public required string FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public required string LastName { get; init; }

    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [JsonPropertyName("phone_number")]
    public required string PhoneNumber { get; init; }

    [JsonPropertyName("country")]
    public required string Country { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; init; }

    [JsonPropertyName("street")]
    public string? Street { get; init; }

    [JsonPropertyName("building")]
    public string? Building { get; init; }

    [JsonPropertyName("floor")]
    public string? Floor { get; init; }

    [JsonPropertyName("apartment")]
    public string? Apartment { get; init; }
}
