using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob.Contracts;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob;

public sealed class PaymobGateway : IPaymentGateway
{
    private const int MinorUnitScale = 100;
    private const int MinimumExpirationSeconds = 60;
    private const int MaximumExpirationSeconds = 604800;

    private readonly IPaymobClient _paymobClient;
    private readonly IOptionsMonitor<PaymobOptions> _options;
    private readonly ILogger<PaymobGateway> _logger;

    public PaymobGateway(
        IPaymobClient paymobClient,
        IOptionsMonitor<PaymobOptions> options,
        ILogger<PaymobGateway> logger)
    {
        _paymobClient = paymobClient ?? throw new ArgumentNullException(nameof(paymobClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderCode => PaymobClient.ProviderCode;

    public async Task<GatewayPaymentResult> CreatePaymentIntentionAsync(
        PaymentIntentionCreateRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = ResolveCorrelationId(request.CorrelationId);
        var options = _options.CurrentValue;
        var payload = BuildPayload(request, options, correlationId);
        var response = await _paymobClient.CreatePaymentIntentionAsync(payload, correlationId, ct);

        ValidateResponse(response, payload, correlationId);

        var gatewayReference = GetGatewayReference(response, correlationId);
        _logger.LogInformation(
            "Paymob gateway created payment intention. PaymentId {PaymentId}. GatewayReference {GatewayReference}. CorrelationId {CorrelationId}",
            request.PaymentId,
            gatewayReference,
            correlationId);

        return new GatewayPaymentResult(
            new GatewayReference(ProviderCode, gatewayReference),
            response.ClientSecret!,
            BuildCheckoutUrl(options, response.ClientSecret!),
            GetResponseAmount(response),
            GetResponseCurrency(response)!,
            response.Status,
            response.Created,
            correlationId,
            response.IntentionOrderId?.ToString(CultureInfo.InvariantCulture));
    }

    private PaymobCreateIntentionRequestDto BuildPayload(
        PaymentIntentionCreateRequest request,
        PaymobOptions options,
        string correlationId)
    {
        ValidateRequest(request, options);

        var totalMinorUnits = ToMinorUnits(request.Amount);
        var itemDtos = BuildItems(request, totalMinorUnits);

        return new PaymobCreateIntentionRequestDto
        {
            Amount = totalMinorUnits,
            Currency = request.Amount.Currency,
            PaymentMethods = BuildPaymentMethods(request, options),
            Items = itemDtos,
            BillingData = BuildBillingData(request.BillingData),
            Customer = BuildCustomer(request.Customer),
            Extras = BuildExtras(request, correlationId),
            SpecialReference = request.MerchantReference.Value,
            Expiration = request.ExpirationSeconds ?? options.ExpirationSeconds,
            NotificationUrl = UseConfiguredOrRequestedUrl(request.NotificationUrl, options.NotificationUrl),
            RedirectionUrl = UseConfiguredOrRequestedUrl(request.RedirectionUrl, options.RedirectionUrl)
        };
    }

    private static void ValidateRequest(PaymentIntentionCreateRequest request, PaymobOptions options)
    {
        if (!options.Enabled)
        {
            throw new PaymentGatewayConfigurationException(PaymobClient.ProviderCode, "Paymob is disabled.");
        }

        if (request.PaymentId == Guid.Empty)
        {
            throw Validation("PaymentId is required.", null);
        }

        if (request.PaymentAttemptId == Guid.Empty)
        {
            throw Validation("PaymentAttemptId is required.", request.CorrelationId);
        }

        if (request.Amount.Amount <= 0m)
        {
            throw Validation("Payment amount must be greater than zero.", request.CorrelationId);
        }

        ValidateExpiration(request.ExpirationSeconds, request.CorrelationId);
        ValidateRequestUrl(request.NotificationUrl, nameof(request.NotificationUrl), request.CorrelationId);
        ValidateRequestUrl(request.RedirectionUrl, nameof(request.RedirectionUrl), request.CorrelationId);

        if (GetConfiguredPaymentMethods(request, options).Count == 0)
        {
            throw Validation("At least one Paymob payment method is required.", request.CorrelationId);
        }
    }

    private static IReadOnlyList<PaymobIntentionItemDto> BuildItems(
        PaymentIntentionCreateRequest request,
        long totalMinorUnits)
    {
        if (request.Items.Count == 0)
        {
            return [new PaymobIntentionItemDto { Name = request.MerchantReference.Value, Amount = totalMinorUnits }];
        }

        var items = request.Items.Select(item => BuildItem(item, request.Amount.Currency, request.CorrelationId)).ToArray();
        if (items.Sum(item => item.Amount) != totalMinorUnits)
        {
            throw Validation("Paymob line items must sum to the requested amount.", request.CorrelationId);
        }

        return items;
    }

    private static PaymobIntentionItemDto BuildItem(
        PaymentGatewayLineItem item,
        string currency,
        string? correlationId)
    {
        if (item.Quantity <= 0)
        {
            throw Validation("Line item quantity must be greater than zero.", correlationId);
        }

        if (!string.Equals(item.Amount.Currency, currency, StringComparison.Ordinal))
        {
            throw Validation("Line item currency must match the payment currency.", correlationId);
        }

        return new PaymobIntentionItemDto
        {
            Name = Required(item.Name, nameof(item.Name), correlationId),
            Amount = ToMinorUnits(item.Amount),
            Description = Optional(item.Description),
            Quantity = item.Quantity,
            Image = Optional(item.ImageUrl)
        };
    }

    private static IReadOnlyList<object> BuildPaymentMethods(
        PaymentIntentionCreateRequest request,
        PaymobOptions options)
    {
        return GetConfiguredPaymentMethods(request, options)
            .Select(method => MapPaymentMethod(method, request.CorrelationId))
            .ToArray();
    }

    private static IReadOnlyList<string> GetConfiguredPaymentMethods(
        PaymentIntentionCreateRequest request,
        PaymobOptions options)
    {
        return request.PaymentMethods.Count > 0 ? request.PaymentMethods : options.PaymentMethods;
    }

    private static object MapPaymentMethod(string method, string? correlationId)
    {
        var normalized = Required(method, nameof(method), correlationId);
        if (!int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var methodId))
        {
            return normalized;
        }

        return methodId > 0 ? methodId : throw Validation("Paymob payment method id must be positive.", correlationId);
    }

    private static PaymobBillingDataDto BuildBillingData(PaymentGatewayBillingData billingData)
    {
        ArgumentNullException.ThrowIfNull(billingData);

        return new PaymobBillingDataDto
        {
            FirstName = Required(billingData.FirstName, nameof(billingData.FirstName), null),
            LastName = Required(billingData.LastName, nameof(billingData.LastName), null),
            Email = Required(billingData.Email, nameof(billingData.Email), null),
            PhoneNumber = Required(billingData.PhoneNumber, nameof(billingData.PhoneNumber), null),
            Country = Required(billingData.Country, nameof(billingData.Country), null),
            City = Optional(billingData.City),
            State = Optional(billingData.State),
            PostalCode = Optional(billingData.PostalCode),
            Street = Optional(billingData.Street),
            Building = Optional(billingData.Building),
            Floor = Optional(billingData.Floor),
            Apartment = Optional(billingData.Apartment)
        };
    }

    private static PaymobCustomerDto? BuildCustomer(PaymentGatewayCustomerData? customer)
    {
        return customer is null
            ? null
            : new PaymobCustomerDto
            {
                FirstName = Required(customer.FirstName, nameof(customer.FirstName), null),
                LastName = Required(customer.LastName, nameof(customer.LastName), null),
                Email = Required(customer.Email, nameof(customer.Email), null)
            };
    }

    private static IReadOnlyDictionary<string, string> BuildExtras(
        PaymentIntentionCreateRequest request,
        string correlationId)
    {
        return new Dictionary<string, string>
        {
            ["payment_id"] = request.PaymentId.ToString("D", CultureInfo.InvariantCulture),
            ["payment_attempt_id"] = request.PaymentAttemptId.ToString("D", CultureInfo.InvariantCulture),
            ["merchant_reference"] = request.MerchantReference.Value,
            ["correlation_id"] = correlationId
        };
    }

    private static void ValidateResponse(
        PaymobCreateIntentionResponseDto response,
        PaymobCreateIntentionRequestDto payload,
        string correlationId)
    {
        if (GetResponseAmount(response) != payload.Amount ||
            !string.Equals(GetResponseCurrency(response), payload.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new PaymentGatewayException(PaymobClient.ProviderCode, "Paymob response amount or currency did not match the request.", correlationId);
        }
    }

    // Paymob's Intention API returns amount/currency nested in intention_detail; the top-level
    // fields only exist on legacy/alternate response shapes, so they are the fallback.
    private static long GetResponseAmount(PaymobCreateIntentionResponseDto response)
    {
        return response.IntentionDetail?.Amount ?? response.Amount;
    }

    private static string? GetResponseCurrency(PaymobCreateIntentionResponseDto response)
    {
        return Optional(response.IntentionDetail?.Currency) ?? Optional(response.Currency);
    }

    // Paymob's Intention API returns only client_secret; the Unified Checkout URL is
    // constructed by the merchant as {BaseUrl}/unifiedcheckout/?publicKey=...&clientSecret=...
    private static string BuildCheckoutUrl(PaymobOptions options, string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(options.PublicKey))
        {
            throw new PaymentGatewayConfigurationException(PaymobClient.ProviderCode, "Paymob public key is not configured.");
        }

        var checkoutUri = new Uri(new Uri(options.BaseUrl, UriKind.Absolute), options.UnifiedCheckoutPath);

        return $"{checkoutUri}?publicKey={Uri.EscapeDataString(options.PublicKey.Trim())}&clientSecret={Uri.EscapeDataString(clientSecret)}";
    }

    private static string GetGatewayReference(PaymobCreateIntentionResponseDto response, string correlationId)
    {
        var reference = Optional(response.Id) ?? response.IntentionOrderId?.ToString(CultureInfo.InvariantCulture);

        return !string.IsNullOrWhiteSpace(reference)
            ? reference
            : throw new PaymentGatewayException(PaymobClient.ProviderCode, "Paymob response was missing the gateway reference.", correlationId);
    }

    private static long ToMinorUnits(Money amount)
    {
        var minorUnits = amount.Amount * MinorUnitScale;
        if (decimal.Truncate(minorUnits) != minorUnits || minorUnits > long.MaxValue)
        {
            throw Validation("Money amount cannot be represented as Paymob minor units.", null);
        }

        return decimal.ToInt64(minorUnits);
    }

    private static void ValidateExpiration(int? expirationSeconds, string? correlationId)
    {
        if (expirationSeconds is < MinimumExpirationSeconds or > MaximumExpirationSeconds)
        {
            throw Validation("Payment intention expiration must be between 60 seconds and 7 days.", correlationId);
        }
    }

    private static void ValidateRequestUrl(string? value, string name, string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw Validation($"{name} must be an absolute HTTPS URL.", correlationId);
        }
    }

    private static string? UseConfiguredOrRequestedUrl(string? requested, string? configured)
    {
        return Optional(requested) ?? Optional(configured);
    }

    private static string ResolveCorrelationId(string? correlationId)
    {
        return Optional(correlationId) ??
            Activity.Current?.TraceId.ToString() ??
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
    }

    private static string Required(string? value, string name, string? correlationId)
    {
        return Optional(value) ?? throw Validation($"{name} is required.", correlationId);
    }

    private static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static PaymentGatewayValidationException Validation(string message, string? correlationId)
    {
        return new PaymentGatewayValidationException(PaymobClient.ProviderCode, message, correlationId);
    }
}
