using System.Net;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;

public class PaymentGatewayException : Exception
{
    public PaymentGatewayException(
        string providerCode,
        string message,
        string? correlationId = null,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderCode = providerCode;
        CorrelationId = correlationId;
        StatusCode = statusCode;
    }

    public string ProviderCode { get; }

    public string? CorrelationId { get; }

    public HttpStatusCode? StatusCode { get; }
}

public sealed class PaymentGatewayConfigurationException : PaymentGatewayException
{
    public PaymentGatewayConfigurationException(string providerCode, string message)
        : base(providerCode, message)
    {
    }
}

public sealed class PaymentGatewayValidationException : PaymentGatewayException
{
    public PaymentGatewayValidationException(
        string providerCode,
        string message,
        string? correlationId,
        HttpStatusCode? statusCode = null)
        : base(providerCode, message, correlationId, statusCode)
    {
    }
}

public sealed class PaymentGatewayAuthenticationException : PaymentGatewayException
{
    public PaymentGatewayAuthenticationException(
        string providerCode,
        string message,
        string? correlationId,
        HttpStatusCode? statusCode)
        : base(providerCode, message, correlationId, statusCode)
    {
    }
}

public sealed class PaymentGatewayTimeoutException : PaymentGatewayException
{
    public PaymentGatewayTimeoutException(
        string providerCode,
        string message,
        string? correlationId,
        Exception? innerException = null)
        : base(providerCode, message, correlationId, HttpStatusCode.RequestTimeout, innerException)
    {
    }
}

public sealed class PaymentGatewayUnavailableException : PaymentGatewayException
{
    public PaymentGatewayUnavailableException(
        string providerCode,
        string message,
        string? correlationId,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(providerCode, message, correlationId, statusCode, innerException)
    {
    }
}
