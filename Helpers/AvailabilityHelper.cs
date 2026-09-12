namespace RestaurantPos.Api.Helpers
{
    public static class AvailabilityHelper
    {
        public static bool IsAvailableNow(
            DateOnly? startDate,
            DateOnly? endDate,
            TimeOnly? startTime,
            TimeOnly? endTime)
            => IsAvailableAt(startDate, endDate, startTime, endTime, DateTime.Now);

        public static bool IsAvailableNow(
            DateOnly? startDate,
            DateOnly? endDate,
            TimeOnly? startTime,
            TimeOnly? endTime,
            TimeZoneInfo timeZone)
            => IsAvailableAt(
                startDate,
                endDate,
                startTime,
                endTime,
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));

        public static bool IsAvailableAt(
            DateOnly? startDate,
            DateOnly? endDate,
            TimeOnly? startTime,
            TimeOnly? endTime,
            DateTime localNow)
        {
            var today = DateOnly.FromDateTime(localNow);
            var now = TimeOnly.FromDateTime(localNow);

            if (!startTime.HasValue || !endTime.HasValue)
                return IsDateInRange(today, startDate, endDate);

            if (startTime.Value <= endTime.Value)
                return IsDateInRange(today, startDate, endDate)
                    && now >= startTime.Value
                    && now <= endTime.Value;

            return (IsDateInRange(today, startDate, endDate) && now >= startTime.Value)
                || (IsDateInRange(today.AddDays(-1), startDate, endDate) && now <= endTime.Value);
        }

        private static bool IsDateInRange(DateOnly date, DateOnly? startDate, DateOnly? endDate)
        {
            if (startDate.HasValue && date < startDate.Value) return false;
            if (endDate.HasValue && date > endDate.Value) return false;
            return true;
        }

        public static decimal ComputeFinalPrice(
            int discountType,
            decimal discountValue,
            List<(decimal Price, int Qty)> products)
        {
            decimal originalTotal = products.Sum(p => p.Price * p.Qty);
            if (discountType == 0) return discountValue;
            return originalTotal * (1 - discountValue / 100m);
        }
    }
}
