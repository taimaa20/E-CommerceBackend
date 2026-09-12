using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Repositories.Category
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;

        public CategoryRepository(PosDbContext context, ITenantResolver tenantResolver)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        private static string? NormalizeImageUrl(string? imageUrl)
            => string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();

        private static string ResolveDisplayName(string name, string? nameAr, bool isArabic)
            => isArabic && !string.IsNullOrWhiteSpace(nameAr) ? nameAr : name;

        public async Task<CategoryDto> CreateCategory(CategoryCreateDto dto)
        {


            // Check duplicate name (case-insensitive, per tenant via global filter)
         

            var category = new Models.Category
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                Name = dto.Name,
                NameAr = dto.NameAr,
                Description = dto.Description,
                ImageUrl = NormalizeImageUrl(dto.ImageUrl),
                SortOrder = dto.SortOrder,
                DisplayMode = (CategoryDisplayMode)dto.DisplayMode,
                IsActive = dto.IsActive
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                NameAr = category.NameAr,
                DisplayName = category.Name,
                Description = category.Description,
                ImageUrl = NormalizeImageUrl(category.ImageUrl),
                SortOrder = category.SortOrder,
                DisplayMode = (int)category.DisplayMode,
                IsActive = category.IsActive,
                Subcategories = new List<SubcategoryDto>()
            };
        }

        public async Task<bool> DeleteCategory(Guid id)
        {

            var category = await _context.Categories.FindAsync(id);

            // Case 1: Not found
            if (category == null)
                return false;

            _context.Categories.Remove(category);

            var result = await _context.SaveChangesAsync();

            // Case 2: Check if delete actually happened
            return result > 0;
        }

        public async Task<IEnumerable<CategoryDto>> GetCategories(bool isArabic)
        {

            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    NameAr = c.NameAr,
                    DisplayName = isArabic && c.NameAr != null && c.NameAr != string.Empty ? c.NameAr : c.Name,
                    Description = c.Description,
                    ImageUrl = c.ImageUrl == null ? null : c.ImageUrl.Trim(),
                    SortOrder = c.SortOrder,
                    DisplayMode = (int)c.DisplayMode,
                    IsActive = c.IsActive,
                    Subcategories = c.Subcategories
                        .OrderBy(s => s.DisplayOrder)
                        .ThenBy(s => s.Name)
                        .Select(s => new SubcategoryDto
                        {
                            Id = s.Id,
                            CategoryId = s.CategoryId,
                            Name = s.Name,
                            NameAr = s.NameAr,
                            DisplayName = isArabic && s.NameAr != null && s.NameAr != string.Empty ? s.NameAr : s.Name,
                            DisplayOrder = s.DisplayOrder,
                            IsActive = s.IsActive
                        }).ToList()
                })
                .ToListAsync();
            return categories;
        }

        public async Task<List<CategoryDto>> GetCategoriesPaginated(int pageNumber,
            int pageSize,
            string? search,
            bool? isActive,bool isArabic)
        {
            var query = _context.Categories.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(c =>
                    EF.Functions.ILike(c.Name, $"%{s}%") ||
                    (c.NameAr != null && EF.Functions.ILike(c.NameAr, $"%{s}%")));
            }

            if (isActive.HasValue)
                query = query.Where(c => c.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    NameAr = c.NameAr,
                    DisplayName = isArabic && c.NameAr != null && c.NameAr != string.Empty ? c.NameAr : c.Name,
                    Description = c.Description,
                    ImageUrl = c.ImageUrl == null ? null : c.ImageUrl.Trim(),
                    SortOrder = c.SortOrder,
                    DisplayMode = (int)c.DisplayMode,
                    IsActive = c.IsActive,
                    Subcategories = c.Subcategories
                        .OrderBy(s => s.DisplayOrder)
                        .ThenBy(s => s.Name)
                        .Select(s => new SubcategoryDto
                        {
                            Id = s.Id,
                            CategoryId = s.CategoryId,
                            Name = s.Name,
                            NameAr = s.NameAr,
                            DisplayName = isArabic && s.NameAr != null && s.NameAr != string.Empty ? s.NameAr : s.Name,
                            DisplayOrder = s.DisplayOrder,
                            IsActive = s.IsActive
                        }).ToList()
                })
                .ToListAsync();
     
            return items;
        }

        public async Task<CategoryDto?> GetCategoryById(Guid id,bool isArabic)
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    NameAr = c.NameAr,
                    DisplayName = isArabic && c.NameAr != null && c.NameAr != string.Empty ? c.NameAr : c.Name,
                    Description = c.Description,
                    ImageUrl = c.ImageUrl == null ? null : c.ImageUrl.Trim(),
                    SortOrder = c.SortOrder,
                    DisplayMode = (int)c.DisplayMode,
                    IsActive = c.IsActive,
                    Subcategories = c.Subcategories
                        .OrderBy(s => s.DisplayOrder)
                        .ThenBy(s => s.Name)
                        .Select(s => new SubcategoryDto
                        {
                            Id = s.Id,
                            CategoryId = s.CategoryId,
                            Name = s.Name,
                            NameAr = s.NameAr,
                            DisplayName = isArabic && s.NameAr != null && s.NameAr != string.Empty ? s.NameAr : s.Name,
                            DisplayOrder = s.DisplayOrder,
                            IsActive = s.IsActive
                        }).ToList()
                })
                .FirstOrDefaultAsync();
        }

        public  async Task<List<CategoryDto>> GetPublicCategories(bool isArabic)
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    NameAr = c.NameAr,
                    DisplayName = isArabic && c.NameAr != null && c.NameAr != string.Empty ? c.NameAr : c.Name,
                    Description = c.Description,
                    ImageUrl = c.ImageUrl == null ? null : c.ImageUrl.Trim(),
                    SortOrder = c.SortOrder,
                    DisplayMode = (int)c.DisplayMode,
                    IsActive = c.IsActive,
                    Subcategories = c.Subcategories
                        .Where(s => s.IsActive)
                        .OrderBy(s => s.DisplayOrder)
                        .ThenBy(s => s.Name)
                        .Select(s => new SubcategoryDto
                        {
                            Id = s.Id,
                            CategoryId = s.CategoryId,
                            Name = s.Name,
                            NameAr = s.NameAr,
                            DisplayName = isArabic && s.NameAr != null && s.NameAr != string.Empty ? s.NameAr : s.Name,
                            DisplayOrder = s.DisplayOrder,
                            IsActive = s.IsActive
                        }).ToList()
                })
                .ToListAsync();
            return categories;
        }
            
        public async Task<bool> UpdateCategory(Guid id, CategoryUpdateDto dto)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) 
                return false;
            category.Name = dto.Name;
            category.NameAr = dto.NameAr;
            category.Description = dto.Description;
            category.ImageUrl = NormalizeImageUrl(dto.ImageUrl);
            category.SortOrder = dto.SortOrder;
            category.DisplayMode = (CategoryDisplayMode)dto.DisplayMode;
            category.IsActive = dto.IsActive;

            var result = await _context.SaveChangesAsync();
            return result > 0;
        }
        public async Task<bool>  IsDuplicatedEn( string NameEn)
        {
            var nameEn = NameEn.Trim().ToLower();
            var duplicateEn = await _context.Categories
                .AnyAsync(c => c.Name.ToLower() == nameEn);
        
            return duplicateEn;
        }
        public async Task<bool> IsDuplicatedAr(string NameAr)
        {
            var nameAr = NameAr.Trim();
          
            var duplicateAr = await _context.Categories
               .AnyAsync(c => c.NameAr == nameAr);
         
            return duplicateAr;
        }
    
        public async Task<bool> HasProductRelated(Guid Id)
        {
            var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == Id);
            return hasProducts;
            
            
        }

        public async Task<List<SubcategoryDto>> GetSubcategoriesAsync(Guid categoryId, bool isArabic, CancellationToken ct = default)
        {
            return await _context.Subcategories
                .AsNoTracking()
                .Where(s => s.CategoryId == categoryId)
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .Select(s => new SubcategoryDto
                {
                    Id = s.Id,
                    CategoryId = s.CategoryId,
                    Name = s.Name,
                    NameAr = s.NameAr,
                    DisplayName = isArabic && s.NameAr != null && s.NameAr != string.Empty ? s.NameAr : s.Name,
                    DisplayOrder = s.DisplayOrder,
                    IsActive = s.IsActive
                })
                .ToListAsync(ct);
        }

        public async Task<SubcategoryDto?> GetSubcategoryByIdAsync(Guid id, bool isArabic, CancellationToken ct = default)
        {
            return await _context.Subcategories
                .AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => new SubcategoryDto
                {
                    Id = s.Id,
                    CategoryId = s.CategoryId,
                    Name = s.Name,
                    NameAr = s.NameAr,
                    DisplayName = isArabic && s.NameAr != null && s.NameAr != string.Empty ? s.NameAr : s.Name,
                    DisplayOrder = s.DisplayOrder,
                    IsActive = s.IsActive
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken ct = default)
        {
            return await _context.Categories.AnyAsync(c => c.Id == categoryId, ct);
        }

        public async Task<bool> SubcategoryHasProductsAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Products.AnyAsync(p => p.SubcategoryId == id, ct);
        }

        public async Task<bool> SubcategoryBelongsToCategoryAsync(Guid subcategoryId, Guid categoryId, CancellationToken ct = default)
        {
            return await _context.Subcategories.AnyAsync(s => s.Id == subcategoryId && s.CategoryId == categoryId, ct);
        }

        public async Task<bool> SubcategoryNameExistsAsync(Guid categoryId, string name, Guid? excludeId = null, CancellationToken ct = default)
        {
            var normalizedName = name.Trim().ToLower();
            var query = _context.Subcategories.Where(s => s.CategoryId == categoryId && s.Name.ToLower() == normalizedName);
            if (excludeId.HasValue)
                query = query.Where(s => s.Id != excludeId.Value);

            return await query.AnyAsync(ct);
        }

        public async Task<SubcategoryDto> CreateSubcategoryAsync(Guid categoryId, SubcategoryCreateDto dto, Guid tenantId, bool isArabic, CancellationToken ct = default)
        {
            var subcategory = new Subcategory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CategoryId = categoryId,
                Name = dto.Name.Trim(),
                NameAr = string.IsNullOrWhiteSpace(dto.NameAr) ? null : dto.NameAr.Trim(),
                DisplayOrder = dto.DisplayOrder,
                IsActive = dto.IsActive
            };

            _context.Subcategories.Add(subcategory);
            await _context.SaveChangesAsync(ct);
            return MapSubcategory(subcategory, isArabic);
        }

        public async Task<SubcategoryDto?> UpdateSubcategoryAsync(Guid id, SubcategoryUpdateDto dto, bool isArabic, CancellationToken ct = default)
        {
            var subcategory = await _context.Subcategories.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (subcategory == null)
                return null;

            subcategory.Name = dto.Name.Trim();
            subcategory.NameAr = string.IsNullOrWhiteSpace(dto.NameAr) ? null : dto.NameAr.Trim();
            subcategory.DisplayOrder = dto.DisplayOrder;
            subcategory.IsActive = dto.IsActive;
            await _context.SaveChangesAsync(ct);
            return MapSubcategory(subcategory, isArabic);
        }

        public async Task<SubcategoryDto?> SetSubcategoryActiveAsync(Guid id, bool isActive, bool isArabic, CancellationToken ct = default)
        {
            var subcategory = await _context.Subcategories.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (subcategory == null)
                return null;

            subcategory.IsActive = isActive;
            await _context.SaveChangesAsync(ct);
            return MapSubcategory(subcategory, isArabic);
        }

        public async Task<bool> DeleteSubcategoryAsync(Guid id, CancellationToken ct = default)
        {
            var subcategory = await _context.Subcategories.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (subcategory == null)
                return false;

            _context.Subcategories.Remove(subcategory);
            return await _context.SaveChangesAsync(ct) > 0;
        }

        public async Task<List<SubcategoryDto>> ReorderSubcategoriesAsync(Guid categoryId, List<SubcategoryReorderDto> items, bool isArabic, CancellationToken ct = default)
        {
            var ids = items.Select(i => i.Id).ToHashSet();
            var subcategories = await _context.Subcategories
                .Where(s => s.CategoryId == categoryId && ids.Contains(s.Id))
                .ToListAsync(ct);
            var orderById = items.ToDictionary(i => i.Id, i => i.DisplayOrder);

            foreach (var subcategory in subcategories)
            {
                subcategory.DisplayOrder = orderById[subcategory.Id];
            }

            await _context.SaveChangesAsync(ct);
            return await GetSubcategoriesAsync(categoryId, isArabic, ct);
        }

        private static SubcategoryDto MapSubcategory(Subcategory subcategory, bool isArabic)
        {
            return new SubcategoryDto
            {
                Id = subcategory.Id,
                CategoryId = subcategory.CategoryId,
                Name = subcategory.Name,
                NameAr = subcategory.NameAr,
                DisplayName = ResolveDisplayName(subcategory.Name, subcategory.NameAr, isArabic),
                DisplayOrder = subcategory.DisplayOrder,
                IsActive = subcategory.IsActive
            };
        }

    }
}
