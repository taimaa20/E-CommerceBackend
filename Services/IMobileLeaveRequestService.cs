using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.DTOs.MobileRequests;

namespace RestaurantPos.Api.Services
{
    public interface IMobileLeaveRequestService
    {
        Task<IReadOnlyList<MobileLeaveRequestDto>> GetAsync(Guid userId, MobileRequestListQuery query, CancellationToken ct);
        Task<MobileLeaveRequestDto> CreateAsync(Guid userId, MobileLeaveRequestCreateDto request, CancellationToken ct);
        Task<MobileLeaveRequestMetaDto> GetMetaAsync(Guid userId, CancellationToken ct);

        // ===== Admin =====
        Task<HrRequestPagedResponse<HrLeaveRequestDto>> GetAdminListAsync(HrRequestAdminFilter filter, CancellationToken ct);
        Task<HrLeaveRequestDto> UpdateStatusAsync(Guid requestId, string newStatus, Guid actingUserId, bool isAdmin, CancellationToken ct);
    }
}
