using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Runs <see cref="IRefreshTokenCleanupJobService"/> once per day at 02:30 UTC —
    /// scheduled after ExpiryCheck (01:00) but still in the quiet window before traffic.
    /// </summary>
    public class RefreshTokenCleanupBackgroundJob : IBackgroundJob
    {
        private static readonly TimeSpan RunTimeUtc = new(2, 30, 0);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RefreshTokenCleanupBackgroundJob> _logger;
        private DateOnly? _lastCompletedRunDateUtc;

        public RefreshTokenCleanupBackgroundJob(
            IServiceScopeFactory scopeFactory,
            ILogger<RefreshTokenCleanupBackgroundJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string JobName => "RefreshTokenCleanup";

        public async Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);

            if (_lastCompletedRunDateUtc == todayUtc || nowUtc.TimeOfDay < RunTimeUtc)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenCleanupJobService>();

            var result = await service.RunAsync(cancellationToken);
            if (result.SkippedBecauseAlreadyRunning)
            {
                _logger.LogInformation("[RefreshTokenCleanupBackgroundJob] Skipped — another instance holds the lock.");
                return;
            }

            _lastCompletedRunDateUtc = todayUtc;
            _logger.LogInformation(
                "[RefreshTokenCleanupBackgroundJob] Completed at {RunAtUtc}. Deleted={Deleted}",
                nowUtc,
                result.TokensDeleted);
        }
    }
}
