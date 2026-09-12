using RestaurantPos.Api.Modules.Dashboard.DTOs.Operations;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Operational
{
    public interface IOperationalMetricsService
    {
        Task<OperationsSnapshotDto> BuildSnapshotAsync(CancellationToken ct);
    }
}
