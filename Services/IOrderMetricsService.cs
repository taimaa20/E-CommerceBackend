using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IOrderMetricsService
    {
        Task<decimal> GetTotalRevenueAsync(Guid tenantId, DateTime startDate, DateTime endDate, Guid? branchId, CancellationToken ct);
        Task<int> GetTotalOrdersAsync(Guid tenantId, DateTime startDate, DateTime endDate, Guid? branchId, CancellationToken ct);
        Task<int> GetActiveOrdersAsync(Guid tenantId, Guid? branchId, CancellationToken ct);
        Task<int> GetCompletedOrdersAsync(Guid tenantId, DateTime startDate, DateTime endDate, Guid? branchId, CancellationToken ct);
        Task<AnalyticsResponseDto> GetAnalyticsAsync(Guid tenantId, DateTime? startDate, DateTime? endDate, Guid? branchId, CancellationToken ct);
        Task<DailySummaryDto> GetDailySummaryAsync(Guid? branchId, CancellationToken ct);
        Task<List<DailyReportDto>> GetZReportAsync(DateTime? startDate, DateTime? endDate, Guid? branchId, CancellationToken ct);
    }
}
