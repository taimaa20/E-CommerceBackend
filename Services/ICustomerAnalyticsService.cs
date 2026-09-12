using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface ICustomerAnalyticsService
    {
        Task<CustomerAnalyticsSnapshotDto> GetDashboardAsync(
            CustomerAnalyticsQueryDto query,
            bool isArabic,
            CancellationToken ct);
    }
}
