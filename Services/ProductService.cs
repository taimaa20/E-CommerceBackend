using Microsoft.Extensions.Logging;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Services.Caching;
using RestaurantPos.Api.Services.Pricing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Product service with in-memory caching.
    /// Handles business logic, validation, and cache invalidation.
    /// </summary>
    public class ProductService : IProductService
    {
        private readonly IProductRepository _repository;
        private readonly ICacheService _cache;
        private readonly IPricingEngine _pricingEngine;
        private readonly IBranchContext _branchContext;
        private readonly IBranchConfigurationService _branchConfigurationService;
        private readonly Modules.Retail.Services.IRetailProductService _retailProducts;
        private readonly ILogger<ProductService> _logger;

        public ProductService(
            IProductRepository repository,
            ICacheService cache,
            IPricingEngine pricingEngine,
            IBranchContext branchContext,
            IBranchConfigurationService branchConfigurationService,
            Modules.Retail.Services.IRetailProductService retailProducts,
            ILogger<ProductService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _pricingEngine = pricingEngine ?? throw new ArgumentNullException(nameof(pricingEngine));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
            _retailProducts = retailProducts ?? throw new ArgumentNullException(nameof(retailProducts));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<ProductDto>> GetAllProductsAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default)
        {
            var cacheKey = CacheKeys.ProductsAll(tenantId, isArabic);

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async () => AddPricingMetrics(
                    await _repository.GetAllProductDtosAsync(tenantId, isArabic, cancellationToken)),
                slidingExpiration: TimeSpan.FromMinutes(15),
                absoluteExpiration: TimeSpan.FromHours(1),
                cancellationToken);
        }

        public async Task<List<ProductDto>> GetActiveProductsAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default)
        {
            var cacheKey = CacheKeys.ProductsActive(tenantId, isArabic);

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async () => AddPricingMetrics(
                    await _repository.GetActiveProductDtosAsync(tenantId, isArabic, cancellationToken)),
                slidingExpiration: TimeSpan.FromMinutes(15),
                absoluteExpiration: TimeSpan.FromHours(1),
                cancellationToken);
        }

        public async Task<List<ProductDto>> GetCashierProductsAsync(
            Guid tenantId,
            bool isArabic,
            Guid? deliveryPartnerId,
            CancellationToken cancellationToken = default)
        {
            var branchId = (await _branchContext.GetCurrentAsync(cancellationToken)).CurrentBranch.Id;
            var branchFilter = await BuildCashierBranchFilterAsync(branchId, cancellationToken);
            if (deliveryPartnerId.HasValue)
            {
                var enabledPartners = await _branchConfigurationService.GetEnabledDeliveryPartnerIdsAsync(branchId, cancellationToken);
                if (!enabledPartners.Contains(deliveryPartnerId.Value))
                    return new List<ProductDto>();
            }

            var cacheKey = $"{CacheKeys.ProductsCashier(tenantId, isArabic, deliveryPartnerId)}:{branchId:N}";

            var products = await _cache.GetOrCreateAsync(
                cacheKey,
                async () => AddPricingMetrics(
                    await _repository.GetCashierProductDtosAsync(tenantId, isArabic, deliveryPartnerId, branchFilter, cancellationToken)),
                slidingExpiration: TimeSpan.FromMinutes(15),
                absoluteExpiration: TimeSpan.FromHours(1),
                cancellationToken) ?? new List<ProductDto>();

            // SKU and barcode are what a till searches on, and stock on hand is what the
            // cashier needs to see before ringing an item up. Attached AFTER the cache and
            // scoped to the active branch: the catalogue itself is stable enough to cache for
            // 15 minutes, but a stock figure that old would be wrong after the first sale.
            // This is the same read the admin product screens use — no second stock source.
            await _retailProducts.AttachRetailDetailsAsync(products, isArabic, cancellationToken, branchId);

            return products;
        }

        private async Task<CashierBranchCatalogFilter> BuildCashierBranchFilterAsync(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var productIds = await _branchConfigurationService.GetAvailableProductIdsAsync(branchId, cancellationToken);
            var categoryIds = await _branchConfigurationService.GetVisibleCategoryIdsAsync(branchId, cancellationToken);
            var subcategoryIds = await _branchConfigurationService.GetVisibleSubcategoryIdsAsync(branchId, cancellationToken);
            var modifierGroupIds = await _branchConfigurationService.GetAvailableModifierGroupIdsAsync(branchId, cancellationToken);
            var modifierIds = await _branchConfigurationService.GetAvailableModifierIdsAsync(branchId, cancellationToken);
            var optionIds = await _branchConfigurationService.GetAvailableProductOptionIdsAsync(branchId, cancellationToken);

            return new CashierBranchCatalogFilter(
                productIds,
                categoryIds,
                subcategoryIds,
                modifierGroupIds,
                modifierIds,
                optionIds);
        }

        public async Task<PaginatedResponse<ProductDto>> GetProductsPaginatedAsync(
            Guid tenantId,
            bool isArabic,
            int pageNumber,
            int pageSize,
            string? search,
            Guid? categoryId,
            string? status,
            int? stationRouting,
            CancellationToken cancellationToken = default)
        {
            var page = await _repository.GetProductDtosPaginatedAsync(
                tenantId, isArabic, pageNumber, pageSize, search, categoryId, status, stationRouting, cancellationToken);
            page.Items = AddPricingMetrics(page.Items);
            return page;
        }

        public async Task<List<ProductDto>> GetPublicActiveProductsAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default)
        {
            var cacheKey = CacheKeys.ProductsPublic(tenantId, isArabic);

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async () => await _repository.GetPublicActiveProductDtosAsync(tenantId, isArabic, cancellationToken),
                slidingExpiration: TimeSpan.FromMinutes(15),
                absoluteExpiration: TimeSpan.FromHours(1),
                cancellationToken);
        }

        public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _repository.GetProductByIdAsync(id, cancellationToken);
        }

        public async Task<Product> CreateProductAsync(ProductCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
        {
            // Validate
            var (isValid, errorMessage) = await ValidateProductAsync(dto, null, cancellationToken);
            if (!isValid)
                throw new InvalidOperationException(errorMessage);

            // Build entity
            var newProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                NameAr = dto.NameAr,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                DescriptionAr = string.IsNullOrWhiteSpace(dto.DescriptionAr) ? null : dto.DescriptionAr.Trim(),
                Calories = dto.Calories,
                BasePrice = dto.BasePrice,
                CostPrice = dto.CostPrice,
                PricingMode = ProductPricingModes.Normalize(dto.PricingMode),
                Markup = dto.Markup,
                MarkupType = ResolveMarkupType(dto.PricingMode, dto.MarkupType),
                DiscountPercentage = dto.DiscountPercentage < 0 ? 0 : dto.DiscountPercentage,
                DiscountedPrice = dto.DiscountedPrice,
                CustomPrice = dto.CustomPrice,
                UseCustomPrice = dto.UseCustomPrice,
                CategoryId = dto.CategoryId,
                SubcategoryId = dto.SubcategoryId,
                TenantId = tenantId,
                IsActive = dto.IsActive,
                IsSoon = dto.IsSoon,
                Allergens = (AllergenType)dto.Allergens,
                StationRouting = (StationRouting)dto.StationRouting,
                PrinterIds = dto.PrinterIds,
                KitchenId = dto.KitchenId,
                AllKitchenPrinters = dto.AllKitchenPrinters,
                ImageUrl = dto.ImageUrl,
                ImageKey = dto.ImageKey,
                AvailableStartDate = dto.AvailableStartDate,
                AvailableEndDate = dto.AvailableEndDate,
                AvailableFrom = dto.AvailableFrom,
                AvailableTo = dto.AvailableTo,
                ProductModifierGroups = new List<ProductModifierGroup>(),
                RecipeItems = dto.RecipeItems.Select(ri => new RecipeItem
                {
                    Id = Guid.NewGuid(),
                    RawMaterialId = ri.RawMaterialId,
                    Amount = ri.Amount,
                    TenantId = tenantId,
                    Alternatives = (ri.Alternatives ?? Enumerable.Empty<RecipeItemAlternativeCreateForRecipeDto>()).Select(a => new RecipeItemAlternative
                    {
                        Id = Guid.NewGuid(),
                        RawMaterialId = a.RawMaterialId,
                        Name = string.IsNullOrWhiteSpace(a.Name) ? null : a.Name.Trim(),
                        NameAr = string.IsNullOrWhiteSpace(a.NameAr) ? null : a.NameAr.Trim(),
                        Amount = a.Amount,
                        PriceAdjustment = a.PriceAdjustment,
                        PricingType = (RecipeItemAlternativePricingType)a.AlternativePricingType,
                        CustomerAdditionalPrice = a.CustomerAdditionalPrice,
                        FixedOverridePrice = a.FixedOverridePrice,
                        IsDefault = a.IsDefault,
                        IsActive = a.IsActive,
                        SortOrder = a.SortOrder,
                        TenantId = tenantId
                    }).ToList()
                }).ToList()
            };

            // Build modifier groups (existing code logic preserved)
            foreach (var (mg, index) in dto.ModifierGroups.Select((mg, i) => (mg, i)))
            {
                if (mg.ExistingGroupId.HasValue && mg.ExistingGroupId.Value != Guid.Empty)
                {
                    newProduct.ProductModifierGroups.Add(new ProductModifierGroup
                    {
                        ProductId = newProduct.Id,
                        ModifierGroupId = mg.ExistingGroupId.Value,
                        SortOrder = index,
                        TenantId = tenantId
                    });
                }
                else
                {
                    var newGroup = new ModifierGroup
                    {
                        Id = Guid.NewGuid(),
                        Name = mg.Name,
                        NameAr = mg.NameAr,
                        SelectionType = (SelectionType)mg.SelectionType,
                        MinSelection = mg.MinSelection,
                        MaxSelection = mg.MaxSelection,
                        TenantId = tenantId,
                        Modifiers = mg.Modifiers.Select(m => new Modifier
                        {
                            Id = Guid.NewGuid(),
                            Name = m.Name,
                            NameAr = m.NameAr,
                            PriceAdjustment = m.PriceAdjustment,
                            PricingType = m.LinkedProductId.HasValue ? PricingType.Fixed : (PricingType)m.PricingType,
                            IsDefault = m.IsDefault,
                            IsActive = m.IsActive,
                            IsFree = m.IsFree,
                            FreeQuantityLimit = m.FreeQuantityLimit,
                            LinkedRawMaterialId = m.LinkedRawMaterialId,
                            LinkedProductId = m.LinkedProductId,
                            LinkedMaterialAmount = m.LinkedProductId.HasValue ? 0 : m.LinkedMaterialAmount,
                            TenantId = tenantId,
                            RecipeItems = m.RecipeItems.Select(ri => new ModifierRecipeItem
                            {
                                Id = Guid.NewGuid(),
                                RawMaterialId = ri.RawMaterialId,
                                Amount = ri.Amount,
                                TenantId = tenantId
                            }).ToList()
                        }).ToList()
                    };
                    newProduct.ProductModifierGroups.Add(new ProductModifierGroup
                    {
                        ProductId = newProduct.Id,
                        ModifierGroupId = newGroup.Id,
                        SortOrder = index,
                        TenantId = tenantId,
                        ModifierGroup = newGroup
                    });
                }
            }

            _pricingEngine.ApplySavedPricing(newProduct, dto.CostPrice, dto.BasePrice);

            var created = await _repository.CreateProductAsync(newProduct, cancellationToken);
            await EnsureMainBranchProductConfigurationAsync(created, cancellationToken);

            await _repository.SyncProductKitchenPrintersAsync(created.Id, tenantId, dto.KitchenPrinterIds ?? new List<Guid>(), cancellationToken);

            if (dto.Images is not null)
                await _repository.SyncProductImagesAsync(created.Id, tenantId, dto.Images, cancellationToken);

            // Invalidate cache
            await InvalidateProductCacheAsync(tenantId, cancellationToken);

            return created;
        }

        public async Task<Product> UpdateProductAsync(Guid id, ProductCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var product = await _repository.GetProductByIdAsync(id, cancellationToken);
            if (product == null)
                throw new InvalidOperationException("Product not found.");

            // Validate
            var (isValid, errorMessage) = await ValidateProductAsync(dto, id, cancellationToken);
            if (!isValid)
                throw new InvalidOperationException(errorMessage);

            // Update properties
            product.Name = dto.Name;
            product.NameAr = dto.NameAr;
            product.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            product.DescriptionAr = string.IsNullOrWhiteSpace(dto.DescriptionAr) ? null : dto.DescriptionAr.Trim();
            product.Calories = dto.Calories;
            product.PricingMode = ProductPricingModes.Normalize(dto.PricingMode);
            product.Markup = dto.Markup;
            product.MarkupType = ResolveMarkupType(dto.PricingMode, dto.MarkupType);
            product.DiscountPercentage = dto.DiscountPercentage < 0 ? 0 : dto.DiscountPercentage;
            product.CustomPrice = dto.CustomPrice;
            product.UseCustomPrice = dto.UseCustomPrice;
            product.CategoryId = dto.CategoryId;
            product.SubcategoryId = dto.SubcategoryId;
            product.IsActive = dto.IsActive;
            product.IsSoon = dto.IsSoon;
            product.Allergens = (AllergenType)dto.Allergens;
            product.StationRouting = (StationRouting)dto.StationRouting;
            product.PrinterIds = dto.PrinterIds;
            product.KitchenId = dto.KitchenId;
            product.AllKitchenPrinters = dto.AllKitchenPrinters;
            product.ImageUrl = dto.ImageUrl;
            // Only overwrite ImageKey when the payload carries one — an older
            // admin client that ships { imageUrl } but omits imageKey must not
            // wipe out an existing optimized key produced by a newer upload.
            if (!string.IsNullOrWhiteSpace(dto.ImageKey))
            {
                product.ImageKey = dto.ImageKey;
            }
            else if (string.IsNullOrWhiteSpace(dto.ImageUrl))
            {
                // The caller removed the image outright. Leaving the key behind would
                // keep rendering the old WebP variant from a product that has no image.
                product.ImageKey = null;
            }
            product.AvailableStartDate = dto.AvailableStartDate;
            product.AvailableEndDate = dto.AvailableEndDate;
            product.AvailableFrom = dto.AvailableFrom;
            product.AvailableTo = dto.AvailableTo;

            // Update modifier groups - delegate to repository for complex update logic
            // This preserves the existing behavior from the controller
            await UpdateProductModifierGroupsAsync(product, dto.ModifierGroups, tenantId, cancellationToken);

            await ApplySavedPricingAsync(product, dto.CostPrice, dto.BasePrice, cancellationToken);

            var updated = await _repository.UpdateProductAsync(product, cancellationToken);

            await _repository.SyncProductKitchenPrintersAsync(updated.Id, tenantId, dto.KitchenPrinterIds ?? new List<Guid>(), cancellationToken);

            // Null means the caller does not manage the gallery — leave the stored images
            // alone so an older admin client cannot silently wipe them.
            if (dto.Images is not null)
                await _repository.SyncProductImagesAsync(updated.Id, tenantId, dto.Images, cancellationToken);

            // Invalidate cache
            await InvalidateProductCacheAsync(tenantId, cancellationToken);

            return updated;
        }

        public async Task DeleteProductAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var cleanup = await _repository.GetProductBranchConfigurationCleanupAsync(id, cancellationToken);
            await _branchConfigurationService.RemoveProductBranchConfigurationsAsync(id, cancellationToken);
            await RemoveModifierBranchConfigurationsAsync(cleanup, cancellationToken);
            await _repository.DeleteProductAsync(id, cancellationToken);

            // Invalidate cache
            await InvalidateProductCacheAsync(tenantId, cancellationToken);
        }

        public async Task<RecipeItem> AddRecipeItemAsync(Guid productId, RecipeItemCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var product = await _repository.GetProductByIdAsync(productId, cancellationToken);
            if (product == null)
                throw new InvalidOperationException("Product not found.");

            var recipeItem = new RecipeItem
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                RawMaterialId = dto.RawMaterialId,
                Amount = dto.Amount,
                TenantId = tenantId
            };

            var created = await _repository.AddRecipeItemAsync(recipeItem, cancellationToken);

            await ApplyCostChangeAsync(product, null, cancellationToken);

            // Invalidate cache
            await InvalidateProductCacheAsync(tenantId, cancellationToken);

            return created;
        }

        public async Task UpdateRecipeItemAsync(
            Guid recipeItemId,
            decimal amount,
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            var recipeItem = await GetRecipeItemAsync(recipeItemId, cancellationToken);
            recipeItem.Amount = amount;
            ApplyRecipeCostChange(recipeItem.Product, recipeItemId, includeCurrentItem: true);
            await _repository.UpdateRecipeItemAsync(recipeItem, cancellationToken);
            await InvalidateProductCacheAsync(tenantId, cancellationToken);
        }

        public async Task DeleteRecipeItemAsync(
            Guid recipeItemId,
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            var recipeItem = await GetRecipeItemAsync(recipeItemId, cancellationToken);
            ApplyRecipeCostChange(recipeItem.Product, recipeItemId, includeCurrentItem: false);
            await _repository.DeleteRecipeItemAsync(recipeItem, cancellationToken);
            await InvalidateProductCacheAsync(tenantId, cancellationToken);
        }

        public async Task<int> BulkImportProductsAsync(List<Product> products, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var count = await _repository.BulkImportProductsAsync(products, cancellationToken);
            await _branchConfigurationService.EnsureMainBranchProductsAsync(
                products.Select(product => new MainBranchConfigurationSeed(product.TenantId, product.Id)).ToList(),
                cancellationToken);

            // Invalidate cache
            await InvalidateProductCacheAsync(tenantId, cancellationToken);

            return count;
        }

        public async Task<(bool IsValid, string? ErrorMessage)> ValidateProductAsync(ProductCreateDto dto, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            // Name validation
            if (string.IsNullOrWhiteSpace(dto.Name))
                return (false, "Product name (EN) is required.");

            var pricingMode = ProductPricingModes.Normalize(dto.PricingMode);
            if (!ProductPricingModes.IsSupported(pricingMode))
                return (false, "Pricing mode must be manual, margin, or multiplier.");

            var pricingValidationError = ValidateAutomaticPricing(dto, pricingMode);
            if (pricingValidationError != null)
                return (false, pricingValidationError);

            // BasePrice is only required when promotional CustomPrice is NOT in use.
            // Per spec, enabling UseCustomPrice bypasses BasePrice validation so
            // admins can publish a promo-only product without entering a base price.
            if (!dto.UseCustomPrice && dto.BasePrice <= 0)
                return (false, "Base price must be greater than zero.");

            // Duplicate name check
            if (await _repository.ProductNameExistsAsync(dto.Name, excludeId, cancellationToken))
                return (false, $"A product with name '{dto.Name.Trim()}' already exists.");

            if (!string.IsNullOrWhiteSpace(dto.NameAr))
            {
                if (await _repository.ProductNameArExistsAsync(dto.NameAr, excludeId, cancellationToken))
                    return (false, $"A product with Arabic name '{dto.NameAr.Trim()}' already exists.");
            }

            if (!dto.CategoryId.HasValue)
                return (false, "Category is required.");

            if (!await _repository.CategoryExistsAsync(dto.CategoryId.Value, cancellationToken))
                return (false, "The specified category does not exist.");

            if (dto.SubcategoryId.HasValue &&
                !await _repository.SubcategoryBelongsToCategoryAsync(dto.SubcategoryId.Value, dto.CategoryId.Value, cancellationToken))
            {
                return (false, "The specified subcategory does not belong to the selected category.");
            }

            // Calories validation
            if (dto.Calories.HasValue && dto.Calories.Value < 0)
                return (false, "Calories must be 0 or greater.");

            // Recipe items validation
            // Skipped entirely when UseCustomPrice = true: promo-only products
            // are decoupled from cost/recipe accounting, so admins shouldn't be
            // blocked by ingredient correctness when publishing a marketing item.
            if (!dto.UseCustomPrice)
            {
                if (dto.RecipeItems.Any(ri => ri.Amount <= 0))
                    return (false, "Recipe item amount must be greater than zero.");

                if (dto.RecipeItems
                    .SelectMany(ri => ri.Alternatives ?? Enumerable.Empty<RecipeItemAlternativeCreateForRecipeDto>())
                    .Any(a => a.Amount <= 0))
                {
                    return (false, "Recipe alternative amount must be greater than zero.");
                }

                if (dto.RecipeItems
                    .SelectMany(ri => ri.Alternatives ?? Enumerable.Empty<RecipeItemAlternativeCreateForRecipeDto>())
                    .Any(a => a.AlternativePricingType == (int)RecipeItemAlternativePricingType.FixedOverride &&
                        (!a.FixedOverridePrice.HasValue || a.FixedOverridePrice.Value <= 0)))
                {
                    return (false, "Fixed override alternatives require an override price.");
                }

                var recipeMaterialIds = dto.RecipeItems.Select(ri => ri.RawMaterialId).Distinct().ToList();
                recipeMaterialIds.AddRange(dto.RecipeItems
                    .SelectMany(ri => ri.Alternatives ?? Enumerable.Empty<RecipeItemAlternativeCreateForRecipeDto>())
                    .Select(a => a.RawMaterialId));
                recipeMaterialIds.AddRange(dto.ModifierGroups
                    .SelectMany(group => group.Modifiers)
                    .SelectMany(modifier => modifier.RecipeItems)
                    .Select(ri => ri.RawMaterialId));
                recipeMaterialIds = recipeMaterialIds.Distinct().ToList();
                if (recipeMaterialIds.Count > 0)
                {
                    var existingIds = await _repository.GetExistingRawMaterialIdsAsync(recipeMaterialIds, cancellationToken);
                    if (existingIds.Count != recipeMaterialIds.Count)
                        return (false, "One or more recipe ingredients are invalid.");
                }

                if (dto.ModifierGroups
                    .SelectMany(group => group.Modifiers)
                    .SelectMany(modifier => modifier.RecipeItems)
                    .Any(ri => ri.Amount <= 0))
                {
                    return (false, "Modifier recipe item amount must be greater than zero.");
                }
            }

            return (true, null);
        }

        #region Private Helpers

        private async Task InvalidateProductCacheAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            // All product caches are keyed per-locale (DisplayName baked in
            // at projection time), so drop EN + AR variants for every key.
            await _cache.RemoveAsync(CacheKeys.ProductsAll(tenantId, isArabic: false), cancellationToken);
            await _cache.RemoveAsync(CacheKeys.ProductsAll(tenantId, isArabic: true), cancellationToken);
            await _cache.RemoveAsync(CacheKeys.ProductsActive(tenantId, isArabic: false), cancellationToken);
            await _cache.RemoveAsync(CacheKeys.ProductsActive(tenantId, isArabic: true), cancellationToken);
            await _cache.RemoveAsync(CacheKeys.ProductsPublic(tenantId, isArabic: false), cancellationToken);
            await _cache.RemoveAsync(CacheKeys.ProductsPublic(tenantId, isArabic: true), cancellationToken);
            await _cache.RemoveByPatternAsync(CacheKeys.ProductsPattern(tenantId), cancellationToken);
            await _cache.RemoveByPatternAsync($"pos:products:cashier:*:{tenantId}:*", cancellationToken);

            // Invalidate menu caches (since menu depends on products)
            await _cache.RemoveByPatternAsync($"pos:menu:*:{tenantId}:*", cancellationToken);

            _logger.LogInformation("Product and menu caches invalidated for tenant {TenantId}", tenantId);
        }

        private List<ProductDto> AddPricingMetrics(List<ProductDto> products)
        {
            foreach (var product in products)
            {
                var metrics = _pricingEngine.CalculateMetrics(product.CostPrice, product.BasePrice);
                product.ProfitAmount = metrics.ProfitAmount;
                product.ProfitMargin = metrics.ProfitMargin;
                product.ProfitMultiplier = metrics.ProfitMultiplier;
            }

            return products;
        }

        private async Task ApplySavedPricingAsync(
            Product product,
            decimal? fallbackCostPrice,
            decimal requestedBasePrice,
            CancellationToken cancellationToken)
        {
            var calculatedCostPrice = await ResolveCostPriceAsync(
                product.Id,
                fallbackCostPrice,
                cancellationToken);
            _pricingEngine.ApplySavedPricing(product, calculatedCostPrice, requestedBasePrice);
        }

        private async Task ApplyCostChangeAsync(
            Product product,
            decimal? fallbackCostPrice,
            CancellationToken cancellationToken)
        {
            var calculatedCostPrice = await ResolveCostPriceAsync(
                product.Id,
                fallbackCostPrice,
                cancellationToken);
            _pricingEngine.ApplyCostChange(product, calculatedCostPrice);
            await _repository.UpdateProductAsync(product, cancellationToken);
        }

        private async Task<decimal?> ResolveCostPriceAsync(
            Guid productId,
            decimal? fallbackCostPrice,
            CancellationToken cancellationToken)
        {
            var recipeItems = await _repository.GetRecipeItemsWithMaterialsAsync(productId, cancellationToken);
            return recipeItems.Count > 0
                ? recipeItems.Sum(r => r.Amount * r.RawMaterial.CostPerUnit)
                : fallbackCostPrice;
        }

        private static string? ValidateAutomaticPricing(ProductCreateDto dto, string pricingMode)
        {
            if (!ProductPricingModes.IsAutomatic(pricingMode))
                return null;
            if (dto.CostPrice is not > 0m)
                return "Cost price must be greater than zero for automatic pricing.";
            if (dto.Markup > 100m)
                return "Pricing value must be between 0 and 100.";
            if (pricingMode == ProductPricingModes.Margin && dto.Markup >= 100m)
                return "Profit margin must be less than 100%.";
            if (pricingMode == ProductPricingModes.Multiplier && dto.Markup <= 0m)
                return "Profit multiplier must be greater than zero.";

            return null;
        }

        private static string ResolveMarkupType(string? pricingMode, string? legacyMarkupType)
        {
            var mode = ProductPricingModes.Normalize(pricingMode);
            if (mode == ProductPricingModes.Margin)
                return ProductMarkupTypes.Percentage;
            if (mode == ProductPricingModes.Multiplier)
                return ProductMarkupTypes.Multiplier;

            return NormalizeLegacyMarkupType(legacyMarkupType);
        }

        private static string NormalizeLegacyMarkupType(string? markupType)
        {
            var normalized = string.IsNullOrWhiteSpace(markupType)
                ? ProductMarkupTypes.Multiplier
                : markupType.Trim().ToLowerInvariant();

            return normalized is ProductMarkupTypes.Percentage or ProductMarkupTypes.Fixed
                ? normalized
                : ProductMarkupTypes.Multiplier;
        }

        private async Task UpdateProductModifierGroupsAsync(Product product, List<ModifierGroupCreateDto> modifierGroupDtos, Guid tenantId, CancellationToken cancellationToken)
        {
            var newLinkedGroupIds = modifierGroupDtos
                .Where(group => group.ExistingGroupId.HasValue && group.ExistingGroupId.Value != Guid.Empty)
                .Select(group => group.ExistingGroupId!.Value)
                .ToList();
            var cleanup = await _repository.GetModifierGroupCleanupForProductUpdateAsync(
                product.Id,
                newLinkedGroupIds,
                cancellationToken);
            await RemoveModifierBranchConfigurationsAsync(cleanup, cancellationToken);
            await _repository.UpdateProductModifierGroupsAsync(product, modifierGroupDtos, cancellationToken);
            await EnsureMainBranchProductConfigurationAsync(product, cancellationToken);
        }

        private async Task EnsureMainBranchProductConfigurationAsync(
            Product product,
            CancellationToken cancellationToken)
        {
            await _branchConfigurationService.EnsureMainBranchProductsAsync(
                new[] { new MainBranchConfigurationSeed(product.TenantId, product.Id) },
                cancellationToken);

            var seeds = await _repository.GetProductModifierBranchConfigurationSeedsAsync(product.Id, cancellationToken);
            await _branchConfigurationService.EnsureMainBranchModifierGroupsAsync(
                ToMainBranchSeeds(seeds.ModifierGroups),
                cancellationToken);
            await _branchConfigurationService.EnsureMainBranchModifiersAsync(
                ToMainBranchSeeds(seeds.Modifiers),
                cancellationToken);
        }

        private static List<MainBranchConfigurationSeed> ToMainBranchSeeds(
            IReadOnlyCollection<ProductBranchConfigurationSeed> seeds)
            => seeds
                .Select(seed => new MainBranchConfigurationSeed(seed.TenantId, seed.EntityId, seed.DisplayOrder))
                .ToList();

        private async Task RemoveModifierBranchConfigurationsAsync(
            ProductBranchConfigurationCleanup cleanup,
            CancellationToken cancellationToken)
        {
            await _branchConfigurationService.RemoveModifierBranchConfigurationsAsync(
                cleanup.ModifierIds,
                cancellationToken);
            await _branchConfigurationService.RemoveModifierGroupBranchConfigurationsAsync(
                cleanup.ModifierGroupIds,
                cancellationToken);
        }

        private async Task<RecipeItem> GetRecipeItemAsync(
            Guid recipeItemId,
            CancellationToken cancellationToken)
        {
            return await _repository.GetRecipeItemWithProductAsync(recipeItemId, cancellationToken)
                ?? throw new NotFoundException("Recipe item not found.");
        }

        private void ApplyRecipeCostChange(
            Product product,
            Guid currentRecipeItemId,
            bool includeCurrentItem)
        {
            var rows = includeCurrentItem
                ? product.RecipeItems
                : product.RecipeItems.Where(r => r.Id != currentRecipeItemId);
            var costPrice = rows.Any()
                ? rows.Sum(r => r.Amount * r.RawMaterial.CostPerUnit)
                : (decimal?)null;

            _pricingEngine.ApplyCostChange(product, costPrice);
        }

        #endregion
    }
}
