using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Controllers
{
    /// <summary>
    /// Manual triggers for the nightly/periodic maintenance jobs.
    /// ExpiryCheck has its own controller (ExpiryJobController) — this one owns everything added later.
    /// Each endpoint is idempotent, gated by Admin/Manager role, and optionally by an X-Job-Secret header
    /// (set via Jobs:* config) so an external scheduler can call them without a user session.
    /// </summary>
    [Route("api/jobs")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public class JobsController : ControllerBase
    {
        private const string ServerSecretHeader = "X-Job-Secret";

        private readonly IRefreshTokenCleanupJobService _refreshTokenCleanup;
        private readonly IStaleCashierShiftAlertJobService _staleShiftAlert;
        private readonly ILowStockAlertJobService _lowStockAlert;
        private readonly INotificationCleanupJobService _notificationCleanup;
        private readonly IConfiguration _configuration;

        public JobsController(
            IRefreshTokenCleanupJobService refreshTokenCleanup,
            IStaleCashierShiftAlertJobService staleShiftAlert,
            ILowStockAlertJobService lowStockAlert,
            INotificationCleanupJobService notificationCleanup,
            IConfiguration configuration)
        {
            _refreshTokenCleanup = refreshTokenCleanup ?? throw new ArgumentNullException(nameof(refreshTokenCleanup));
            _staleShiftAlert = staleShiftAlert ?? throw new ArgumentNullException(nameof(staleShiftAlert));
            _lowStockAlert = lowStockAlert ?? throw new ArgumentNullException(nameof(lowStockAlert));
            _notificationCleanup = notificationCleanup ?? throw new ArgumentNullException(nameof(notificationCleanup));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpPost("refresh-token-cleanup")]
        public async Task<ActionResult> RunRefreshTokenCleanup(CancellationToken cancellationToken)
        {
            if (!SecretOk("Jobs:RefreshTokenCleanupSecret")) return Forbid();

            var result = await _refreshTokenCleanup.RunAsync(cancellationToken);
            return Ok(new
            {
                message = result.Message,
                tokensDeleted = result.TokensDeleted,
                skippedBecauseAlreadyRunning = result.SkippedBecauseAlreadyRunning
            });
        }

        [HttpPost("stale-shift-alert")]
        public async Task<ActionResult> RunStaleShiftAlert(CancellationToken cancellationToken)
        {
            if (!SecretOk("Jobs:StaleShiftAlertSecret")) return Forbid();

            var result = await _staleShiftAlert.RunAsync(cancellationToken);
            return Ok(new
            {
                message = result.Message,
                shiftsAlerted = result.ShiftsAlerted,
                skippedBecauseAlreadyRunning = result.SkippedBecauseAlreadyRunning
            });
        }

        [HttpPost("low-stock-alert")]
        public async Task<ActionResult> RunLowStockAlert(CancellationToken cancellationToken)
        {
            if (!SecretOk("Jobs:LowStockAlertSecret")) return Forbid();

            var result = await _lowStockAlert.RunAsync(cancellationToken);
            return Ok(new
            {
                message = result.Message,
                materialsNotified = result.MaterialsNotified,
                skippedBecauseAlreadyRunning = result.SkippedBecauseAlreadyRunning
            });
        }

        [HttpPost("notification-cleanup")]
        public async Task<ActionResult> RunNotificationCleanup(CancellationToken cancellationToken)
        {
            if (!SecretOk("Jobs:NotificationCleanupSecret")) return Forbid();

            var result = await _notificationCleanup.RunAsync(cancellationToken);
            return Ok(new
            {
                message = result.Message,
                notificationsDeleted = result.NotificationsDeleted,
                skippedBecauseAlreadyRunning = result.SkippedBecauseAlreadyRunning
            });
        }

        private bool SecretOk(string configKey)
        {
            var expected = _configuration[configKey];
            if (string.IsNullOrEmpty(expected)) return true; // secret not configured → role auth is enough

            var provided = Request.Headers[ServerSecretHeader].FirstOrDefault();
            return provided == expected;
        }
    }
}
