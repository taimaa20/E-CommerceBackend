using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Modules.Payments.Domain.Enums;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using PaymentMerchantReference = RestaurantPos.Api.Modules.Payments.Domain.ValueObjects.MerchantReference;

namespace RestaurantPos.Api.Modules.Payments.Api;

internal static class PaymentApiValidation
{
    public const int TextMaxLength = 200;
    public const int IdempotencyKeyMaxLength = 200;
    public const int PhoneMaxLength = 40;
    public const int CountryCodeLength = 2;
    public const int UrlMaxLength = 2048;
    public const int PaymentMethodsMaxCount = 20;
    public const int MinimumExpirationSeconds = 60;
    public const int MaximumExpirationSeconds = 604800;
    public const string MinimumAmount = "0.01";
    public const string MaximumAmount = "9999999999999999.99";
}

public sealed record CreatePaymentRequestDto
{
    [Required]
    public Guid OrderId { get; init; }

    [Required]
    [StringLength(PaymentMerchantReference.MaxLength)]
    public string MerchantReference { get; init; } = string.Empty;

    [Range(typeof(decimal), PaymentApiValidation.MinimumAmount, PaymentApiValidation.MaximumAmount)]
    public decimal Amount { get; init; }

    [Required]
    [StringLength(Money.CurrencyCodeLength, MinimumLength = Money.CurrencyCodeLength)]
    public string Currency { get; init; } = string.Empty;

    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod Method { get; init; } = PaymentMethod.Card;

    [Required]
    [StringLength(GatewayReference.ProviderCodeMaxLength)]
    public string ProviderCode { get; init; } = string.Empty;

    [Required]
    [StringLength(PaymentApiValidation.IdempotencyKeyMaxLength)]
    public string IdempotencyKey { get; init; } = string.Empty;

    [Required]
    public CreatePaymentBillingDataDto BillingData { get; init; } = null!;

    public CreatePaymentCustomerDataDto? Customer { get; init; }

    public IReadOnlyList<CreatePaymentLineItemDto> Items { get; init; } = [];

    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string? CorrelationId { get; init; }
}

public sealed record CreatePaymentBillingDataDto
{
    [Required]
    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(PaymentApiValidation.PhoneMaxLength)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required]
    [StringLength(PaymentApiValidation.CountryCodeLength, MinimumLength = PaymentApiValidation.CountryCodeLength)]
    public string Country { get; init; } = string.Empty;

    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string? City { get; init; }

    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string? State { get; init; }

    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string? PostalCode { get; init; }

    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string? Street { get; init; }

    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string? Building { get; init; }

    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string? Floor { get; init; }

    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string? Apartment { get; init; }
}

public sealed record CreatePaymentCustomerDataDto
{
    [Required]
    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string Email { get; init; } = string.Empty;
}

public sealed record CreatePaymentLineItemDto
{
    [Required]
    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string Name { get; init; } = string.Empty;

    [Range(typeof(decimal), PaymentApiValidation.MinimumAmount, PaymentApiValidation.MaximumAmount)]
    public decimal Amount { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; } = 1;

    [StringLength(PaymentApiValidation.TextMaxLength)]
    public string? Description { get; init; }

    [StringLength(PaymentApiValidation.UrlMaxLength)]
    public string? ImageUrl { get; init; }
}
