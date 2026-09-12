using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface ITableCategoryRepository
    {
        Task<List<TableCategoryDto>> GetAllProjectedAsync(bool isArabic, Guid branchId, CancellationToken cancellationToken = default);

        Task<bool> NameExistsAsync(string name, Guid branchId, Guid? excludeId, CancellationToken cancellationToken = default);

        Task<TableCategory?> GetByIdAsync(Guid id, Guid branchId, CancellationToken cancellationToken = default);

        Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

        Task AddAsync(TableCategory category, CancellationToken cancellationToken = default);

        Task UnlinkTablesAsync(Guid categoryId, CancellationToken cancellationToken = default);

        Task RemoveAsync(TableCategory category, CancellationToken cancellationToken = default);

        Task UpdateAsync(TableCategory category, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
