using RestaurantPos.Api.Modules.Payments.Api;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.Enums;

namespace RestaurantPos.Api.Modules.Payments.Application.Queries;

internal static class PaymentEngineQueryMapper
{
    private static readonly PaymentSessionStatus[] ActiveSessionStatuses =
    [
        PaymentSessionStatus.Created,
        PaymentSessionStatus.Pending,
        PaymentSessionStatus.RequiresAction
    ];

    public static PaymentEnginePaymentDetailsDto Map(Payment payment)
    {
        var activeSession = payment.Sessions
            .Where(session => ActiveSessionStatuses.Contains(session.Status))
            .OrderByDescending(session => session.CreatedAtUtc)
            .FirstOrDefault();
        var gatewayReference = payment.Attempts
            .OrderByDescending(attempt => attempt.AttemptNumber)
            .Select(attempt => attempt.GatewayReference?.Reference)
            .FirstOrDefault(reference => !string.IsNullOrWhiteSpace(reference));

        return new PaymentEnginePaymentDetailsDto
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            BranchId = payment.BranchId,
            MerchantReference = payment.MerchantReference.Value,
            ProviderCode = payment.ProviderCode,
            GatewayReference = gatewayReference ?? activeSession?.SessionReference?.Reference,
            Status = payment.Status,
            Method = payment.Method,
            Amount = payment.RequestedAmount.Amount,
            Currency = payment.RequestedAmount.Currency,
            CapturedAmount = payment.CapturedAmount.Amount,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt,
            ExpiresAtUtc = payment.ExpiresAtUtc,
            CompletedAtUtc = payment.CompletedAtUtc,
            ActiveCheckoutUrl = activeSession?.CheckoutUrl,
            Attempts = payment.Attempts
                .OrderBy(attempt => attempt.AttemptNumber)
                .Select(MapAttempt)
                .ToArray(),
            Sessions = payment.Sessions
                .OrderBy(session => session.CreatedAtUtc)
                .Select(MapSession)
                .ToArray()
        };
    }

    private static PaymentEngineAttemptDto MapAttempt(PaymentAttempt attempt)
    {
        return new PaymentEngineAttemptDto
        {
            AttemptId = attempt.Id,
            AttemptNumber = attempt.AttemptNumber,
            Operation = attempt.Operation,
            Status = attempt.Status,
            Amount = attempt.Amount.Amount,
            Currency = attempt.Amount.Currency,
            GatewayProvider = attempt.GatewayReference?.ProviderCode,
            GatewayReference = attempt.GatewayReference?.Reference,
            FailureCode = attempt.FailureCode,
            FailureReason = attempt.FailureReason,
            CreatedAt = attempt.CreatedAt,
            UpdatedAt = attempt.UpdatedAt,
            StartedAtUtc = attempt.StartedAtUtc,
            CompletedAtUtc = attempt.CompletedAtUtc
        };
    }

    private static PaymentEngineSessionDto MapSession(PaymentSession session)
    {
        return new PaymentEngineSessionDto
        {
            SessionId = session.Id,
            Status = session.Status,
            ProviderCode = session.ProviderCode,
            GatewayProvider = session.SessionReference?.ProviderCode,
            GatewayReference = session.SessionReference?.Reference,
            CheckoutUrl = session.CheckoutUrl,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            CreatedAtUtc = session.CreatedAtUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            CompletedAtUtc = session.CompletedAtUtc
        };
    }
}
