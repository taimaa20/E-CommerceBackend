using RestaurantPos.Api.Modules.Dashboard.DTOs.Financial;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Financial
{
    public interface IFinancialAnalyticsService
    {
        Task<FinancialSnapshotDto> BuildSnapshotAsync(CancellationToken ct);
        Task<CommissionAnalysisDto> BuildCommissionAnalysisAsync(CancellationToken ct);
    }
}
