using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IPosLogService
    {
        Task<PosLogPageDto> GetLogsAsync(
            Guid tenantId,
            PosLogQueryDto query,
            Guid currentUserId,
            bool isAdmin,
            CancellationToken cancellationToken = default);
    }
}
