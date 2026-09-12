namespace RestaurantPos.Api.Modules.Dashboard.Predicates
{
    /// <summary>
    /// Half-open UTC interval <c>[Start, End)</c>. Used everywhere a dashboard
    /// query needs a date range so we never repeat the
    /// <c>endDate.Date.AddDays(1).AddTicks(-1)</c> drift seen in the legacy
    /// ReportService.
    /// </summary>
    public readonly record struct MetricWindow(DateTime StartUtc, DateTime EndUtc)
    {
        public TimeSpan Duration => EndUtc - StartUtc;

        /// <summary>Same-length window immediately preceding this one (for comparisons).</summary>
        public MetricWindow PreviousPeriod()
        {
            var dur = Duration;
            return new MetricWindow(StartUtc - dur, StartUtc);
        }

        /// <summary>Same-length window one calendar year earlier (for YoY comparisons).</summary>
        public MetricWindow PreviousYear()
        {
            return new MetricWindow(StartUtc.AddYears(-1), EndUtc.AddYears(-1));
        }

        public static MetricWindow FromDayUtc(DateTime utcDay)
        {
            var start = DateTime.SpecifyKind(utcDay.Date, DateTimeKind.Utc);
            return new MetricWindow(start, start.AddDays(1));
        }
    }
}
