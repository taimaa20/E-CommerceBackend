using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface ITableCategoryService
    {
        Task<List<TableCategoryDto>> GetAllAsync(bool isArabic, CancellationToken cancellationToken = default);

        Task<TableCategoryCreateOutcome> CreateAsync(CreateTableCategoryRequest request, CancellationToken cancellationToken = default);

        Task<TableCategoryUpdateOutcome> UpdateAsync(Guid id, CreateTableCategoryRequest request, bool isArabic, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
