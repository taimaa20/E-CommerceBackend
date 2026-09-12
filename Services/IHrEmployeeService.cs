using RestaurantPos.Api.DTOs.Hr;

namespace RestaurantPos.Api.Services
{
    public interface IHrEmployeeService
    {
        Task<HrEmployeeListResponse> GetEmployeesAsync(
            string? search,
            int? role,
            int? contractStatus,
            int page,
            int pageSize,
            CancellationToken ct);

        Task<HrEmployeeDetailDto?> GetEmployeeAsync(Guid employeeId, CancellationToken ct);
        Task<HrEmployeeDetailDto?> GetEmployeeByUserIdAsync(Guid userId, CancellationToken ct);

        Task<HrEmployeeRequestsDto> GetEmployeeRequestsAsync(Guid employeeId, CancellationToken ct);

        Task<HrLookupsDto> GetLookupsAsync(CancellationToken ct);

        Task<HrTimesheetDto?> GetEmployeeTimesheetAsync(Guid employeeId, int year, int month, CancellationToken ct);
    }
}
