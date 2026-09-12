using System.Net.Mail;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;

namespace RestaurantPos.Api.Modules.Payments.Application.CreatePayment;

public sealed class CreatePaymentCommandValidator
{
    private const int IdempotencyKeyMaxLength = 200;
    private const int TextMaxLength = 200;
    private const int PhoneMaxLength = 40;
    private const int CountryCodeMaxLength = 2;
    private const int MinimumExpirationSeconds = 60;
    private const int MaximumExpirationSeconds = 604800;

    public void ValidateAndThrow(CreatePaymentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new Dictionary<string, List<string>>();

        RequiredGuid(command.TenantId, nameof(command.TenantId), errors);
        RequiredGuid(command.BranchId, nameof(command.BranchId), errors);
        RequiredGuid(command.OrderId, nameof(command.OrderId), errors);
        ValidateMerchantReference(command.MerchantReference, errors);
        ValidateProviderCode(command.ProviderCode, errors);
        ValidateCurrency(command.Currency, errors);
        ValidateAmount(command.Amount, nameof(command.Amount), errors);
        ValidateEnum(command.Method, nameof(command.Method), errors);
        ValidateIdempotencyKey(command.IdempotencyKey, errors);
        ValidateBillingData(command.BillingData, errors);
        ValidateCustomer(command.Customer, errors);
        ValidateLineItems(command, errors);
        ValidateUrl(command.NotificationUrl, nameof(command.NotificationUrl), errors);
        ValidateUrl(command.RedirectionUrl, nameof(command.RedirectionUrl), errors);
        ValidateExpiration(command.ExpirationSeconds, errors);

        if (errors.Count > 0)
        {
            throw new ValidationException(errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray()));
        }
    }

    private static void ValidateMerchantReference(string value, Dictionary<string, List<string>> errors)
    {
        TryValueObject(() => new MerchantReference(value), nameof(CreatePaymentCommand.MerchantReference), errors);
    }

    private static void ValidateProviderCode(string value, Dictionary<string, List<string>> errors)
    {
        TryValueObject(() => GatewayReference.NormalizeProviderCode(value), nameof(CreatePaymentCommand.ProviderCode), errors);
    }

    private static void ValidateCurrency(string value, Dictionary<string, List<string>> errors)
    {
        TryValueObject(() => Money.Zero(value), nameof(CreatePaymentCommand.Currency), errors);
    }

    private static void ValidateAmount(decimal amount, string key, Dictionary<string, List<string>> errors)
    {
        if (amount <= 0m)
        {
            Add(errors, key, "Amount must be greater than zero.");
            return;
        }

        if (decimal.Round(amount, 2) != amount)
        {
            Add(errors, key, "Amount cannot have more than two decimal places.");
        }
    }

    private static void ValidateIdempotencyKey(string value, Dictionary<string, List<string>> errors)
    {
        RequiredText(value, nameof(CreatePaymentCommand.IdempotencyKey), IdempotencyKeyMaxLength, errors);
    }

    private static void ValidateBillingData(CreatePaymentBillingData? data, Dictionary<string, List<string>> errors)
    {
        if (data is null)
        {
            Add(errors, nameof(CreatePaymentCommand.BillingData), "Billing data is required.");
            return;
        }

        RequiredText(data.FirstName, "BillingData.FirstName", TextMaxLength, errors);
        RequiredText(data.LastName, "BillingData.LastName", TextMaxLength, errors);
        RequiredEmail(data.Email, "BillingData.Email", errors);
        RequiredText(data.PhoneNumber, "BillingData.PhoneNumber", PhoneMaxLength, errors);
        RequiredText(data.Country, "BillingData.Country", CountryCodeMaxLength, errors);
    }

    private static void ValidateCustomer(CreatePaymentCustomerData? data, Dictionary<string, List<string>> errors)
    {
        if (data is null)
        {
            return;
        }

        RequiredText(data.FirstName, "Customer.FirstName", TextMaxLength, errors);
        RequiredText(data.LastName, "Customer.LastName", TextMaxLength, errors);
        RequiredEmail(data.Email, "Customer.Email", errors);
    }

    private static void ValidateLineItems(CreatePaymentCommand command, Dictionary<string, List<string>> errors)
    {
        if (command.Items.Count == 0)
        {
            return;
        }

        for (var i = 0; i < command.Items.Count; i++)
        {
            var item = command.Items[i];
            RequiredText(item.Name, $"Items[{i}].Name", TextMaxLength, errors);
            ValidateAmount(item.Amount, $"Items[{i}].Amount", errors);

            if (item.Quantity <= 0)
            {
                Add(errors, $"Items[{i}].Quantity", "Quantity must be greater than zero.");
            }
        }

        var itemTotal = command.Items.Sum(static item => item.Amount);
        if (itemTotal != command.Amount)
        {
            Add(errors, nameof(command.Items), "Line items must sum to the payment amount.");
        }
    }

    private static void ValidateUrl(string? value, string key, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            Add(errors, key, "URL must be an absolute HTTPS URL.");
        }
    }

    private static void ValidateExpiration(int? value, Dictionary<string, List<string>> errors)
    {
        if (value is < MinimumExpirationSeconds or > MaximumExpirationSeconds)
        {
            Add(errors, nameof(CreatePaymentCommand.ExpirationSeconds), "Expiration must be between 60 seconds and 7 days.");
        }
    }

    private static void RequiredGuid(Guid value, string key, Dictionary<string, List<string>> errors)
    {
        if (value == Guid.Empty)
        {
            Add(errors, key, "Value is required.");
        }
    }

    private static void RequiredText(string? value, string key, int maxLength, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(errors, key, "Value is required.");
            return;
        }

        if (value.Trim().Length > maxLength)
        {
            Add(errors, key, $"Value cannot exceed {maxLength} characters.");
        }
    }

    private static void RequiredEmail(string? value, string key, Dictionary<string, List<string>> errors)
    {
        RequiredText(value, key, TextMaxLength, errors);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            _ = new MailAddress(value);
        }
        catch (FormatException)
        {
            Add(errors, key, "Email is not valid.");
        }
    }

    private static void ValidateEnum<T>(T value, string key, Dictionary<string, List<string>> errors)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            Add(errors, key, "Value is not supported.");
        }
    }

    private static void TryValueObject(Action create, string key, Dictionary<string, List<string>> errors)
    {
        try
        {
            create();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentNullException)
        {
            Add(errors, key, ex.Message);
        }
    }

    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var values))
        {
            values = [];
            errors[key] = values;
        }

        values.Add(message);
    }
}
