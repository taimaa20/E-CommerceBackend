using System.Globalization;

namespace RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;

/// <summary>
/// Provider-neutral reference returned by an external gateway.
/// Provider code is plain text instead of an enum so new providers can be added by configuration.
/// </summary>
public sealed record GatewayReference
{
    /// <summary>Maximum provider code length aligned with future provider configuration keys.</summary>
    public const int ProviderCodeMaxLength = 60;

    /// <summary>Gateway references can be long, but should remain index-friendly.</summary>
    public const int ReferenceMaxLength = 160;

    private GatewayReference()
    {
        ProviderCode = string.Empty;
        Reference = string.Empty;
    }

    /// <summary>Creates a provider-neutral gateway reference.</summary>
    public GatewayReference(string providerCode, string reference)
    {
        ProviderCode = NormalizeProviderCode(providerCode);
        Reference = NormalizeReference(reference);
    }

    /// <summary>Configuration key for the provider that produced the reference.</summary>
    public string ProviderCode { get; }

    /// <summary>External reference value returned by the provider.</summary>
    public string Reference { get; }

    /// <summary>Normalizes a provider configuration key without requiring a gateway transaction reference.</summary>
    public static string NormalizeProviderCode(string providerCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCode);

        var normalized = providerCode.Trim().ToLower(CultureInfo.InvariantCulture);
        if (normalized.Length > ProviderCodeMaxLength)
        {
            throw new ArgumentException($"Provider code cannot exceed {ProviderCodeMaxLength} characters.", nameof(providerCode));
        }

        if (normalized.Any(static c => !IsAllowedProviderCodeCharacter(c)))
        {
            throw new ArgumentException("Provider code contains unsupported characters.", nameof(providerCode));
        }

        return normalized;
    }

    private static string NormalizeReference(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        var normalized = reference.Trim();
        if (normalized.Length > ReferenceMaxLength)
        {
            throw new ArgumentException($"Gateway reference cannot exceed {ReferenceMaxLength} characters.", nameof(reference));
        }

        return normalized;
    }

    private static bool IsAllowedProviderCodeCharacter(char value)
        => char.IsLetterOrDigit(value) || value is '-' or '_' or '.';
}
