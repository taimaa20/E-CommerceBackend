using RestaurantPos.Api.DTOs.Hr;

namespace RestaurantPos.Api.Services
{
    public interface IBonusService
    {
        Task<HrBonusListResponse> GetAsync(
            Guid? employeeId,
            string? status,
            string? bonusType,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            CancellationToken ct);

        Task<HrBonusDto> CreateAsync(HrBonusCreateDto dto, Guid actingUserId, string actingUserName, CancellationToken ct);

        Task<HrBonusDto> UpdateStatusAsync(Guid bonusId, string newStatus, Guid actingUserId, CancellationToken ct);

        Task<HrBonusDto?> GetByIdAsync(Guid id, CancellationToken ct);
    }
}
