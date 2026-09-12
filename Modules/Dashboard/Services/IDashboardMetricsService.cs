using RestaurantPos.Api.Modules.Dashboard.DTOs;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Audit;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Financial;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Inventory;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Operations;

namespace RestaurantPos.Api.Modules.Dashboard.Services
{
    /// <summary>
    /// Single entry point used by every dashboard controller. Resolves the
    /// active filter context, applies the cache TTL appropriate for the scope
    /// and delegates to the dedicated analytics service.
    /// </summary>
    public interface IDashboardMetricsService
    {
        Task<OperationsSnapshotDto>      GetOperationsAsync(CancellationToken ct);
        Task<FinancialSnapshotDto>       GetFinancialAsync(CancellationToken ct);
        Task<CommissionAnalysisDto>      GetFinancialCommissionAnalysisAsync(CancellationToken ct);
        Task<InventorySnapshotDto>       GetInventoryAsync(CancellationToken ct);
        Task<AuditSnapshotDto>           GetAuditAsync(CancellationToken ct);
        Task<DashboardFilterOptionsDto>  GetFilterOptionsAsync(CancellationToken ct);
    }
}
