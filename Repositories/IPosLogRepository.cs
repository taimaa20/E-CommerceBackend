using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IPosLogRepository
    {
        Task<CashierBalanceShift?> GetShiftAsync(Guid tenantId, Guid shiftId, CancellationToken cancellationToken = default);
        Task<PosLogPageDto> GetLogsAsync(
            Guid tenantId,
            PosLogQueryDto query,
            Guid currentUserId,
            bool isAdmin,
            CashierBalanceShift? shift,
            CancellationToken cancellationToken = default);
    }
}
