using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface ISubcategoryService
    {
        Task<List<SubcategoryDto>> GetByCategoryAsync(Guid categoryId, bool isArabic, CancellationToken ct = default);
        Task<SubcategoryDto> CreateAsync(Guid categoryId, SubcategoryCreateDto dto, bool isArabic, CancellationToken ct = default);
        Task<SubcategoryDto> UpdateAsync(Guid id, SubcategoryUpdateDto dto, bool isArabic, CancellationToken ct = default);
        Task<SubcategoryDto> SetActiveAsync(Guid id, bool isActive, bool isArabic, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
        Task<List<SubcategoryDto>> ReorderAsync(Guid categoryId, List<SubcategoryReorderDto> items, bool isArabic, CancellationToken ct = default);
    }
}
