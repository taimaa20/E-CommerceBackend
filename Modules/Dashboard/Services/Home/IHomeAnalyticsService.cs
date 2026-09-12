using RestaurantPos.Api.Modules.Dashboard.DTOs.Home;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Home
{
    public interface IHomeAnalyticsService
    {
        Task<HomeSnapshotDto> BuildSnapshotAsync(CancellationToken ct);
    }
}
