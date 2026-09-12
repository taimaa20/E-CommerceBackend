using Microsoft.AspNetCore.Http;
using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.DTOs.MobileRequests;

namespace RestaurantPos.Api.Services
{
    public interface IMobilePermissionRequestService
    {
        Task<IReadOnlyList<MobilePermissionRequestListItemDto>> GetAsync(Guid userId, MobileRequestListQuery query, CancellationToken ct);
        Task<MobilePermissionRequestDto> CreateAsync(Guid userId, MobilePermissionRequestCreateDto request, IFormFile? attachment, CancellationToken ct);
        Task<MobilePermissionAllowanceDto> GetAllowanceAsync(Guid userId, CancellationToken ct);

        // ===== Admin =====
        Task<HrRequestPagedResponse<HrPermissionRequestDto>> GetAdminListAsync(HrRequestAdminFilter filter, CancellationToken ct);
        Task<HrPermissionRequestDto> UpdateStatusAsync(Guid requestId, string newStatus, Guid actingUserId, bool isAdmin, CancellationToken ct);
        Task<PermissionAttachmentStream?> OpenAttachmentAsync(Guid requestId, CancellationToken ct);
    }

    public sealed record PermissionAttachmentStream(Stream Content, string ContentType, string FileName, long Length);
}
