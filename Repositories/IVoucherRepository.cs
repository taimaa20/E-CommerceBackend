using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IVoucherRepository
    {
        Task AcquireDailyUsageLockAsync(Guid tenantId, DateOnly businessDate, CancellationToken ct);
        Task<int> CountUsedTodayAsync(Guid tenantId, DateOnly businessDate, CancellationToken ct);
        Task<bool> ExistsForOrderAsync(Guid tenantId, Guid orderId, CancellationToken ct);
        Task<List<VoucherAuditItemDto>> GetTodayAuditAsync(Guid tenantId, DateOnly businessDate, CancellationToken ct);
        Task InsertAuditAsync(VoucherUsageAudit audit, CancellationToken ct);
    }
}
