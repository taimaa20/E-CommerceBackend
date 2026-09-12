using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob;

public sealed class PaymobWebhookVerifier : IPaymentWebhookVerifier
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    private readonly IOptionsMonitor<PaymobOptions> _options;
    private readonly ILogger<PaymobWebhookVerifier> _logger;

    public PaymobWebhookVerifier(
        IOptionsMonitor<PaymobOptions> options,
        ILogger<PaymobWebhookVerifier> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderCode => PaymobClient.ProviderCode;

    public Task<PaymentWebhookVerificationResult> VerifyAsync(
        PaymentWebhookVerificationRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = _options.CurrentValue;
        var result = Verify(request, options);
        if (!result.IsValid)
        {
            _logger.LogWarning(
                "Rejected Paymob webhook. Reason {Reason}. CorrelationId {CorrelationId}. RemoteIp {RemoteIpAddress}",
                result.Reason,
                request.CorrelationId,
                request.RemoteIpAddress);
        }

        return Task.FromResult(result);
    }

    private PaymentWebhookVerificationResult Verify(
        PaymentWebhookVerificationRequest request,
        PaymobOptions options)
    {
        if (!options.Enabled)
        {
            return Invalid(PaymentWebhookRejectionReason.ProviderDisabled, "Paymob is disabled.");
        }

        if (options.RequireWebhookHmac && string.IsNullOrWhiteSpace(options.HmacSecret))
        {
            return Invalid(PaymentWebhookRejectionReason.ProviderMisconfigured, "Paymob webhook HMAC is not configured.");
        }

        if (Encoding.UTF8.GetByteCount(request.RawBody) > options.MaxWebhookBodyBytes)
        {
            return Invalid(PaymentWebhookRejectionReason.PayloadInvalid, "Webhook payload is too large.");
        }

        using var document = ParseBody(request.RawBody);
        if (document is null || !TryGetTransactionElement(document.RootElement, out var transaction))
        {
            return Invalid(PaymentWebhookRejectionReason.PayloadInvalid, "Webhook payload is not a valid Paymob transaction.");
        }

        if (IsReplayWindowExceeded(transaction, options.WebhookToleranceSeconds))
        {
            return Invalid(PaymentWebhookRejectionReason.ReplayWindowExceeded, "Webhook timestamp is outside the accepted replay window.");
        }

        if (!options.RequireWebhookHmac)
        {
            _logger.LogWarning(
                "Paymob webhook HMAC verification is disabled. Use this only for sandbox/testing. CorrelationId {CorrelationId}. RemoteIp {RemoteIpAddress}",
                request.CorrelationId,
                request.RemoteIpAddress);

            return PaymentWebhookVerificationResult.Valid();
        }

        var signature = ResolveSignature(request, options);
        if (string.IsNullOrWhiteSpace(signature))
        {
            return Invalid(PaymentWebhookRejectionReason.SignatureMissing, "Webhook signature is missing.");
        }

        var expected = ComputeHmac(transaction, options);
        return SignatureMatches(signature, expected)
            ? PaymentWebhookVerificationResult.Valid()
            : Invalid(PaymentWebhookRejectionReason.SignatureInvalid, "Webhook signature is invalid.");
    }

    private static JsonDocument? ParseBody(string rawBody)
    {
        try
        {
            return JsonDocument.Parse(rawBody, DocumentOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetTransactionElement(JsonElement root, out JsonElement transaction)
    {
        if (TryGetObjectProperty(root, "obj", out transaction))
        {
            return true;
        }

        if (TryGetObjectProperty(root, "transaction", out transaction))
        {
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("id", out _))
        {
            transaction = root;
            return true;
        }

        transaction = default;
        return false;
    }

    private static string? ResolveSignature(
        PaymentWebhookVerificationRequest request,
        PaymobOptions options)
    {
        return TryGetValue(request.Headers, options.HmacHeaderName)
            ?? TryGetValue(request.Headers, "X-Paymob-Hmac")
            ?? TryGetValue(request.Headers, "X-HMAC-SHA512")
            ?? TryGetValue(request.Headers, "hmac")
            ?? TryGetValue(request.Query, options.HmacQueryParameterName)
            ?? TryGetValue(request.Query, "hmac")
            ?? TryGetValue(request.Query, "signature");
    }

    private static byte[] ComputeHmac(JsonElement transaction, PaymobOptions options)
    {
        var builder = new StringBuilder();
        foreach (var field in options.HmacFieldOrder)
        {
            builder.Append(ReadPathValue(transaction, field));
        }

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(options.HmacSecret.Trim()));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string ReadPathValue(JsonElement root, string fieldPath)
    {
        var current = root;
        foreach (var part in fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryGetObjectProperty(current, part, out current))
            {
                return string.Empty;
            }
        }

        return FormatHmacValue(current);
    }

    private static string FormatHmacValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            _ => value.GetRawText()
        };
    }

    private static bool IsReplayWindowExceeded(JsonElement transaction, int toleranceSeconds)
    {
        if (toleranceSeconds <= 0)
        {
            return false;
        }

        if (!TryGetObjectProperty(transaction, "created_at", out var createdAt) ||
            createdAt.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(
                createdAt.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var occurredAt))
        {
            return true;
        }

        var drift = (DateTimeOffset.UtcNow - occurredAt.ToUniversalTime()).Duration();
        return drift > TimeSpan.FromSeconds(toleranceSeconds);
    }

    private static bool SignatureMatches(string receivedSignature, byte[] expected)
    {
        var normalized = receivedSignature.Trim();
        if (normalized.Length != expected.Length * 2)
        {
            return false;
        }

        try
        {
            var received = Convert.FromHexString(normalized);
            return CryptographicOperations.FixedTimeEquals(received, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryGetObjectProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? TryGetValue(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value)
            ? value
            : null;
    }

    private static PaymentWebhookVerificationResult Invalid(
        PaymentWebhookRejectionReason reason,
        string message)
        => PaymentWebhookVerificationResult.Invalid(reason, message);
}
