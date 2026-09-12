using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Runs <see cref="ILowStockAlertJobService"/> once per day at 06:00 UTC —
    /// late enough to be "morning" for most deployments so managers see the alert
    /// at the start of the operational day.
    /// </summary>
    public class LowStockAlertBackgroundJob : IBackgroundJob
    {
        private static readonly TimeSpan RunTimeUtc = new(6, 0, 0);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LowStockAlertBackgroundJob> _logger;
        private DateOnly? _lastCompletedRunDateUtc;

        public LowStockAlertBackgroundJob(
            IServiceScopeFactory scopeFactory,
            ILogger<LowStockAlertBackgroundJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string JobName => "LowStockAlert";

        public async Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);

            if (_lastCompletedRunDateUtc == todayUtc || nowUtc.TimeOfDay < RunTimeUtc)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ILowStockAlertJobService>();

            var result = await service.RunAsync(cancellationToken);
            if (result.SkippedBecauseAlreadyRunning)
            {
                _logger.LogInformation("[LowStockAlertBackgroundJob] Skipped — another instance holds the lock.");
                return;
            }

            _lastCompletedRunDateUtc = todayUtc;
            _logger.LogInformation(
                "[LowStockAlertBackgroundJob] Completed at {RunAtUtc}. Notified={Notified}",
                nowUtc,
                result.MaterialsNotified);
        }
    }
}
