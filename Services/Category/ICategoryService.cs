using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Categroy
{
    public interface ICategoryServices
    {
        Task<IEnumerable<CategoryDto>> GetCategories(bool isArabic);
        Task<List<CategoryDto>> GetCategoriesPaginated(int pageNumber,
            int pageSize,
            string? search,
            bool? isActive, bool isArabic);
        Task<CategoryDto?> GetCategoryById(Guid id, bool isArabic);
        Task<CategoryDto> CreateCategory(CategoryCreateDto dto);
         Task<bool> UpdateCategory(Guid id, CategoryUpdateDto dto);
        Task<bool> DeleteCategory(Guid id);
        Task<List<CategoryDto>> GetPublicCategories(bool isArabic);
        Task<bool> IsDuplicatedEn(string NameEn);
        Task<bool> IsDuplicatedAr(string NameAr);
         Task<bool> HasProductRelated(Guid Id);

    }
}
