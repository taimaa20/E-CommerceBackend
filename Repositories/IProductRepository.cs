using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Repositories
{
    public sealed record CashierBranchCatalogFilter(
        IReadOnlyCollection<Guid> ProductIds,
        IReadOnlyCollection<Guid> CategoryIds,
        IReadOnlyCollection<Guid> SubcategoryIds,
        IReadOnlyCollection<Guid> ModifierGroupIds,
        IReadOnlyCollection<Guid> ModifierIds,
        IReadOnlyCollection<Guid> ProductOptionIds);

    public sealed record ProductBranchConfigurationCleanup(
        Guid ProductId,
        IReadOnlyCollection<Guid> ModifierGroupIds,
        IReadOnlyCollection<Guid> ModifierIds);

    public readonly record struct ProductBranchConfigurationSeed(Guid TenantId, Guid EntityId, int DisplayOrder = 0);

    public sealed record ProductModifierBranchConfigurationSeeds(
        IReadOnlyCollection<ProductBranchConfigurationSeed> ModifierGroups,
        IReadOnlyCollection<ProductBranchConfigurationSeed> Modifiers);

    /// <summary>
    /// Repository abstraction for Product data access.
    /// Isolates EF Core queries from business logic.
    /// </summary>
    public interface IProductRepository
    {
        /// <summary>
        /// Get all products with full eager loading for a tenant.
        /// Used by GET /api/Products
        /// </summary>
        Task<List<ProductDto>> GetAllProductDtosAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get active products only.
        /// Used by GET /api/Products/public
        /// </summary>
        Task<List<ProductDto>> GetActiveProductDtosAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default);

        Task<List<ProductDto>> GetCashierProductDtosAsync(
            Guid tenantId,
            bool isArabic,
            Guid? deliveryPartnerId,
            CashierBranchCatalogFilter branchFilter,
            CancellationToken cancellationToken = default);

        Task<PaginatedResponse<ProductDto>> GetProductDtosPaginatedAsync(
            Guid tenantId,
            bool isArabic,
            int pageNumber,
            int pageSize,
            string? search,
            Guid? categoryId,
            string? status,
            int? stationRouting,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Public menu projection: active products only, stripped of internal cost / BOM data.
        /// Excludes CostPrice, Markup(Type), StationRouting, PrinterIds, modifier recipe items,
        /// and per-ingredient cost/unit fields that the public showcase never renders.
        /// Used by GET /api/Products/public — ~40–60 % smaller than <see cref="GetActiveProductDtosAsync"/>.
        /// </summary>
        Task<List<ProductDto>> GetPublicActiveProductDtosAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get product by ID with full relations.
        /// </summary>
        Task<Product?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if product name exists (for duplicate validation).
        /// </summary>
        Task<bool> ProductNameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if Arabic product name exists.
        /// </summary>
        Task<bool> ProductNameArExistsAsync(string nameAr, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if category exists.
        /// </summary>
        Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken = default);

        Task<bool> SubcategoryBelongsToCategoryAsync(Guid subcategoryId, Guid categoryId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if modifier group exists.
        /// </summary>
        Task<bool> ModifierGroupExistsAsync(Guid modifierGroupId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate raw material IDs exist.
        /// </summary>
        Task<List<Guid>> GetExistingRawMaterialIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create product with relations.
        /// </summary>
        Task<Product> CreateProductAsync(Product product, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update product with relations.
        /// </summary>
        Task<Product> UpdateProductAsync(Product product, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete product and cleanup orphaned modifier groups.
        /// </summary>
        Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);

        Task<ProductBranchConfigurationCleanup> GetProductBranchConfigurationCleanupAsync(
            Guid productId,
            CancellationToken cancellationToken = default);

        Task<ProductModifierBranchConfigurationSeeds> GetProductModifierBranchConfigurationSeedsAsync(
            Guid productId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Add recipe item to product.
        /// </summary>
        Task<RecipeItem> AddRecipeItemAsync(RecipeItem recipeItem, CancellationToken cancellationToken = default);

        Task<RecipeItem?> GetRecipeItemWithProductAsync(Guid recipeItemId, CancellationToken cancellationToken = default);

        Task UpdateRecipeItemAsync(RecipeItem recipeItem, CancellationToken cancellationToken = default);

        Task DeleteRecipeItemAsync(RecipeItem recipeItem, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get recipe items for a product (for cost calculation).
        /// </summary>
        Task<List<RecipeItem>> GetRecipeItemsWithMaterialsAsync(Guid productId, CancellationToken cancellationToken = default);

        Task<List<Product>> GetProductsByRawMaterialIdWithRecipeItemsAsync(
            Guid rawMaterialId,
            CancellationToken cancellationToken = default);

        Task UpdateProductsAsync(IEnumerable<Product> products, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk import products.
        /// </summary>
        Task<int> BulkImportProductsAsync(List<Product> products, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update product modifier groups (handles orphan cleanup).
        /// </summary>
        Task UpdateProductModifierGroupsAsync(Product product, List<ModifierGroupCreateDto> modifierGroupDtos, CancellationToken cancellationToken = default);

        Task<ProductBranchConfigurationCleanup> GetModifierGroupCleanupForProductUpdateAsync(
            Guid productId,
            IReadOnlyCollection<Guid> newLinkedGroupIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replace the product's kitchen-printer mappings to exactly match <paramref name="kitchenPrinterIds"/>.
        /// Empty list clears all mappings (legacy fallback applies).
        /// </summary>
        Task SyncProductKitchenPrintersAsync(Guid productId, Guid tenantId, IReadOnlyCollection<Guid> kitchenPrinterIds, CancellationToken cancellationToken = default);

        /// <summary>Replaces a product's gallery with the supplied list, preserving its order.
        /// Never called with a null list — the service treats null as "leave the gallery alone".</summary>
        Task SyncProductImagesAsync(Guid productId, Guid tenantId, IReadOnlyList<ProductImageInputDto> images, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetch the printer-id mappings for a product (read-side, for DTO projection).
        /// </summary>
        Task<List<Guid>> GetProductKitchenPrinterIdsAsync(Guid productId, CancellationToken cancellationToken = default);
    }
}
