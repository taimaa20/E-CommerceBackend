using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories.Category;

namespace RestaurantPos.Api.Services.Categroy
{
    public class CategoryServices : ICategoryServices
    {
        private readonly ICategoryRepository _ICategoryRepository;
        public CategoryServices(ICategoryRepository ICategoryServices)
        {
            _ICategoryRepository = ICategoryServices;
        }
        public async Task <CategoryDto> CreateCategory(CategoryCreateDto dto)
        {
            var CreatedCategory = await _ICategoryRepository.CreateCategory(dto);
            return CreatedCategory;
        }

        public async Task<bool> DeleteCategory(Guid id)
        {
            var deletedCategory =await  _ICategoryRepository.DeleteCategory(id);
            return deletedCategory;      
                
                }

        public async Task<IEnumerable<CategoryDto>> GetCategories(bool isArabic)
        {
            var Categories = await _ICategoryRepository.GetCategories(isArabic);
            return Categories;
        }

        public async Task<List<CategoryDto>> GetCategoriesPaginated(int pageNumber,
            int pageSize,
            string? search,
            bool? isActive, bool isArabic)
        {
            var Categories = await _ICategoryRepository.GetCategoriesPaginated(pageNumber, pageSize, search, isActive, isArabic);
            return Categories;
        }

        public async Task<CategoryDto?> GetCategoryById(Guid id, bool isArabic)
        {
            var Category = await _ICategoryRepository.GetCategoryById(id,isArabic);
            return Category;
        }

        public async Task<List<CategoryDto>> GetPublicCategories(bool isArabic)
        {
            var Categories = await _ICategoryRepository.GetPublicCategories(isArabic);
            return Categories;
        }

        public async Task<bool> IsDuplicatedEn(string NameEn)
        {
            bool IsDuplicated= await _ICategoryRepository.IsDuplicatedEn(NameEn);
            return IsDuplicated;
        }

        public async Task<bool> IsDuplicatedAr(string NameAr)
        {
            bool IsDuplicated = await _ICategoryRepository.IsDuplicatedAr(NameAr);
            return IsDuplicated;
        }
      
        public async Task<bool> UpdateCategory(Guid id, CategoryUpdateDto dto)
        {
            bool UpdatedCategory = await _ICategoryRepository.UpdateCategory(id,dto);
            return UpdatedCategory;
        }
        public async Task<bool> HasProductRelated(Guid id)
        {
            bool HasProductRelated = await _ICategoryRepository.HasProductRelated(id);
            return HasProductRelated;
        }
    }
}
