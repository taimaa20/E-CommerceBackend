using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>
    /// Daily (≈02:00 UTC) tier recalculation pass. Runs after points expiration (01:30) so tiers
    /// reflect post-expiry balances. Idempotent — only writes when a customer's tier actually changes.
    /// </summary>
    public sealed class TierRecalculationBackgroundJob : IBackgroundJob
    {
        private const string JobKey = "TierRecalculation";
        private static readonly TimeSpan DefaultRunAtUtc = new(2, 0, 0);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoyaltyJobSchedule _schedule;
        private readonly ILogger<TierRecalculationBackgroundJob> _logger;
        private DateOnly? _lastCompletedRunDateUtc;

        public TierRecalculationBackgroundJob(
            IServiceScopeFactory scopeFactory,
            ILoyaltyJobSchedule schedule,
            ILogger<TierRecalculationBackgroundJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string JobName => "LoyaltyTierRecalculation";

        public async Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            if (!_schedule.IsEnabled(JobKey)) return;

            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);
            if (_lastCompletedRunDateUtc == todayUtc || nowUtc.TimeOfDay < _schedule.RunTimeUtc(JobKey, DefaultRunAtUtc))
                return;

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ITierRecalculationJobService>();

            var result = await service.RunAsync(cancellationToken);
            if (result.SkippedBecauseAlreadyRunning) return;

            _lastCompletedRunDateUtc = todayUtc;
            if (result.TierChanges > 0)
                _logger.LogInformation(
                    "[TierRecalc] Completed at {RunAtUtc}. Evaluated={Evaluated}, Changes={Changes}",
                    nowUtc, result.WalletsEvaluated, result.TierChanges);
        }
    }
}
