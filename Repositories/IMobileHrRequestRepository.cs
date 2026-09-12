using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.DTOs.MobileRequests;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed record MobileStaffSnapshot(
        Guid StaffProfileId,
        Guid TenantId,
        Guid UserId,
        string Username,
        string? FullName,
        string? StaffNo,
        string? Phone,
        string? PhotoUrl,
        DateTime? StartDate,
        decimal MonthlySalary,
        decimal NetSalary);

    /// Materialized join row returned from admin list queries — entity + employee snapshot.
    public sealed record HrAdminRequestRow<TEntity>(
        TEntity Entity,
        HrRequestEmployeeDto Employee);

    public sealed record HrAdminRequestPage<TEntity>(
        IReadOnlyList<HrAdminRequestRow<TEntity>> Items,
        int Total,
        IReadOnlyDictionary<string, int> StatusCounts);

    public interface IMobileHrRequestRepository
    {
        Task<MobileStaffSnapshot?> GetOrCreateStaffSnapshotAsync(Guid userId, CancellationToken ct);
        Task<IReadOnlyList<MobileLeaveRequestDto>> GetLeaveRequestsAsync(Guid staffProfileId, MobileRequestFilter filter, CancellationToken ct);
        Task<bool> HasOverlappingLeaveRequestAsync(Guid staffProfileId, DateOnly startDate, DateOnly endDate, CancellationToken ct);
        Task<MobileLeaveRequest> AddLeaveRequestAsync(MobileLeaveRequest request, CancellationToken ct);
        Task<IReadOnlyList<MobileLoanRequestDto>> GetLoanRequestsAsync(Guid staffProfileId, MobileRequestFilter filter, CancellationToken ct);
        Task<decimal> GetPreviousLoanBalanceAsync(Guid staffProfileId, CancellationToken ct);
        Task<MobileLoanRequest> AddLoanRequestAsync(MobileLoanRequest request, CancellationToken ct);
        Task<IReadOnlyList<MobilePermissionRequestListItemDto>> GetPermissionRequestsAsync(Guid staffProfileId, MobileRequestFilter filter, CancellationToken ct);
        Task<int> GetUsedPermissionMinutesAsync(Guid staffProfileId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct);
        Task<MobilePermissionRequest> AddPermissionRequestAsync(MobilePermissionRequest request, CancellationToken ct);

        // ===== Admin =====
        Task<HrAdminRequestPage<MobileLeaveRequest>> GetAdminLeaveRequestsAsync(HrRequestAdminFilter filter, CancellationToken ct);
        Task<HrAdminRequestPage<MobileLoanRequest>> GetAdminLoanRequestsAsync(HrRequestAdminFilter filter, CancellationToken ct);
        Task<HrAdminRequestPage<MobilePermissionRequest>> GetAdminPermissionRequestsAsync(HrRequestAdminFilter filter, CancellationToken ct);

        Task<MobileLeaveRequest?> GetLeaveByIdAsync(Guid id, CancellationToken ct);
        Task<MobileLoanRequest?> GetLoanByIdAsync(Guid id, CancellationToken ct);
        Task<MobilePermissionRequest?> GetPermissionByIdAsync(Guid id, CancellationToken ct);
        Task<HrRequestEmployeeDto?> GetEmployeeForStaffAsync(Guid staffProfileId, CancellationToken ct);

        Task SaveChangesAsync(CancellationToken ct);
    }
}
