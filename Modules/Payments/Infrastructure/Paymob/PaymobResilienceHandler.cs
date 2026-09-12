using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob;

public sealed class PaymobResilienceHandler : DelegatingHandler
{
    private const int MaxRetryDelayMilliseconds = 30000;

    private readonly IOptionsMonitor<PaymobOptions> _options;
    private readonly ILogger<PaymobResilienceHandler> _logger;

    public PaymobResilienceHandler(
        IOptionsMonitor<PaymobOptions> options,
        ILogger<PaymobResilienceHandler> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var options = _options.CurrentValue;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await SendCloneAsync(request, ct);
                if (!ShouldRetry(response) || attempt >= options.MaxRetryAttempts)
                {
                    return response;
                }

                response.Dispose();
                await DelayBeforeRetryAsync(attempt, options, GetCorrelationId(request), ct);
            }
            catch (Exception ex) when (ShouldRetryException(ex, ct) && attempt < options.MaxRetryAttempts)
            {
                await DelayBeforeRetryAsync(attempt, options, GetCorrelationId(request), ct, ex);
            }
        }
    }

    private async Task<HttpResponseMessage> SendCloneAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var clone = await CloneRequestAsync(request, ct);
        return await base.SendAsync(clone, ct);
    }

    private async Task DelayBeforeRetryAsync(
        int attempt,
        PaymobOptions options,
        string? correlationId,
        CancellationToken ct,
        Exception? exception = null)
    {
        var delay = CalculateDelay(attempt, options);
        _logger.LogWarning(
            exception,
            "Retrying Paymob HTTP request. Attempt {Attempt} of {MaxAttempts}. DelayMs {DelayMs}. CorrelationId {CorrelationId}",
            attempt + 1,
            options.MaxRetryAttempts,
            delay.TotalMilliseconds,
            correlationId);

        await Task.Delay(delay, ct);
    }

    private static TimeSpan CalculateDelay(int attempt, PaymobOptions options)
    {
        var exponential = options.RetryBaseDelayMilliseconds * Math.Pow(2, attempt);
        var jitter = Random.Shared.Next(0, options.RetryBaseDelayMilliseconds);
        var delay = Math.Min(exponential + jitter, MaxRetryDelayMilliseconds);

        return TimeSpan.FromMilliseconds(delay);
    }

    private static bool ShouldRetry(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;

        return statusCode is 408 or 429 || statusCode >= 500;
    }

    private static bool ShouldRetryException(Exception exception, CancellationToken ct)
    {
        return exception is HttpRequestException ||
            exception is TaskCanceledException && !ct.IsCancellationRequested;
    }

    private static string? GetCorrelationId(HttpRequestMessage request)
    {
        return request.Headers.TryGetValues(PaymobClient.CorrelationHeaderName, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        CopyHeaders(request.Headers, clone.Headers);

        if (request.Content is not null)
        {
            var content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(ct));
            CopyHeaders(request.Content.Headers, content.Headers);
            clone.Content = content;
        }

        return clone;
    }

    private static void CopyHeaders(HttpHeaders source, HttpHeaders target)
    {
        foreach (var header in source)
        {
            target.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}
