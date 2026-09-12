using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services.Printing.Agent
{
    /// <summary>
    /// HTTP client used by <see cref="LocalPrintAgentBackgroundService"/> to
    /// talk to the cloud's <c>/api/printing/agent/*</c> endpoints.
    /// Logs in once with a normal POS user (Admin role), caches the JWT,
    /// and transparently re-logins on a 401.
    /// </summary>
    public sealed class AgentApiClient
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly PrintAgentSettings _settings;
        private readonly ILogger<AgentApiClient> _logger;
        private readonly SemaphoreSlim _loginLock = new(1, 1);
        private string? _token;

        public AgentApiClient(HttpClient http, IOptions<PrintAgentSettings> settings, ILogger<AgentApiClient> logger)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
                throw new InvalidOperationException("PrintAgent:BaseUrl is required when PrintAgent:Enabled = true");

            _http.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
            // Tenant id is optional. When empty the backend's TenantResolver
            // falls back to its hardcoded default, which matches what every
            // existing frontend request already does.
            if (!string.IsNullOrWhiteSpace(_settings.TenantId))
            {
                _http.DefaultRequestHeaders.Add("X-Tenant-Id", _settings.TenantId);
            }
            _http.Timeout = TimeSpan.FromSeconds(20);
        }

        public async Task<List<AgentJobDto>> GetPendingJobsAsync(int take, CancellationToken ct)
        {
            return await SendWithAuthAsync<List<AgentJobDto>>(
                () => new HttpRequestMessage(HttpMethod.Get, $"api/printing/agent/jobs/pending?take={take}"),
                ct) ?? new List<AgentJobDto>();
        }

        public async Task<bool> ClaimAsync(Guid jobId, CancellationToken ct)
        {
            try
            {
                var response = await SendRawAsync(
                    () => new HttpRequestMessage(HttpMethod.Post, $"api/printing/agent/jobs/{jobId}/claim"),
                    ct);
                if (response.StatusCode == HttpStatusCode.Conflict) return false;
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Claim failed for {JobId}", jobId);
                return false;
            }
        }

        public async Task ReportResultAsync(Guid jobId, bool success, string? error, CancellationToken ct)
        {
            var payload = new AgentResultPayload { Success = success, Error = error };
            using var resp = await SendRawAsync(
                () => new HttpRequestMessage(HttpMethod.Post, $"api/printing/agent/jobs/{jobId}/result")
                {
                    Content = JsonContent.Create(payload, options: Json)
                },
                ct);
            // Best effort — don't throw if the cloud is briefly unreachable; the
            // cloud's own retry/backoff curve will eventually surface the row.
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Result POST returned {Status} for job {JobId}",
                    (int)resp.StatusCode, jobId);
            }
        }

        // ── auth ─────────────────────────────────────────────────────────

        private async Task EnsureAuthenticatedAsync(CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(_token)) return;
            await _loginLock.WaitAsync(ct);
            try
            {
                if (!string.IsNullOrEmpty(_token)) return;
                await LoginAsync(ct);
            }
            finally { _loginLock.Release(); }
        }

        private async Task LoginAsync(CancellationToken ct)
        {
            var login = new { username = _settings.Username, password = _settings.Password };
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
            {
                Content = JsonContent.Create(login, options: Json)
            };
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"Agent login failed: {(int)resp.StatusCode} {body}");
            }
            var payload = await resp.Content.ReadFromJsonAsync<AgentLoginResponse>(Json, ct)
                ?? throw new InvalidOperationException("Login response was empty");
            if (string.IsNullOrEmpty(payload.Token))
                throw new InvalidOperationException("Login response had no token");
            _token = payload.Token;
            _logger.LogInformation("Agent logged in as {Username}", _settings.Username);
        }

        private void InvalidateToken() => _token = null;

        private async Task<T?> SendWithAuthAsync<T>(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
        {
            using var resp = await SendRawAsync(requestFactory, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<T>(Json, ct);
        }

        private async Task<HttpResponseMessage> SendRawAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
        {
            await EnsureAuthenticatedAsync(ct);

            var req = requestFactory();
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var resp = await _http.SendAsync(req, ct);
            if (resp.StatusCode != HttpStatusCode.Unauthorized) return resp;

            // Token expired → re-login once and replay.
            resp.Dispose();
            InvalidateToken();
            await EnsureAuthenticatedAsync(ct);

            req = requestFactory();
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            return await _http.SendAsync(req, ct);
        }
    }
}
