using System.Globalization;

namespace RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;

/// <summary>
/// Immutable monetary value object.
/// Currency is part of equality so different currencies can never be added accidentally.
/// </summary>
public sealed record Money
{
    /// <summary>Database precision used for all payment amounts.</summary>
    public const string DatabaseType = "decimal(18,2)";

    /// <summary>ISO-4217 currency codes are three letters.</summary>
    public const int CurrencyCodeLength = 3;

    private Money()
    {
        Currency = string.Empty;
    }

    /// <summary>Creates a non-negative money value in an ISO-4217 currency.</summary>
    public Money(decimal amount, string currency)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Money amount cannot be negative.");
        }

        Amount = amount;
        Currency = NormalizeCurrency(currency);
    }

    /// <summary>Monetary amount stored as decimal to avoid floating-point rounding errors.</summary>
    public decimal Amount { get; }

    /// <summary>Uppercase ISO-4217 currency code.</summary>
    public string Currency { get; }

    /// <summary>Creates a zero value for the same currency rules as real money.</summary>
    public static Money Zero(string currency) => new(0m, currency);

    /// <summary>Adds two values only when their currencies match.</summary>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>Subtracts two values only when their currencies match and the result stays non-negative.</summary>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);

        if (other.Amount > Amount)
        {
            throw new InvalidOperationException("Money subtraction cannot produce a negative amount.");
        }

        return new Money(Amount - other.Amount, Currency);
    }

    private static string NormalizeCurrency(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var normalized = currency.Trim().ToUpper(CultureInfo.InvariantCulture);
        if (normalized.Length != CurrencyCodeLength || normalized.Any(static c => c < 'A' || c > 'Z'))
        {
            throw new ArgumentException("Currency must be a three-letter ISO-4217 code.", nameof(currency));
        }

        return normalized;
    }

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Money operations require matching currencies.");
        }
    }
}
