using System;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public interface ITimeTrackingService
    {
        Task<bool> ClockInAsync(Guid userId);
        Task<bool> ClockOutAsync(Guid userId);
        Task CalculateDailyLaborCostAsync(DateTime date);
    }
}
