using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>
    /// Daily (≈01:30 UTC) sweep that expires due loyalty points lots. Runs after the legacy
    /// stock ExpiryCheck (01:00) so the two daily passes don't contend. Idempotent and resumable.
    /// </summary>
    public sealed class LoyaltyExpirationBackgroundJob : IBackgroundJob
    {
        private const string JobKey = "Expiration";
        private static readonly TimeSpan DefaultRunAtUtc = new(1, 30, 0);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoyaltyJobSchedule _schedule;
        private readonly ILogger<LoyaltyExpirationBackgroundJob> _logger;
        private DateOnly? _lastCompletedRunDateUtc;

        public LoyaltyExpirationBackgroundJob(
            IServiceScopeFactory scopeFactory,
            ILoyaltyJobSchedule schedule,
            ILogger<LoyaltyExpirationBackgroundJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string JobName => "LoyaltyPointsExpiration";

        public async Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            if (!_schedule.IsEnabled(JobKey)) return;

            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);
            if (_lastCompletedRunDateUtc == todayUtc || nowUtc.TimeOfDay < _schedule.RunTimeUtc(JobKey, DefaultRunAtUtc))
                return;

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ILoyaltyExpirationJobService>();

            var result = await service.RunAsync(cancellationToken);
            if (result.SkippedBecauseAlreadyRunning) return;

            _lastCompletedRunDateUtc = todayUtc;
            if (result.LotsExpired > 0)
                _logger.LogInformation(
                    "[LoyaltyExpiration] Completed at {RunAtUtc}. Wallets={Wallets}, LotsExpired={Lots}",
                    nowUtc, result.WalletsProcessed, result.LotsExpired);
        }
    }
}
