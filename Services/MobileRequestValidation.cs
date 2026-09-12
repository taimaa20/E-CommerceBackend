using System.Globalization;
using RestaurantPos.Api.DTOs.MobileRequests;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public static class MobileRequestValidation
    {
        public const int DefaultPage = 1;
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;

        public static MobileRequestFilter BuildFilter(MobileRequestListQuery query)
        {
            var status = NormalizeOptionalCode(query.Status, "status", MobileRequestStatuses.All);
            var from = ParseOptionalDate(query.From, "from");
            var to = ParseOptionalDate(query.To, "to");

            if (from.HasValue && to.HasValue && from > to)
            {
                throw new MobileRequestValidationException("from must be before or equal to to.");
            }

            return new MobileRequestFilter(status, from, to, NormalizePage(query.Page), NormalizePageSize(query.PageSize));
        }

        public static string RequireCode(string? value, string field, IReadOnlySet<string> allowed)
        {
            var normalized = NormalizeCode(value, field);
            if (!allowed.Contains(normalized))
            {
                throw new MobileRequestValidationException($"{field} is invalid.");
            }

            return normalized;
        }

        public static string? NormalizeOptionalCode(string? value, string field, IReadOnlySet<string> allowed)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return RequireCode(value, field, allowed);
        }

        public static DateOnly RequireDate(string? value, string field)
            => ParseOptionalDate(value, field)
                ?? throw new MobileRequestValidationException($"{field} is required.");

        public static TimeOnly RequireTime(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new MobileRequestValidationException($"{field} is required.");
            }

            if (!TimeOnly.TryParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                throw new MobileRequestValidationException($"{field} must use HH:mm format.");
            }

            return time;
        }

        public static string? TrimAndCap(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        public static string FormatDate(DateOnly date)
            => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static string FormatTime(TimeOnly time)
            => time.ToString("HH:mm", CultureInfo.InvariantCulture);

        public static string FormatDuration(int minutes)
            => $"{minutes / 60:D2}:{minutes % 60:D2}";

        private static DateOnly? ParseOptionalDate(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                throw new MobileRequestValidationException($"{field} must use YYYY-MM-DD format.");
            }

            return date;
        }

        private static string NormalizeCode(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new MobileRequestValidationException($"{field} is required.");
            }

            return value.Trim().ToLowerInvariant();
        }

        private static int NormalizePage(int? page)
        {
            var value = page.GetValueOrDefault(DefaultPage);
            return value < DefaultPage ? DefaultPage : value;
        }

        private static int NormalizePageSize(int? pageSize)
        {
            var value = pageSize.GetValueOrDefault(DefaultPageSize);
            return value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
        }
    }
}
