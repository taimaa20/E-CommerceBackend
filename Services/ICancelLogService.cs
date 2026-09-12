using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Cancel-log read API. Admin/Manager see all entries for the tenant; other roles
    /// see only their own (enforced server-side, regardless of the role-claim).
    /// </summary>
    public interface ICancelLogService
    {
        Task<CancelLogPageDto> GetAsync(
            Guid tenantId,
            Guid currentUserId,
            UserRole currentUserRole,
            CancelLogQueryDto query,
            CancellationToken cancellationToken = default);
    }
}
