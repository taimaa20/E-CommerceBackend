using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>
    /// Approves due pending loyalty points. Runs daily at the configured time (default 00:30 UTC) and
    /// also catches Delayed-mode points as their delay elapses. Distributed-safe (advisory lock).
    /// </summary>
    public sealed class PendingApprovalBackgroundJob : IBackgroundJob
    {
        private const string JobKey = "PendingApproval";
        private static readonly TimeSpan DefaultRunAtUtc = new(0, 30, 0);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoyaltyJobSchedule _schedule;
        private readonly ILogger<PendingApprovalBackgroundJob> _logger;
        private DateOnly? _lastCompletedRunDateUtc;

        public PendingApprovalBackgroundJob(
            IServiceScopeFactory scopeFactory,
            ILoyaltyJobSchedule schedule,
            ILogger<PendingApprovalBackgroundJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string JobName => "LoyaltyPendingApproval";

        public async Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            if (!_schedule.IsEnabled(JobKey)) return;

            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);
            if (_lastCompletedRunDateUtc == todayUtc || nowUtc.TimeOfDay < _schedule.RunTimeUtc(JobKey, DefaultRunAtUtc))
                return;

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPendingApprovalJobService>();

            var result = await service.RunAsync(cancellationToken);
            if (result.SkippedBecauseAlreadyRunning) return; // let the holder finish; retry next tick

            _lastCompletedRunDateUtc = todayUtc;
            if (result.Approved > 0)
                _logger.LogInformation(
                    "[PendingApproval] Completed at {RunAtUtc}. Approved={Approved}, Points={Points}",
                    nowUtc, result.Approved, result.PointsApproved);
        }
    }
}
