using RestaurantPos.Api.Modules.Dashboard.DTOs.Operations;
using RestaurantPos.Api.Modules.Dashboard.Services.Export;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Operational
{
    public interface IOperationsDashboardExportService
    {
        DashboardExportFile BuildWorkbook(OperationsSnapshotDto snapshot, string? currency);
    }
}
