using RestaurantPos.Api.Modules.Dashboard.DTOs.Inventory;
using RestaurantPos.Api.Modules.Dashboard.Services.Export;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Inventory
{
    public interface IInventoryDashboardExportService
    {
        DashboardExportFile BuildWorkbook(InventorySnapshotDto snapshot, string? currency);
    }
}
