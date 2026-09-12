using RestaurantPos.Api.Modules.Dashboard.DTOs.Audit;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Audit
{
    public interface IAuditAnalyticsService
    {
        Task<AuditSnapshotDto> BuildSnapshotAsync(CancellationToken ct);
    }
}
