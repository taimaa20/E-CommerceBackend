using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob;

public sealed class PaymobOptions
{
    public const string SectionName = "Payments:Paymob";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://accept.paymob.com";

    public string CreateIntentionPath { get; set; } = "/v1/intention/";

    public string UnifiedCheckoutPath { get; set; } = "/unifiedcheckout/";

    public string SecretKey { get; set; } = string.Empty;

    public string HmacSecret { get; set; } = string.Empty;

    public bool RequireWebhookHmac { get; set; } = true;

    public string HmacHeaderName { get; set; } = "X-Paymob-Signature";

    public string HmacQueryParameterName { get; set; } = "hmac";

    public string[] HmacFieldOrder { get; set; } =
    [
        "amount_cents",
        "created_at",
        "currency",
        "error_occured",
        "has_parent_transaction",
        "id",
        "integration_id",
        "is_3d_secure",
        "is_auth",
        "is_capture",
        "is_refunded",
        "is_standalone_payment",
        "is_voided",
        "order.id",
        "owner",
        "pending",
        "source_data.pan",
        "source_data.sub_type",
        "source_data.type",
        "success"
    ];

    public int WebhookToleranceSeconds { get; set; } = 300;

    public int MaxWebhookBodyBytes { get; set; } = 65536;

    public string PublicKey { get; set; } = string.Empty;

    public string DefaultCurrency { get; set; } = "EGP";

    public string DefaultBillingCountry { get; set; } = "EG";

    public string[] PaymentMethods { get; set; } = [];

    public string? NotificationUrl { get; set; }

    public string? RedirectionUrl { get; set; }

    public string? PaymentStatusRedirectUrl { get; set; }

    public int? ExpirationSeconds { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRetryAttempts { get; set; } = 3;

    public int RetryBaseDelayMilliseconds { get; set; } = 250;

    public int MaxErrorBodyLogLength { get; set; } = 2048;
}

public sealed class PaymobOptionsValidator : IValidateOptions<PaymobOptions>
{
    private readonly IHostEnvironment? _environment;

    public PaymobOptionsValidator(IHostEnvironment? environment = null)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, PaymobOptions options)
    {
        var failures = new List<string>();

        ValidateBaseShape(options, failures);

        if (options.Enabled && _environment?.IsProduction() == true && !options.RequireWebhookHmac)
        {
            failures.Add($"{PaymobOptions.SectionName}:RequireWebhookHmac must be true in Production.");
        }

        if (options.Enabled)
        {
            ValidateEnabledSettings(options, failures);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateBaseShape(PaymobOptions options, List<string> failures)
    {
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"{PaymobOptions.SectionName}:BaseUrl must be an absolute HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(options.CreateIntentionPath) ||
            !options.CreateIntentionPath.StartsWith('/'))
        {
            failures.Add($"{PaymobOptions.SectionName}:CreateIntentionPath must start with '/'.");
        }

        if (string.IsNullOrWhiteSpace(options.UnifiedCheckoutPath) ||
            !options.UnifiedCheckoutPath.StartsWith('/'))
        {
            failures.Add($"{PaymobOptions.SectionName}:UnifiedCheckoutPath must start with '/'.");
        }

        if (options.TimeoutSeconds is < 1 or > 100)
        {
            failures.Add($"{PaymobOptions.SectionName}:TimeoutSeconds must be between 1 and 100.");
        }

        if (options.MaxRetryAttempts is < 0 or > 5)
        {
            failures.Add($"{PaymobOptions.SectionName}:MaxRetryAttempts must be between 0 and 5.");
        }

        if (options.RetryBaseDelayMilliseconds is < 50 or > 5000)
        {
            failures.Add($"{PaymobOptions.SectionName}:RetryBaseDelayMilliseconds must be between 50 and 5000.");
        }

        if (options.MaxErrorBodyLogLength is < 0 or > 8192)
        {
            failures.Add($"{PaymobOptions.SectionName}:MaxErrorBodyLogLength must be between 0 and 8192.");
        }

        if (options.WebhookToleranceSeconds is < 0 or > 86400)
        {
            failures.Add($"{PaymobOptions.SectionName}:WebhookToleranceSeconds must be between 0 and 86400.");
        }

        if (options.MaxWebhookBodyBytes is < 1024 or > 1048576)
        {
            failures.Add($"{PaymobOptions.SectionName}:MaxWebhookBodyBytes must be between 1024 and 1048576.");
        }

        if (options.RequireWebhookHmac && string.IsNullOrWhiteSpace(options.HmacHeaderName))
        {
            failures.Add($"{PaymobOptions.SectionName}:HmacHeaderName is required.");
        }

        if (options.RequireWebhookHmac && string.IsNullOrWhiteSpace(options.HmacQueryParameterName))
        {
            failures.Add($"{PaymobOptions.SectionName}:HmacQueryParameterName is required.");
        }

        if (options.RequireWebhookHmac &&
            (options.HmacFieldOrder.Length == 0 ||
             options.HmacFieldOrder.Any(string.IsNullOrWhiteSpace)))
        {
            failures.Add($"{PaymobOptions.SectionName}:HmacFieldOrder must contain at least one field.");
        }

        ValidateOptionalHttpsUrl(options.NotificationUrl, nameof(options.NotificationUrl), failures);
        ValidateOptionalHttpsUrl(options.RedirectionUrl, nameof(options.RedirectionUrl), failures);
        ValidateOptionalRedirectUrl(options.PaymentStatusRedirectUrl, failures);
    }

    private void ValidateEnabledSettings(PaymobOptions options, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            failures.Add($"{PaymobOptions.SectionName}:SecretKey is required when Paymob is enabled.");
        }

        if (options.RequireWebhookHmac && string.IsNullOrWhiteSpace(options.HmacSecret))
        {
            failures.Add($"{PaymobOptions.SectionName}:HmacSecret is required when Paymob is enabled and RequireWebhookHmac is true.");
        }

        if (options.PaymentMethods.Length == 0 ||
            options.PaymentMethods.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{PaymobOptions.SectionName}:PaymentMethods must contain at least one configured method when Paymob is enabled.");
        }

        if (options.DefaultCurrency.Trim().Length != 3)
        {
            failures.Add($"{PaymobOptions.SectionName}:DefaultCurrency must be a three-letter ISO currency code.");
        }

        if (string.IsNullOrWhiteSpace(options.PaymentStatusRedirectUrl))
        {
            failures.Add($"{PaymobOptions.SectionName}:PaymentStatusRedirectUrl is required when Paymob is enabled.");
        }

        if (options.DefaultBillingCountry.Trim().Length != 2)
        {
            failures.Add($"{PaymobOptions.SectionName}:DefaultBillingCountry must be a two-letter ISO country code.");
        }

        if (options.ExpirationSeconds is < 60 or > 604800)
        {
            failures.Add($"{PaymobOptions.SectionName}:ExpirationSeconds must be between 60 seconds and 7 days when set.");
        }
    }

    private static void ValidateOptionalHttpsUrl(string? value, string settingName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"{PaymobOptions.SectionName}:{settingName} must be an absolute HTTPS URL when set.");
        }
    }

    private static void ValidateOptionalRedirectUrl(string? value, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme == Uri.UriSchemeHttp)
        {
            failures.Add($"{PaymobOptions.SectionName}:PaymentStatusRedirectUrl must be an absolute HTTPS or custom-scheme URL when set.");
        }
    }
}
