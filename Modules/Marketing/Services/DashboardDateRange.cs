namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <summary>A resolved [From, ToExclusive) window for dashboard filtering. Pure / testable.</summary>
    public readonly record struct DashboardWindow(DateTime From, DateTime ToExclusive);

    /// <summary>
    /// Resolves a dashboard period keyword (today / week / month / year / custom / all) or an
    /// explicit custom range into a half-open UTC window. No EF, no I/O — fully unit-testable.
    /// </summary>
    public static class DashboardDateRange
    {
        public static DashboardWindow Resolve(string? period, DateTime? from, DateTime? to, DateTime nowUtc)
        {
            var today = nowUtc.Date;
            switch ((period ?? "all").Trim().ToLowerInvariant())
            {
                case "today":
                    return new DashboardWindow(today, today.AddDays(1));
                case "week": // rolling last 7 days, inclusive of today
                    return new DashboardWindow(today.AddDays(-6), today.AddDays(1));
                case "month": // calendar month
                    var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    return new DashboardWindow(monthStart, monthStart.AddMonths(1));
                case "year": // calendar year
                    var yearStart = new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    return new DashboardWindow(yearStart, yearStart.AddYears(1));
                case "custom":
                    var f = (from?.Date ?? today);
                    var t = (to?.Date ?? today).AddDays(1); // inclusive end day
                    if (t <= f) t = f.AddDays(1);
                    return new DashboardWindow(DateTime.SpecifyKind(f, DateTimeKind.Utc), DateTime.SpecifyKind(t, DateTimeKind.Utc));
                default: // "all" — wide-open window
                    return new DashboardWindow(DateTime.SpecifyKind(new DateTime(2000, 1, 1), DateTimeKind.Utc), today.AddDays(1));
            }
        }
    }
}
