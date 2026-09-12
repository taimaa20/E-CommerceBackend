using RestaurantPos.Api.DTOs.Hr;

namespace RestaurantPos.Api.Services
{
    public interface IStaffService
    {
        Task<HrEmployeeDetailDto> CreateAsync(HrEmployeeCreateDto dto, Guid actingUserId, CancellationToken ct);
        Task<HrEmployeeDetailDto> UpdateAsync(Guid employeeId, HrEmployeeUpdateDto dto, Guid actingUserId, CancellationToken ct);
    }
}
