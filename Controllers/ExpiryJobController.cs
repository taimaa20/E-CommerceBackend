using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    /// <summary>
    /// Idempotent expiry job — safe to run multiple times, produces same result.
    /// Intended to be triggered by a cron/scheduler with the server secret header.
    /// </summary>
    [Route("api/jobs/expiry-check")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    [EnableRateLimiting("api")]
    public class ExpiryJobController : ControllerBase
    {
        private const string ServerSecretHeader = "X-Job-Secret";

        private readonly IExpiryJobService _expiryJobService;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<ExpiryJobController> _logger;

        public ExpiryJobController(
            IExpiryJobService expiryJobService,
            IConfiguration configuration,
            IHostEnvironment environment,
            ILogger<ExpiryJobController> logger)
        {
            _expiryJobService = expiryJobService ?? throw new ArgumentNullException(nameof(expiryJobService));
            _configuration    = configuration    ?? throw new ArgumentNullException(nameof(configuration));
            _environment      = environment      ?? throw new ArgumentNullException(nameof(environment));
            _logger           = logger           ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost]
        public async Task<ActionResult> RunExpiryCheck(CancellationToken cancellationToken)
        {
            var expectedSecret = _configuration["Jobs:ExpiryCheckSecret"];
            if (string.IsNullOrEmpty(expectedSecret))
            {
                // Fail-closed in non-Development: a missing secret is a misconfiguration,
                // not an opt-out. In Development we tolerate it so local cron testing works.
                if (!_environment.IsDevelopment())
                {
                    _logger.LogError("Jobs:ExpiryCheckSecret is not configured — refusing to run expiry job");
                    return Forbid();
                }
            }
            else
            {
                var providedSecret = Request.Headers[ServerSecretHeader].FirstOrDefault() ?? string.Empty;
                var providedBytes = Encoding.UTF8.GetBytes(providedSecret);
                var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);

                // Constant-time compare — prevents timing-side-channel leak of the prefix.
                if (providedBytes.Length != expectedBytes.Length
                    || !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
                {
                    _logger.LogWarning("Expiry job triggered with invalid secret from {RemoteIp}",
                        HttpContext.Connection.RemoteIpAddress);
                    return Forbid();
                }
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _expiryJobService.RunAsync(cancellationToken);
            sw.Stop();

            _logger.LogInformation(
                "Expiry job completed in {ElapsedMs}ms: expired={Expired}, near={Near}, skipped={Skipped}",
                sw.ElapsedMilliseconds, result.ExpiredLogged, result.NearExpiryNotified, result.SkippedBecauseAlreadyRunning);

            return Ok(new
            {
                message = result.Message,
                expiredLogged = result.ExpiredLogged,
                nearExpiryNotified = result.NearExpiryNotified,
                skippedBecauseAlreadyRunning = result.SkippedBecauseAlreadyRunning
            });
        }
    }
}
