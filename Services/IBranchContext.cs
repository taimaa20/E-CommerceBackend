using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IBranchContext
    {
        Task<BranchContextDto> GetCurrentAsync(CancellationToken ct = default);
        Task<BranchContextDto> SwitchAsync(Guid branchId, CancellationToken ct = default);
    }
}
