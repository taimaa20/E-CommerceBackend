using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services.Time;

namespace RestaurantPos.Api.Services
{
    public static class OrderNumberPeriodResolver
    {
        public static OrderNumberPeriod Resolve(
            OrderNumberResetStrategy strategy,
            DateTime utcNow,
            TimeZoneInfo timeZone,
            Guid? shiftId)
        {
            var time = new RestaurantTimeContext(utcNow, timeZone);
            var date = time.BusinessDate;

            var bucketKey = strategy switch
            {
                OrderNumberResetStrategy.Never => "ALL",
                OrderNumberResetStrategy.Daily => $"DAY:{date:yyyyMMdd}",
                OrderNumberResetStrategy.Monthly => $"MONTH:{date:yyyyMM}",
                OrderNumberResetStrategy.Yearly => $"YEAR:{date:yyyy}",
                OrderNumberResetStrategy.ShiftOpen or OrderNumberResetStrategy.ShiftClose
                    => shiftId.HasValue ? $"SHIFT:{shiftId.Value:N}" : "SHIFT:NONE",
                _ => $"MONTH:{date:yyyyMM}",
            };

            return new OrderNumberPeriod(bucketKey, time.LocalNow);
        }
    }

    public readonly record struct OrderNumberPeriod(string BucketKey, DateTime LocalNow);
}
