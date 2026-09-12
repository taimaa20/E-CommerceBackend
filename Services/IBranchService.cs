using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IBranchService
    {
        Task<BranchPagedResultDto> GetPagedAsync(
            string? search,
            bool? isActive,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<BranchDto> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<BranchDto> CreateAsync(BranchCreateDto dto, CancellationToken ct = default);
        Task<BranchDto> UpdateAsync(Guid id, BranchUpdateDto dto, CancellationToken ct = default);
        Task<BranchDto> ActivateAsync(Guid id, CancellationToken ct = default);
        Task<BranchDto> DeactivateAsync(Guid id, CancellationToken ct = default);
        Task<BranchDto> SetMainAsync(Guid id, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
    }
}
