using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories.Category
{
    public interface ICategoryRepository
    {
          Task <IEnumerable<CategoryDto>>GetCategories(bool isArabic);
       Task<List<CategoryDto>> GetCategoriesPaginated(int pageNumber ,
            int pageSize ,
            string? search ,
            bool? isActive, bool isArabic);
         Task<CategoryDto?> GetCategoryById(Guid id, bool isArabic);
        Task<CategoryDto> CreateCategory(CategoryCreateDto dto);
         Task<bool> UpdateCategory(Guid id, CategoryUpdateDto dto);
        Task<bool> DeleteCategory(Guid id);
        Task<List<CategoryDto>> GetPublicCategories(bool isArabic);
        Task<bool> IsDuplicatedEn(string NameEn);
        Task<bool> IsDuplicatedAr( string NameAr);
        Task<bool> HasProductRelated(Guid Id);
        Task<List<SubcategoryDto>> GetSubcategoriesAsync(Guid categoryId, bool isArabic, CancellationToken ct = default);
        Task<SubcategoryDto?> GetSubcategoryByIdAsync(Guid id, bool isArabic, CancellationToken ct = default);
        Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken ct = default);
        Task<bool> SubcategoryHasProductsAsync(Guid id, CancellationToken ct = default);
        Task<bool> SubcategoryBelongsToCategoryAsync(Guid subcategoryId, Guid categoryId, CancellationToken ct = default);
        Task<bool> SubcategoryNameExistsAsync(Guid categoryId, string name, Guid? excludeId = null, CancellationToken ct = default);
        Task<SubcategoryDto> CreateSubcategoryAsync(Guid categoryId, SubcategoryCreateDto dto, Guid tenantId, bool isArabic, CancellationToken ct = default);
        Task<SubcategoryDto?> UpdateSubcategoryAsync(Guid id, SubcategoryUpdateDto dto, bool isArabic, CancellationToken ct = default);
        Task<SubcategoryDto?> SetSubcategoryActiveAsync(Guid id, bool isActive, bool isArabic, CancellationToken ct = default);
        Task<bool> DeleteSubcategoryAsync(Guid id, CancellationToken ct = default);
        Task<List<SubcategoryDto>> ReorderSubcategoriesAsync(Guid categoryId, List<SubcategoryReorderDto> items, bool isArabic, CancellationToken ct = default);
    }
}
