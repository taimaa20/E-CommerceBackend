using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IBranchRepository
    {
        Task<(List<Branch> Items, int TotalCount)> GetPagedAsync(
            string? search,
            bool? isActive,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<Branch?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Branch?> GetTrackedAsync(Guid id, CancellationToken ct = default);
        Task<Branch?> GetMainAsync(CancellationToken ct = default);
        Task<bool> AnyAsync(CancellationToken ct = default);
        Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct = default);
        Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken ct = default);
        Task<Branch> AddAsync(Branch branch, bool clearExistingMain, Guid? updatedById, CancellationToken ct = default);
        Task<Branch> UpdateAsync(Branch branch, bool clearExistingMain, Guid? updatedById, CancellationToken ct = default);
        Task DeleteAsync(Branch branch, CancellationToken ct = default);
    }
}
