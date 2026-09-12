using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers
{
    public static class CashierShiftQueryHelper
    {
        public static IQueryable<CashierBalanceShift> ApplyDateRange(
            IQueryable<CashierBalanceShift> shifts,
            CashierShiftQueryDto query)
        {
            if (query.From.HasValue)
                shifts = shifts.Where(s => s.OpenedAt >= query.From.Value);

            if (query.ToExclusive.HasValue)
                return shifts.Where(s => s.OpenedAt < query.ToExclusive.Value);

            return query.To.HasValue
                ? shifts.Where(s => s.OpenedAt <= query.To.Value)
                : shifts;
        }
    }
}
