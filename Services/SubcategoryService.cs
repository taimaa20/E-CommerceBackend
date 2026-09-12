using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Repositories.Category;
using RestaurantPos.Api.Services.Caching;

namespace RestaurantPos.Api.Services
{
    public class SubcategoryService : ISubcategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchConfigurationService _branchConfigurationService;
        private readonly ICacheService _cache;
        private readonly ILogger<SubcategoryService> _logger;

        public SubcategoryService(
            ICategoryRepository categoryRepository,
            ITenantResolver tenantResolver,
            IBranchConfigurationService branchConfigurationService,
            ICacheService cache,
            ILogger<SubcategoryService> logger)
        {
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<SubcategoryDto>> GetByCategoryAsync(Guid categoryId, bool isArabic, CancellationToken ct = default)
        {
            if (!await _categoryRepository.CategoryExistsAsync(categoryId, ct))
                throw new InvalidOperationException("Category not found.");

            return await _categoryRepository.GetSubcategoriesAsync(categoryId, isArabic, ct);
        }

        public async Task<SubcategoryDto> CreateAsync(Guid categoryId, SubcategoryCreateDto dto, bool isArabic, CancellationToken ct = default)
        {
            await ValidateCategoryAsync(categoryId, ct);
            await ValidateNameAsync(categoryId, dto.Name, null, ct);

            var tenantId = _tenantResolver.GetTenantId();
            var subcategory = await _categoryRepository.CreateSubcategoryAsync(categoryId, dto, tenantId, isArabic, ct);
            await _branchConfigurationService.EnsureMainBranchSubcategoriesAsync(
                new[] { new MainBranchConfigurationSeed(tenantId, subcategory.Id, subcategory.DisplayOrder) },
                ct);
            await InvalidateMenuCachesAsync(tenantId, ct);
            return subcategory;
        }

        public async Task<SubcategoryDto> UpdateAsync(Guid id, SubcategoryUpdateDto dto, bool isArabic, CancellationToken ct = default)
        {
            var current = await _categoryRepository.GetSubcategoryByIdAsync(id, isArabic, ct)
                ?? throw new InvalidOperationException("Subcategory not found.");
            await ValidateNameAsync(current.CategoryId, dto.Name, id, ct);

            var updated = await _categoryRepository.UpdateSubcategoryAsync(id, dto, isArabic, ct)
                ?? throw new InvalidOperationException("Subcategory not found.");
            await InvalidateMenuCachesAsync(_tenantResolver.GetTenantId(), ct);
            return updated;
        }

        public async Task<SubcategoryDto> SetActiveAsync(Guid id, bool isActive, bool isArabic, CancellationToken ct = default)
        {
            var updated = await _categoryRepository.SetSubcategoryActiveAsync(id, isActive, isArabic, ct)
                ?? throw new InvalidOperationException("Subcategory not found.");
            await InvalidateMenuCachesAsync(_tenantResolver.GetTenantId(), ct);
            return updated;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            if (await _categoryRepository.SubcategoryHasProductsAsync(id, ct))
                throw new InvalidOperationException("This subcategory is being used by products. Move the products before deleting it.");

            await _branchConfigurationService.RemoveSubcategoryBranchConfigurationsAsync(id, ct);
            var deleted = await _categoryRepository.DeleteSubcategoryAsync(id, ct);
            if (!deleted)
                throw new InvalidOperationException("Subcategory not found.");

            await InvalidateMenuCachesAsync(_tenantResolver.GetTenantId(), ct);
        }

        public async Task<List<SubcategoryDto>> ReorderAsync(Guid categoryId, List<SubcategoryReorderDto> items, bool isArabic, CancellationToken ct = default)
        {
            await ValidateCategoryAsync(categoryId, ct);
            if (items.Select(i => i.Id).Distinct().Count() != items.Count)
                throw new InvalidOperationException("Subcategory reorder list contains duplicate IDs.");

            var existingIds = (await _categoryRepository.GetSubcategoriesAsync(categoryId, isArabic, ct))
                .Select(s => s.Id)
                .ToHashSet();
            if (items.Any(i => !existingIds.Contains(i.Id)))
                throw new InvalidOperationException("One or more subcategories do not belong to the selected category.");

            var reordered = await _categoryRepository.ReorderSubcategoriesAsync(categoryId, items, isArabic, ct);
            await InvalidateMenuCachesAsync(_tenantResolver.GetTenantId(), ct);
            return reordered;
        }

        private async Task ValidateCategoryAsync(Guid categoryId, CancellationToken ct)
        {
            if (!await _categoryRepository.CategoryExistsAsync(categoryId, ct))
                throw new InvalidOperationException("Category not found.");
        }

        private async Task ValidateNameAsync(Guid categoryId, string name, Guid? excludeId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Subcategory name (EN) is required.");
            if (await _categoryRepository.SubcategoryNameExistsAsync(categoryId, name, excludeId, ct))
                throw new InvalidOperationException($"A subcategory with name '{name.Trim()}' already exists in this category.");
        }

        private async Task InvalidateMenuCachesAsync(Guid tenantId, CancellationToken ct)
        {
            await _cache.RemoveAsync(CacheKeys.CategoriesAll(tenantId), ct);
            await _cache.RemoveByPatternAsync(CacheKeys.ProductsPattern(tenantId), ct);
            await _cache.RemoveByPatternAsync($"pos:menu:*:{tenantId}:*", ct);
            _logger.LogInformation("Subcategory cache dependencies invalidated for tenant {TenantId}", tenantId);
        }
    }
}
