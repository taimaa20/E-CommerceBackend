using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>
    /// Daily (≈03:00 UTC, configurable) campaign pass: auto-advances lifecycle and issues bonus
    /// points for qualifying orders/customers via the existing wallet engine. Distributed-safe.
    /// </summary>
    public sealed class CampaignProcessingBackgroundJob : IBackgroundJob
    {
        private const string JobKey = "CampaignProcessing";
        private static readonly TimeSpan DefaultRunAtUtc = new(3, 0, 0);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoyaltyJobSchedule _schedule;
        private readonly ILogger<CampaignProcessingBackgroundJob> _logger;
        private DateOnly? _lastCompletedRunDateUtc;

        public CampaignProcessingBackgroundJob(
            IServiceScopeFactory scopeFactory,
            ILoyaltyJobSchedule schedule,
            ILogger<CampaignProcessingBackgroundJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string JobName => "LoyaltyCampaignProcessing";

        public async Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            if (!_schedule.IsEnabled(JobKey)) return;

            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);
            if (_lastCompletedRunDateUtc == todayUtc || nowUtc.TimeOfDay < _schedule.RunTimeUtc(JobKey, DefaultRunAtUtc))
                return;

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICampaignProcessingJobService>();

            var result = await service.RunAsync(cancellationToken);
            if (result.SkippedBecauseAlreadyRunning) return;

            _lastCompletedRunDateUtc = todayUtc;
            if (result.AwardsIssued > 0 || result.CampaignsAdvanced > 0)
                _logger.LogInformation(
                    "[Campaign] Completed at {RunAtUtc}. Advanced={Advanced}, Processed={Processed}, Awards={Awards}, Points={Points}",
                    nowUtc, result.CampaignsAdvanced, result.CampaignsProcessed, result.AwardsIssued, result.PointsIssued);
        }
    }
}
