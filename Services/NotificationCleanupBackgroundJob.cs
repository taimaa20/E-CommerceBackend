using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Runs <see cref="INotificationCleanupJobService"/> once per week
    /// on Sunday at 03:30 UTC — daily purges aren't needed, weekly keeps the table
    /// trimmed without burning query capacity during peak hours.
    /// </summary>
    public class NotificationCleanupBackgroundJob : IBackgroundJob
    {
        private static readonly TimeSpan RunTimeUtc = new(3, 30, 0);
        private const DayOfWeek RunDay = DayOfWeek.Sunday;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationCleanupBackgroundJob> _logger;
        private DateOnly? _lastCompletedRunDateUtc;

        public NotificationCleanupBackgroundJob(
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationCleanupBackgroundJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string JobName => "NotificationCleanup";

        public async Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);

            if (_lastCompletedRunDateUtc == todayUtc
                || nowUtc.DayOfWeek != RunDay
                || nowUtc.TimeOfDay < RunTimeUtc)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<INotificationCleanupJobService>();

            var result = await service.RunAsync(cancellationToken);
            if (result.SkippedBecauseAlreadyRunning)
            {
                _logger.LogInformation("[NotificationCleanupBackgroundJob] Skipped — another instance holds the lock.");
                return;
            }

            _lastCompletedRunDateUtc = todayUtc;
            _logger.LogInformation(
                "[NotificationCleanupBackgroundJob] Completed at {RunAtUtc}. Deleted={Deleted}",
                nowUtc,
                result.NotificationsDeleted);
        }
    }
}
