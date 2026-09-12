using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>
    /// Daily (≈02:30 UTC) recomputation of dynamic segment membership. Runs after tier
    /// recalculation so tier-based rules see settled tiers. Idempotent and incremental.
    /// </summary>
    public sealed class SegmentComputationBackgroundJob : IBackgroundJob
    {
        private const string JobKey = "SegmentComputation";
        private static readonly TimeSpan DefaultRunAtUtc = new(2, 30, 0);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoyaltyJobSchedule _schedule;
        private readonly ILogger<SegmentComputationBackgroundJob> _logger;
        private DateOnly? _lastCompletedRunDateUtc;

        public SegmentComputationBackgroundJob(
            IServiceScopeFactory scopeFactory,
            ILoyaltyJobSchedule schedule,
            ILogger<SegmentComputationBackgroundJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string JobName => "LoyaltySegmentComputation";

        public async Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            if (!_schedule.IsEnabled(JobKey)) return;

            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);
            if (_lastCompletedRunDateUtc == todayUtc || nowUtc.TimeOfDay < _schedule.RunTimeUtc(JobKey, DefaultRunAtUtc))
                return;

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISegmentComputationJobService>();

            var result = await service.RunAsync(cancellationToken);
            if (result.SkippedBecauseAlreadyRunning) return;

            _lastCompletedRunDateUtc = todayUtc;
            if (result.SegmentsProcessed > 0)
                _logger.LogInformation(
                    "[SegmentComputation] Completed at {RunAtUtc}. Segments={Segments}, Added={Added}, Removed={Removed}",
                    nowUtc, result.SegmentsProcessed, result.MembersAdded, result.MembersRemoved);
        }
    }
}
