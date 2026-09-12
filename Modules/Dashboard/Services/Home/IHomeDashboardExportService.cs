using RestaurantPos.Api.Modules.Dashboard.DTOs.Home;
using RestaurantPos.Api.Modules.Dashboard.Services.Export;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Home
{
    public interface IHomeDashboardExportService
    {
        DashboardExportFile BuildWorkbook(HomeSnapshotDto snapshot, string? currency);
    }
}
