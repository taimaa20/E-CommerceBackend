using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.DTOs.MobileRequests;

namespace RestaurantPos.Api.Services
{
    public interface IMobileLoanRequestService
    {
        Task<IReadOnlyList<MobileLoanRequestDto>> GetAsync(Guid userId, MobileRequestListQuery query, CancellationToken ct);
        Task<MobileLoanRequestDto> CreateAsync(Guid userId, MobileLoanRequestCreateDto request, CancellationToken ct);
        Task<MobileLoanRequestMetaDto> GetMetaAsync(Guid userId, CancellationToken ct);

        // ===== Admin =====
        Task<HrRequestPagedResponse<HrLoanRequestDto>> GetAdminListAsync(HrRequestAdminFilter filter, CancellationToken ct);
        Task<HrLoanRequestDto> UpdateStatusAsync(Guid requestId, string newStatus, Guid actingUserId, bool isAdmin, CancellationToken ct);
    }
}
