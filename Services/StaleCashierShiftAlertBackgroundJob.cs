using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Hourly check for cashier shifts left open past the stale threshold.
    /// Hourly cadence is chosen because a shift can be forgotten at any time —
    /// a once-daily check could wait up to 24h to surface an issue.
    /// </summary>
    public class StaleCashierShiftAlertBackgroundJob : IBackgroundJob
    {
        private static readonly TimeSpan RunEvery = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<StaleCashierShiftAlertBackgroundJob> _logger;
        private DateTime? _lastCompletedRunUtc;

        public StaleCashierShiftAlertBackgroundJob(
            IServiceScopeFactory scopeFactory,
            ILogger<StaleCashierShiftAlertBackgroundJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string JobName => "StaleCashierShiftAlert";

        public async Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;

            if (_lastCompletedRunUtc.HasValue && (nowUtc - _lastCompletedRunUtc.Value) < RunEvery)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IStaleCashierShiftAlertJobService>();

            var result = await service.RunAsync(cancellationToken);
            if (result.SkippedBecauseAlreadyRunning)
            {
                _logger.LogInformation("[StaleCashierShiftAlertBackgroundJob] Skipped — another instance holds the lock.");
                return;
            }

            _lastCompletedRunUtc = nowUtc;
            if (result.ShiftsAlerted > 0)
            {
                _logger.LogWarning(
                    "[StaleCashierShiftAlertBackgroundJob] Completed at {RunAtUtc}. Alerted={Alerted}",
                    nowUtc,
                    result.ShiftsAlerted);
            }
        }
    }
}
