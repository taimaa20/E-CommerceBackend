using System.Globalization;
using System.Text.Json;
using RestaurantPos.Api.Modules.Payments.Domain.Enums;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob.Contracts;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob;

public sealed class PaymobWebhookMapper : IPaymentWebhookMapper
{
    private const decimal MinorUnitScale = 100m;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderCode => PaymobClient.ProviderCode;

    public GatewayWebhookEvent Map(
        string rawBody,
        string payloadHash,
        string correlationId)
    {
        var transaction = ReadTransaction(rawBody);
        var transactionId = transaction.Id
            ?? throw new PaymentWebhookMappingException("Paymob transaction id is required.");
        var amountCents = transaction.AmountCents
            ?? throw new PaymentWebhookMappingException("Paymob transaction amount is required.");
        var currency = Required(transaction.Currency, "Paymob transaction currency is required.");
        var eventType = MapEventType(transaction);
        var gatewayReference = new GatewayReference(ProviderCode, transactionId.ToString(CultureInfo.InvariantCulture));

        return new GatewayWebhookEvent
        {
            ProviderCode = ProviderCode,
            EventType = eventType,
            ExternalEventId = $"paymob-transaction-{transactionId.ToString(CultureInfo.InvariantCulture)}",
            PayloadHash = payloadHash,
            PaymentId = TryGetGuidExtra(transaction, "payment_id"),
            PaymentAttemptId = TryGetGuidExtra(transaction, "payment_attempt_id"),
            PaymentSessionId = TryGetGuidExtra(transaction, "payment_session_id"),
            MerchantReference = TrimOptional(transaction.Order?.MerchantOrderId) ?? TryGetStringExtra(transaction, "merchant_reference"),
            GatewayReference = gatewayReference,
            Amount = amountCents / MinorUnitScale,
            Currency = currency,
            OccurredAtUtc = ParseOccurredAt(transaction.CreatedAt),
            FailureReason = MapFailureReason(transaction, eventType),
            FailureCode = eventType == GatewayWebhookEventType.PaymentSucceeded ? null : TrimOptional(transaction.Status),
            FailureMessage = eventType == GatewayWebhookEventType.PaymentSucceeded ? null : "Paymob reported a non-success payment outcome.",
            ProviderStatus = ResolveProviderStatus(transaction, eventType),
            ProviderCorrelationId = transaction.Order?.Id?.ToString(CultureInfo.InvariantCulture),
            CorrelationId = correlationId
        };
    }

    private static PaymobWebhookTransactionDto ReadTransaction(string rawBody)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<PaymobWebhookEnvelopeDto>(rawBody, JsonOptions);
            var transaction = envelope?.GetTransaction();
            if (transaction is not null)
            {
                return transaction;
            }

            transaction = JsonSerializer.Deserialize<PaymobWebhookTransactionDto>(rawBody, JsonOptions);
            return transaction ?? throw new PaymentWebhookMappingException("Paymob webhook payload is empty.");
        }
        catch (JsonException ex)
        {
            throw new PaymentWebhookMappingException($"Paymob webhook payload is not valid JSON: {ex.Message}");
        }
    }

    private static GatewayWebhookEventType MapEventType(PaymobWebhookTransactionDto transaction)
    {
        var status = TrimOptional(transaction.Status)?.ToLower(CultureInfo.InvariantCulture);
        if (status?.Contains("refund", StringComparison.Ordinal) == true || transaction.IsRefunded == true)
        {
            throw new PaymentWebhookMappingException("Refund webhooks are not handled by this phase.");
        }

        if (status?.Contains("expire", StringComparison.Ordinal) == true)
        {
            return GatewayWebhookEventType.PaymentExpired;
        }

        if (status?.Contains("cancel", StringComparison.Ordinal) == true ||
            transaction.IsVoided == true)
        {
            return GatewayWebhookEventType.PaymentCancelled;
        }

        if (transaction.Success == true &&
            transaction.Pending != true &&
            transaction.ErrorOccured != true)
        {
            return GatewayWebhookEventType.PaymentSucceeded;
        }

        if (transaction.Pending == true)
        {
            throw new PaymentWebhookMappingException("Pending webhooks are not terminal payment outcomes.");
        }

        return GatewayWebhookEventType.PaymentFailed;
    }

    private static PaymentFailureReason MapFailureReason(
        PaymobWebhookTransactionDto transaction,
        GatewayWebhookEventType eventType)
    {
        if (eventType != GatewayWebhookEventType.PaymentFailed)
        {
            return PaymentFailureReason.None;
        }

        var status = TrimOptional(transaction.Status)?.ToLower(CultureInfo.InvariantCulture);
        if (status?.Contains("insufficient", StringComparison.Ordinal) == true)
        {
            return PaymentFailureReason.InsufficientFunds;
        }

        if (status?.Contains("fraud", StringComparison.Ordinal) == true)
        {
            return PaymentFailureReason.FraudSuspected;
        }

        return PaymentFailureReason.Unknown;
    }

    private static string ResolveProviderStatus(
        PaymobWebhookTransactionDto transaction,
        GatewayWebhookEventType eventType)
    {
        return TrimOptional(transaction.Status) ?? eventType switch
        {
            GatewayWebhookEventType.PaymentSucceeded => "success",
            GatewayWebhookEventType.PaymentExpired => "expired",
            GatewayWebhookEventType.PaymentCancelled => "cancelled",
            _ => "failed"
        };
    }

    private static DateTime ParseOccurredAt(string? value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed.UtcDateTime
            : DateTime.UtcNow;
    }

    private static Guid? TryGetGuidExtra(PaymobWebhookTransactionDto transaction, string key)
    {
        var value = TryGetStringExtra(transaction, key);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static string? TryGetStringExtra(PaymobWebhookTransactionDto transaction, string key)
    {
        return TryGetJsonValue(transaction.Extras, key)
            ?? TryGetJsonValue(transaction.Extra, key)
            ?? TryGetJsonValue(transaction.Order?.Extras, key)
            ?? TryGetJsonValue(transaction.Order?.Extra, key)
            ?? TryGetJsonValue(transaction.ExtensionData, key)
            ?? TryGetJsonValue(transaction.Order?.ExtensionData, key);
    }

    private static string? TryGetJsonValue(IDictionary<string, JsonElement>? values, string key)
    {
        if (values is null)
        {
            return null;
        }

        foreach (var pair in values)
        {
            if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return pair.Value.ValueKind switch
            {
                JsonValueKind.String => TrimOptional(pair.Value.GetString()),
                JsonValueKind.Number => pair.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        return null;
    }

    private static string Required(string? value, string message)
        => TrimOptional(value) ?? throw new PaymentWebhookMappingException(message);

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
