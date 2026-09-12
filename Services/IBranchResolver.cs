using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IBranchResolver
    {
        Task<BranchContextDto> ResolveAsync(Guid? requestedBranchId, CancellationToken ct = default);
    }
}
