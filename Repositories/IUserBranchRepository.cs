using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed record UserBranchUserSummary(
        Guid Id,
        string Username,
        string? FullName,
        string? FullNameAr);

    public interface IUserBranchRepository
    {
        Task<UserBranchUserSummary?> GetUserSummaryAsync(Guid userId, CancellationToken ct = default);
        Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken ct = default);
        Task<List<UserBranch>> GetAssignmentsAsync(Guid userId, CancellationToken ct = default);
        Task<UserBranch?> GetAssignmentAsync(Guid userId, Guid branchId, CancellationToken ct = default);
        Task<UserBranch?> GetTrackedAssignmentAsync(Guid userId, Guid branchId, CancellationToken ct = default);
        Task<UserBranch?> GetDefaultAsync(Guid userId, CancellationToken ct = default);
        Task<int> CountAssignmentsAsync(Guid userId, CancellationToken ct = default);
        Task<UserBranch> AddAsync(UserBranch assignment, bool clearExistingDefault, Guid? updatedById, CancellationToken ct = default);
        Task<UserBranch> SetDefaultAsync(UserBranch assignment, Guid? updatedById, CancellationToken ct = default);
        Task RemoveAsync(UserBranch assignment, UserBranch? newDefault, Guid? updatedById, CancellationToken ct = default);
    }
}
