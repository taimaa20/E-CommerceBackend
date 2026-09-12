using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IUserBranchService
    {
        Task<UserBranchAssignmentsDto> GetAssignmentsAsync(Guid userId, CancellationToken ct = default);
        Task<UserBranchAssignmentsDto> AssignAsync(Guid userId, UserBranchAssignDto dto, CancellationToken ct = default);
        Task<UserBranchAssignmentsDto> RemoveAsync(Guid userId, Guid branchId, UserBranchRemoveDto dto, CancellationToken ct = default);
        Task<UserBranchAssignmentsDto> SetDefaultAsync(Guid userId, Guid branchId, CancellationToken ct = default);
    }
}
