using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.Modules.Payments.Api;
using RestaurantPos.Api.Modules.Payments.Application.ProcessWebhook;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob;
using System.Text;

namespace RestaurantPos.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/payment-engine/webhooks")]
[Produces("application/json")]
public sealed class PaymentWebhooksController : ControllerBase
{
    private const int MaxConfiguredWebhookBodyBytes = 1_048_576;

    private readonly ISender _sender;
    private readonly IOptionsMonitor<PaymobOptions> _paymobOptions;

    public PaymentWebhooksController(
        ISender sender,
        IOptionsMonitor<PaymobOptions> paymobOptions)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _paymobOptions = paymobOptions ?? throw new ArgumentNullException(nameof(paymobOptions));
    }

    [HttpPost("paymob")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxConfiguredWebhookBodyBytes)]
    [ProducesResponseType(typeof(PaymentWebhookResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentWebhookResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PaymentWebhookResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(PaymentWebhookResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PaymentWebhookResponseDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(PaymentWebhookResponseDto), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(PaymentWebhookResponseDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ReceivePaymobWebhook(CancellationToken ct)
    {
        var headers = ReadHeaders();
        var correlationId = ResolveCorrelationId(headers);
        var maxBodyBytes = _paymobOptions.CurrentValue.MaxWebhookBodyBytes;
        var rawBody = await ReadRawBodyAsync(Request.Body, maxBodyBytes, ct);

        if (rawBody is null)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new PaymentWebhookResponseDto
                {
                    Status = "rejected",
                    CorrelationId = correlationId,
                    Reason = "Webhook payload is too large."
                });
        }

        var result = await _sender.Send(
            new ProcessWebhookCommand
            {
                ProviderCode = PaymobClient.ProviderCode,
                RawBody = rawBody,
                Headers = headers,
                Query = ReadQuery(),
                CorrelationId = correlationId,
                RemoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            },
            ct);

        return StatusCode(result.StatusCode, MapResponse(result));
    }

    [HttpGet("paymob/return")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult ReceivePaymobReturn([FromQuery] bool? success)
    {
        var status = success == true ? "success" : "failed";
        return Redirect(BuildPaymentStatusRedirectUrl(status));
    }

    private string BuildPaymentStatusRedirectUrl(string status)
    {
        var configuredUrl = _paymobOptions.CurrentValue.PaymentStatusRedirectUrl
            ?? throw new InvalidOperationException("Paymob payment status redirect URL is not configured.");
        var builder = new UriBuilder(configuredUrl.Trim());
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(builder.Query);
        query["status"] = status;
        builder.Query = QueryString.Create(query.SelectMany(item =>
            item.Value.Select(value => new KeyValuePair<string, string?>(item.Key, value)))).Value;

        return builder.Uri.AbsoluteUri;
    }

    private IReadOnlyDictionary<string, string> ReadHeaders()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in Request.Headers)
        {
            headers[header.Key] = header.Value.ToString();
        }

        return headers;
    }

    private IReadOnlyDictionary<string, string> ReadQuery()
    {
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Request.Query)
        {
            query[item.Key] = item.Value.ToString();
        }

        return query;
    }

    private string ResolveCorrelationId(IReadOnlyDictionary<string, string> headers)
    {
        return TryGetHeader(headers, PaymobClient.CorrelationHeaderName)
            ?? TryGetHeader(headers, "X-Correlation-ID")
            ?? TryGetHeader(headers, "X-Request-ID")
            ?? (string.IsNullOrWhiteSpace(HttpContext.TraceIdentifier)
                ? Guid.NewGuid().ToString("N")
                : HttpContext.TraceIdentifier);
    }

    private static string? TryGetHeader(IReadOnlyDictionary<string, string> headers, string name)
    {
        return headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static async Task<string?> ReadRawBodyAsync(Stream body, int maxBodyBytes, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream(capacity: Math.Min(maxBodyBytes, buffer.Length));

        while (true)
        {
            var remaining = maxBodyBytes + 1 - (int)stream.Length;
            var read = await body.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), ct);
            if (read == 0)
            {
                break;
            }

            await stream.WriteAsync(buffer.AsMemory(0, read), ct);
            if (stream.Length > maxBodyBytes)
            {
                return null;
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static PaymentWebhookResponseDto MapResponse(ProcessWebhookResult result)
    {
        return new PaymentWebhookResponseDto
        {
            Status = result.Status,
            CorrelationId = result.CorrelationId,
            Reason = result.Reason,
            PaymentId = result.PaymentId,
            IsDuplicate = result.IsDuplicate
        };
    }
}
