using RestaurantPos.Api.Modules.Dashboard.Predicates;

namespace RestaurantPos.Api.Modules.Dashboard.Filters
{
    /// <summary>
    /// Pure helpers that translate a <see cref="DashboardFilterDto"/> +
    /// per-dashboard default preset into an absolute UTC
    /// <see cref="MetricWindow"/>. No service calls, no allocations beyond
    /// the returned struct — safe to use from middleware.
    /// </summary>
    public static class DashboardFilterResolver
    {
        // 366 days cap — protects calculators from accidental "all time"
        // sweeps that would blow up memory on a 5y-old tenant.
        public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(366);

        public static MetricWindow Resolve(DashboardFilterDto filter, DashboardFilterPreset fallback)
        {
            var tz   = ResolveTimeZone(filter.TimeZone);
            var preset = filter.Preset == DashboardFilterPreset.Default ? fallback : filter.Preset;

            if (preset == DashboardFilterPreset.Custom)
                return ResolveCustom(filter);

            var nowUtc = DateTime.UtcNow;
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var todayLocal = nowLocal.Date;

            DateTime startLocal;
            DateTime endLocal;

            switch (preset)
            {
                case DashboardFilterPreset.Today:
                    startLocal = todayLocal;
                    endLocal   = todayLocal.AddDays(1);
                    break;
                case DashboardFilterPreset.Yesterday:
                    startLocal = todayLocal.AddDays(-1);
                    endLocal   = todayLocal;
                    break;
                case DashboardFilterPreset.ThisWeek:
                    // ISO week — Monday start.
                    var diff = ((int)todayLocal.DayOfWeek + 6) % 7;
                    startLocal = todayLocal.AddDays(-diff);
                    endLocal   = startLocal.AddDays(7);
                    break;
                case DashboardFilterPreset.ThisMonth:
                    startLocal = new DateTime(todayLocal.Year, todayLocal.Month, 1);
                    endLocal   = startLocal.AddMonths(1);
                    break;
                case DashboardFilterPreset.LastMonth:
                    var firstOfThisMonth = new DateTime(todayLocal.Year, todayLocal.Month, 1);
                    startLocal = firstOfThisMonth.AddMonths(-1);
                    endLocal   = firstOfThisMonth;
                    break;
                case DashboardFilterPreset.ThisQuarter:
                    var qMonth = ((todayLocal.Month - 1) / 3) * 3 + 1;
                    startLocal = new DateTime(todayLocal.Year, qMonth, 1);
                    endLocal   = startLocal.AddMonths(3);
                    break;
                case DashboardFilterPreset.ThisYear:
                    startLocal = new DateTime(todayLocal.Year, 1, 1);
                    endLocal   = startLocal.AddYears(1);
                    break;
                case DashboardFilterPreset.Last7:
                    startLocal = todayLocal.AddDays(-6);
                    endLocal   = todayLocal.AddDays(1);
                    break;
                case DashboardFilterPreset.Last30:
                    startLocal = todayLocal.AddDays(-29);
                    endLocal   = todayLocal.AddDays(1);
                    break;
                default:
                    startLocal = todayLocal;
                    endLocal   = todayLocal.AddDays(1);
                    break;
            }

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            var endUtc   = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
            return Clamp(new MetricWindow(startUtc, endUtc));
        }

        private static MetricWindow ResolveCustom(DashboardFilterDto filter)
        {
            if (!filter.StartUtc.HasValue || !filter.EndUtc.HasValue)
                throw new ArgumentException("Custom preset requires StartUtc and EndUtc.");

            var start = DateTime.SpecifyKind(filter.StartUtc.Value, DateTimeKind.Utc);
            var end   = DateTime.SpecifyKind(filter.EndUtc.Value,   DateTimeKind.Utc);

            if (end <= start)
                throw new ArgumentException("Custom range end must be greater than start.");

            return Clamp(new MetricWindow(start, end));
        }

        private static MetricWindow Clamp(MetricWindow w)
        {
            if (w.Duration > MaxWindow)
                throw new ArgumentException($"Dashboard window cannot exceed {MaxWindow.TotalDays:0} days.");
            return w;
        }

        private static TimeZoneInfo ResolveTimeZone(string? tz)
        {
            if (string.IsNullOrWhiteSpace(tz))
                return TimeZoneInfo.Utc;

            try { return TimeZoneInfo.FindSystemTimeZoneById(tz); }
            catch { return TimeZoneInfo.Utc; }
        }
    }
}
