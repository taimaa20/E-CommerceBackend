using RestaurantPos.Api.Modules.Dashboard.DTOs.Inventory;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Inventory
{
    public interface IInventoryAnalyticsService
    {
        Task<InventorySnapshotDto> BuildSnapshotAsync(CancellationToken ct);
    }
}
