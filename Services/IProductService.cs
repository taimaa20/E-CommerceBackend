using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Product business logic service with caching support.
    /// </summary>
    public interface IProductService
    {
        /// <summary>
        /// Get all products (with caching).
        /// </summary>
        Task<List<ProductDto>> GetAllProductsAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get active products only (with caching).
        /// </summary>
        Task<List<ProductDto>> GetActiveProductsAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default);

        Task<List<ProductDto>> GetCashierProductsAsync(Guid tenantId, bool isArabic, Guid? deliveryPartnerId, CancellationToken cancellationToken = default);

        Task<PaginatedResponse<ProductDto>> GetProductsPaginatedAsync(
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
        /// Public-menu projection: active products, stripped of internal cost / BOM data.
        /// Backed by its own per-locale cache so /menu benefits from both a lighter
        /// payload and a cache key that varies by Accept-Language.
        /// </summary>
        Task<List<ProductDto>> GetPublicActiveProductsAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get product by ID.
        /// </summary>
        Task<Product?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create product and invalidate cache.
        /// </summary>
        Task<Product> CreateProductAsync(ProductCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update product and invalidate cache.
        /// </summary>
        Task<Product> UpdateProductAsync(Guid id, ProductCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete product and invalidate cache.
        /// </summary>
        Task DeleteProductAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add recipe item and invalidate cache.
        /// </summary>
        Task<RecipeItem> AddRecipeItemAsync(Guid productId, RecipeItemCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default);

        Task UpdateRecipeItemAsync(Guid recipeItemId, decimal amount, Guid tenantId, CancellationToken cancellationToken = default);

        Task DeleteRecipeItemAsync(Guid recipeItemId, Guid tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk import products and invalidate cache.
        /// </summary>
        Task<int> BulkImportProductsAsync(List<Product> products, Guid tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate product creation/update (business rules).
        /// </summary>
        Task<(bool IsValid, string? ErrorMessage)> ValidateProductAsync(ProductCreateDto dto, Guid? excludeId = null, CancellationToken cancellationToken = default);
    }
}
