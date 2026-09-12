using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed record HrEmployeeListFilter(
        string? Search,
        int? Role,
        int? ContractStatus,
        int Page,
        int PageSize);

    public sealed record HrEmployeeRow(
        Guid StaffProfileId,
        Guid UserId,
        string Username,
        string? FullName,
        string? FullNameAr,
        string Role,
        string? StaffNo,
        string? Phone,
        string? PhotoUrl,
        int ContractStatus,
        DateTime? StartDate,
        decimal MonthlySalary,
        decimal NetSalary);

    public sealed record HrEmployeeListResult(
        IReadOnlyList<HrEmployeeRow> Items,
        int Total);

    public interface IHrEmployeeRepository
    {
        Task<HrEmployeeListResult> GetEmployeesAsync(HrEmployeeListFilter filter, CancellationToken ct);
        Task<StaffProfile?> GetByIdAsync(Guid staffProfileId, CancellationToken ct);
        Task<StaffProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct);
        Task<int> CountOpenLeaveRequestsAsync(Guid staffProfileId, CancellationToken ct);
        Task<int> CountOpenLoanRequestsAsync(Guid staffProfileId, CancellationToken ct);
        Task<int> CountOpenPermissionRequestsAsync(Guid staffProfileId, CancellationToken ct);
        Task<IReadOnlyDictionary<Guid, int>> CountOpenRequestsByStaffAsync(IReadOnlyList<Guid> staffProfileIds, CancellationToken ct);
        Task<double> GetTotalWorkingHoursAsync(Guid staffProfileId, CancellationToken ct);

        Task<IReadOnlyList<TimeEntry>> GetTimeEntriesInRangeAsync(
            Guid staffProfileId,
            DateTime fromInclusive,
            DateTime toExclusive,
            CancellationToken ct);
    }
}
