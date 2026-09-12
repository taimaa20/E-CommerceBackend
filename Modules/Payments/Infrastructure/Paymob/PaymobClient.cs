using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob.Contracts;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob;

public sealed class PaymobClient : IPaymobClient
{
    public const string HttpClientName = "Payments.Paymob";
    public const string ProviderCode = "paymob";
    public const string CorrelationHeaderName = "X-Correlation-Id";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<PaymobOptions> _options;
    private readonly ILogger<PaymobClient> _logger;
    private readonly IHostEnvironment? _environment;

    public PaymobClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<PaymobOptions> options,
        ILogger<PaymobClient> logger,
        IHostEnvironment? environment = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment;
    }

    public async Task<PaymobCreateIntentionResponseDto> CreatePaymentIntentionAsync(
        PaymobCreateIntentionRequestDto request,
        string correlationId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCorrelationId(correlationId);

        var options = _options.CurrentValue;
        ValidateConfigured(options);

        _logger.LogInformation(
            "Creating Paymob payment intention. Amount {Amount}. Currency {Currency}. CorrelationId {CorrelationId}",
            request.Amount,
            request.Currency,
            correlationId);

        var result = await SendCreateIntentionAsync(request, options, correlationId, ct);
        LogDevelopmentDiagnostics(request, result, correlationId);

        return result;
    }

    private void LogDevelopmentDiagnostics(
        PaymobCreateIntentionRequestDto request,
        PaymobCreateIntentionResponseDto response,
        string correlationId)
    {
        if (_environment?.IsDevelopment() != true)
        {
            return;
        }

        _logger.LogInformation(
            "Paymob development diagnostics. RequestedAmount {RequestedAmount}. RequestedCurrency {RequestedCurrency}. ReturnedAmount {ReturnedAmount}. ReturnedCurrency {ReturnedCurrency}. IntentionDetailAmount {IntentionDetailAmount}. IntentionDetailCurrency {IntentionDetailCurrency}. AmountProperties {AmountProperties}. SpecialReference {SpecialReference}. HasClientSecret {HasClientSecret}. CorrelationId {CorrelationId}",
            request.Amount,
            request.Currency,
            response.Amount,
            response.Currency,
            response.IntentionDetail?.Amount,
            response.IntentionDetail?.Currency,
            DescribeAmountProperties(response.ExtensionData),
            request.SpecialReference,
            !string.IsNullOrWhiteSpace(response.ClientSecret),
            correlationId);
    }

    private static string DescribeAmountProperties(IDictionary<string, JsonElement>? extensionData)
    {
        if (extensionData is null || extensionData.Count == 0)
        {
            return "none";
        }

        var described = string.Join(", ", extensionData
            .Where(entry => entry.Key.Contains("amount", StringComparison.OrdinalIgnoreCase))
            .Select(entry => $"{entry.Key}={entry.Value.GetRawText()}"));

        return described.Length > 0 ? described : "none";
    }

    private async Task<PaymobCreateIntentionResponseDto> SendCreateIntentionAsync(
        PaymobCreateIntentionRequestDto request,
        PaymobOptions options,
        string correlationId,
        CancellationToken ct)
    {
        using var httpRequest = CreateHttpRequest(request, options, correlationId);

        try
        {
            using var response = await CreateClient().SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            return await ReadResponseAsync(response, options, correlationId, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new PaymentGatewayTimeoutException(ProviderCode, "Paymob request timed out.", correlationId, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new PaymentGatewayUnavailableException(ProviderCode, "Paymob is unavailable.", correlationId, ex.StatusCode, ex);
        }
    }

    private HttpRequestMessage CreateHttpRequest(
        PaymobCreateIntentionRequestDto request,
        PaymobOptions options,
        string correlationId)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.CreateIntentionPath)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Token", options.SecretKey);
        httpRequest.Headers.TryAddWithoutValidation(CorrelationHeaderName, correlationId);

        return httpRequest;
    }

    private async Task<PaymobCreateIntentionResponseDto> ReadResponseAsync(
        HttpResponseMessage response,
        PaymobOptions options,
        string correlationId,
        CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await MapFailureAsync(response, options, correlationId, ct);
        }

        var result = await response.Content.ReadFromJsonAsync<PaymobCreateIntentionResponseDto>(JsonOptions, ct);
        if (result is null || string.IsNullOrWhiteSpace(result.ClientSecret))
        {
            throw new PaymentGatewayException(ProviderCode, "Paymob response was missing the client secret.", correlationId, response.StatusCode);
        }

        _logger.LogInformation(
            "Created Paymob payment intention. GatewayReference {GatewayReference}. Status {GatewayStatus}. CorrelationId {CorrelationId}",
            result.Id ?? result.IntentionOrderId?.ToString(),
            result.Status,
            correlationId);

        return result;
    }

    private async Task<PaymentGatewayException> MapFailureAsync(
        HttpResponseMessage response,
        PaymobOptions options,
        string correlationId,
        CancellationToken ct)
    {
        var bodyPreview = await ReadBodyPreviewAsync(response.Content, options.MaxErrorBodyLogLength, ct);
        var errorCode = TryReadErrorCode(bodyPreview);

        _logger.LogWarning(
            "Paymob payment intention failed. StatusCode {StatusCode}. GatewayErrorCode {GatewayErrorCode}. CorrelationId {CorrelationId}",
            (int)response.StatusCode,
            errorCode,
            correlationId);

        if (_environment?.IsDevelopment() == true)
        {
            _logger.LogWarning(
                "Paymob development error response. StatusCode {StatusCode}. GatewayErrorCode {GatewayErrorCode}. CorrelationId {CorrelationId}. ResponseBody {ResponseBody}",
                (int)response.StatusCode,
                errorCode,
                correlationId,
                bodyPreview);
        }

        var diagnosticSuffix = _environment?.IsDevelopment() == true
            ? $" HTTP {(int)response.StatusCode}. Gateway error code: {errorCode ?? "none"}. Response: {bodyPreview}"
            : string.Empty;

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                new PaymentGatewayValidationException(ProviderCode, $"Paymob rejected the payment intention request.{diagnosticSuffix}", correlationId, response.StatusCode),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new PaymentGatewayAuthenticationException(ProviderCode, $"Paymob authentication failed.{diagnosticSuffix}", correlationId, response.StatusCode),
            HttpStatusCode.RequestTimeout =>
                new PaymentGatewayTimeoutException(ProviderCode, $"Paymob request timed out.{diagnosticSuffix}", correlationId),
            HttpStatusCode.TooManyRequests => new PaymentGatewayUnavailableException(ProviderCode, $"Paymob rate limit was reached.{diagnosticSuffix}", correlationId, response.StatusCode),
            _ when (int)response.StatusCode >= 500 =>
                new PaymentGatewayUnavailableException(ProviderCode, $"Paymob is unavailable.{diagnosticSuffix}", correlationId, response.StatusCode),
            _ => new PaymentGatewayException(ProviderCode, $"Paymob request failed.{diagnosticSuffix}", correlationId, response.StatusCode)
        };
    }

    private HttpClient CreateClient()
    {
        return _httpClientFactory.CreateClient(HttpClientName);
    }

    private static void ValidateConfigured(PaymobOptions options)
    {
        if (!options.Enabled)
        {
            throw new PaymentGatewayConfigurationException(ProviderCode, "Paymob is disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            throw new PaymentGatewayConfigurationException(ProviderCode, "Paymob secret key is not configured.");
        }
    }

    private static void ValidateCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new PaymentGatewayValidationException(ProviderCode, "CorrelationId is required.", null);
        }
    }

    private static async Task<string> ReadBodyPreviewAsync(HttpContent content, int maxLength, CancellationToken ct)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        await using var stream = await content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var buffer = new char[maxLength];
        var read = await reader.ReadBlockAsync(buffer.AsMemory(0, maxLength), ct);

        return new string(buffer, 0, read);
    }

    private static string? TryReadErrorCode(string bodyPreview)
    {
        if (string.IsNullOrWhiteSpace(bodyPreview))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PaymobErrorResponseDto>(bodyPreview, JsonOptions)?.Code;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
