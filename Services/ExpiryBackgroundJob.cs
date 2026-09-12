using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public class ExpiryBackgroundJob : IBackgroundJob
    {
        private static readonly TimeSpan DefaultRunTimeUtc = new(1, 0, 0);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExpiryBackgroundJob> _logger;
        private DateOnly? _lastCompletedRunDateUtc;

        public ExpiryBackgroundJob(
            IServiceScopeFactory scopeFactory,
            ILogger<ExpiryBackgroundJob> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string JobName => "ExpiryCheck";

        public async Task RunIfDueAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var expiryJobService = scope.ServiceProvider.GetRequiredService<IExpiryJobService>();

            var settings = await settingsService.GetSettingsAsync(cancellationToken: cancellationToken);
            var runTimeUtc = ParseRunTimeUtc(settings.ExpiryJobRunTime);
            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);

            if (_lastCompletedRunDateUtc == todayUtc || nowUtc.TimeOfDay < runTimeUtc)
            {
                return;
            }

            var result = await expiryJobService.RunAsync(cancellationToken);
            if (result.SkippedBecauseAlreadyRunning)
            {
                _logger.LogInformation("[ExpiryBackgroundJob] Skipped because another instance is already processing the job.");
                return;
            }

            _lastCompletedRunDateUtc = todayUtc;
            _logger.LogInformation(
                "[ExpiryBackgroundJob] Completed at {RunAtUtc}. ExpiredLogged={ExpiredLogged}, NearExpiryNotified={NearExpiryNotified}",
                nowUtc,
                result.ExpiredLogged,
                result.NearExpiryNotified);
        }

        private TimeSpan ParseRunTimeUtc(string? rawValue)
        {
            if (!string.IsNullOrWhiteSpace(rawValue)
                && TimeSpan.TryParseExact(rawValue, @"hh\:mm", CultureInfo.InvariantCulture, out var parsed)
                && parsed >= TimeSpan.Zero
                && parsed < TimeSpan.FromDays(1))
            {
                return parsed;
            }

            if (!string.IsNullOrWhiteSpace(rawValue))
            {
                _logger.LogWarning("[ExpiryBackgroundJob] Invalid ExpiryJobRunTime '{ExpiryJobRunTime}'. Falling back to 01:00 UTC.", rawValue);
            }

            return DefaultRunTimeUtc;
        }
    }
}
