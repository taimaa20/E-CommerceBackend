using RestaurantPos.Api.DTOs.Attendance;

namespace RestaurantPos.Api.Interfaces
{
    public interface IAttendanceService
    {
        Task<AttendanceRecordDto?> CheckInAsync(Guid userId, CheckInRequest request, CancellationToken ct = default);
        Task<AttendanceRecordDto?> CheckOutAsync(Guid userId, CheckOutRequest request, CancellationToken ct = default);
        Task<AttendancePagedResponse> GetAttendanceAsync(AttendanceFilterRequest filter, CancellationToken ct = default);
        Task<AttendanceRecordDto?> GetActiveCheckInAsync(Guid userId, CancellationToken ct = default);
    }
}
