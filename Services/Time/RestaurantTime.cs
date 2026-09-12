using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Time
{
    public static class RestaurantTimeZone
    {
        public static TimeZoneInfo Resolve(string? timeZoneId)
        {
            var resolvedId = string.IsNullOrWhiteSpace(timeZoneId)
                ? RestaurantTimeDefaults.TimeZoneId
                : timeZoneId.Trim();

            if (TryResolve(resolvedId, out var timeZone))
                return timeZone;

            return TryResolve(RestaurantTimeDefaults.TimeZoneId, out timeZone)
                ? timeZone
                : TimeZoneInfo.Utc;
        }

        public static bool IsValid(string? timeZoneId)
            => string.IsNullOrWhiteSpace(timeZoneId) || TryResolve(timeZoneId.Trim(), out _);

        private static bool TryResolve(string timeZoneId, out TimeZoneInfo timeZone)
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
                timeZone = TimeZoneInfo.Utc;
                return false;
            }
            catch (InvalidTimeZoneException)
            {
                timeZone = TimeZoneInfo.Utc;
                return false;
            }
        }
    }

    public sealed class RestaurantTimeContext
    {
        private readonly TimeZoneInfo _timeZone;

        public RestaurantTimeContext(DateTime utcNow, TimeZoneInfo timeZone)
        {
            _timeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));
            UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            LocalNow = TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _timeZone);
        }

        public DateTime UtcNow { get; }
        public DateTime LocalNow { get; }
        public DateOnly BusinessDate => DateOnly.FromDateTime(LocalNow);

        public RestaurantUtcRange GetDayRange(DateOnly businessDate)
        {
            var nextDate = businessDate.AddDays(1);
            return new RestaurantUtcRange(
                ToUtc(businessDate),
                ToUtc(nextDate));
        }

        private DateTime ToUtc(DateOnly date)
        {
            var localMidnight = DateTime.SpecifyKind(
                date.ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Unspecified);

            return TimeZoneInfo.ConvertTimeToUtc(localMidnight, _timeZone);
        }
    }

    public readonly record struct RestaurantUtcRange(DateTime StartUtc, DateTime EndUtc);
}
