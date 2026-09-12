using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>
    /// Config-driven schedule for the loyalty background jobs. Run-times and enable flags live under
    /// <c>Loyalty:Jobs:&lt;JobKey&gt;</c> in appsettings, so operators can retime or disable a job
    /// without a code change. Future loyalty jobs reuse this by passing their own key.
    /// <code>
    /// "Loyalty": { "Jobs": {
    ///   "Expiration":        { "RunTime": "01:30", "Enabled": true },
    ///   "TierRecalculation": { "RunTime": "02:00", "Enabled": true },
    ///   "SegmentComputation":{ "RunTime": "02:30", "Enabled": true },
    ///   "PendingApproval":   { "RunTime": "00:30", "Enabled": true }
    /// } }
    /// </code>
    /// </summary>
    public interface ILoyaltyJobSchedule
    {
        TimeSpan RunTimeUtc(string jobKey, TimeSpan fallback);
        bool IsEnabled(string jobKey, bool fallback = true);
    }

    public sealed class LoyaltyJobSchedule : ILoyaltyJobSchedule
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoyaltyJobSchedule> _logger;

        public LoyaltyJobSchedule(IConfiguration configuration, ILogger<LoyaltyJobSchedule> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public TimeSpan RunTimeUtc(string jobKey, TimeSpan fallback)
        {
            var raw = _configuration[$"Loyalty:Jobs:{jobKey}:RunTime"];
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            if (TimeSpan.TryParseExact(raw, @"hh\:mm", CultureInfo.InvariantCulture, out var parsed)
                && parsed >= TimeSpan.Zero && parsed < TimeSpan.FromDays(1))
                return parsed;

            _logger.LogWarning("[LoyaltyJobSchedule] Invalid RunTime '{Raw}' for {JobKey}; using {Fallback}.", raw, jobKey, fallback);
            return fallback;
        }

        public bool IsEnabled(string jobKey, bool fallback = true)
            => _configuration.GetValue<bool?>($"Loyalty:Jobs:{jobKey}:Enabled") ?? fallback;
    }
}
