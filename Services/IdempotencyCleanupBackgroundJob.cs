using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Runs <see cref="IIdempotencyCleanupJobService"/> once per day at 03:00 UTC.
    /// Placed after RefreshTokenCleanup (02:30) and before LowStockAlert /
    /// reporting jobs so the table is trimmed before any of them scan it.
    /// </summary>
    public class IdempotencyCleanupBackgroundJob : IBackgroundJob
    {
        private static readonly TimeSpan RunTimeUtc = new(3, 0, 0);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IdempotencyCleanupBackgroundJob> _logger;
        private DateOnly? _lastCompletedRunDateUtc;

        public IdempotencyCleanupBackgroundJob(
            IServiceScopeFactory scopeFactory,
            ILogger<IdempotencyCleanupBackgroundJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string JobName => "IdempotencyCleanup";

        public async Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);

            if (_lastCompletedRunDateUtc == todayUtc || nowUtc.TimeOfDay < RunTimeUtc)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IIdempotencyCleanupJobService>();

            var result = await service.RunAsync(cancellationToken);
            if (result.SkippedBecauseAlreadyRunning)
            {
                _logger.LogInformation("[IdempotencyCleanupBackgroundJob] Skipped — another instance holds the lock.");
                return;
            }

            _lastCompletedRunDateUtc = todayUtc;
            _logger.LogInformation(
                "[IdempotencyCleanupBackgroundJob] Completed at {RunAtUtc}. Deleted={Deleted} Batches={Batches}",
                nowUtc,
                result.EntriesDeleted,
                result.Batches);
        }
    }
}
