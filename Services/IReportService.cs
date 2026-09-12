using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IReportService
    {
        Task<ProfitLossReportDto> GetProfitLossReportAsync(
            DateTime startDate,
            DateTime endDate,
            Guid? branchId,
            CancellationToken ct);
    }
}
