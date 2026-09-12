using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Repositories
{
    /// <summary>
    /// Product repository implementation using EF Core.
    /// All database queries are centralized here.
    /// </summary>
    public class ProductRepository : IProductRepository
    {
        private const string ActiveStatus = "active";
        private const string InactiveStatus = "inactive";
        private readonly PosDbContext _context;

        public ProductRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<List<ProductDto>> GetAllProductDtosAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default)
            => QueryProductDtos(tenantId, isArabic, activeOnly: false, cancellationToken);

        public Task<List<ProductDto>> GetActiveProductDtosAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default)
            => QueryProductDtos(tenantId, isArabic, activeOnly: true, cancellationToken);

        public async Task<PaginatedResponse<ProductDto>> GetProductDtosPaginatedAsync(
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
            var query = BuildProductPageQuery(tenantId, search, categoryId, status, stationRouting);
            var totalCount = await query.CountAsync(cancellationToken);
            var productIds = await query
                .OrderBy(p => p.Name)
                .ThenBy(p => p.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            var items = await QueryProductPageDtos(tenantId, isArabic, productIds, cancellationToken);

            return new PaginatedResponse<ProductDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<List<ProductDto>> GetCashierProductDtosAsync(
            Guid tenantId,
            bool isArabic,
            Guid? deliveryPartnerId,
            CashierBranchCatalogFilter branchFilter,
            CancellationToken cancellationToken = default)
        {
            if (branchFilter.ProductIds.Count == 0)
                return new List<ProductDto>();

            if (!deliveryPartnerId.HasValue)
                return await QueryCashierProductDtos(tenantId, isArabic, branchFilter, cancellationToken);

            var partner = await _context.DeliveryPartners
                .AsNoTracking()
                .Where(p => p.Id == deliveryPartnerId.Value && p.Status == DeliveryPartnerStatus.Active)
                .Select(p => new PartnerProjection(
                    p.Id,
                    p.Name,
                    p.NameAr,
                    p.Code,
                    p.DefaultPricingRuleType,
                    p.DefaultPricingRuleValue))
                .FirstOrDefaultAsync(cancellationToken);

            if (partner == null)
                return new List<ProductDto>();

            var mappings = await _context.DeliveryPartnerProducts
                .AsNoTracking()
                .Where(m =>
                    m.DeliveryPartnerId == deliveryPartnerId.Value &&
                    m.IsEnabled &&
                    m.IsAvailable &&
                    m.Product.IsActive)
                .Select(m => new ProductMappingProjection(
                    m.ProductId,
                    m.IsEnabled,
                    m.IsAvailable,
                    m.PricingRuleType,
                    m.PricingRuleValue,
                    m.CustomPrice,
                    m.AvailableStartDate,
                    m.AvailableEndDate,
                    m.AvailableFrom,
                    m.AvailableTo))
                .ToListAsync(cancellationToken);

            var activeMappings = mappings
                .Where(m => AvailabilityHelper.IsAvailableNow(
                    m.AvailableStartDate,
                    m.AvailableEndDate,
                    m.AvailableFrom,
                    m.AvailableTo))
                .ToDictionary(m => m.ProductId);

            if (activeMappings.Count == 0)
                return new List<ProductDto>();

            var allowedProductIds = activeMappings.Keys.Intersect(branchFilter.ProductIds).ToList();
            if (allowedProductIds.Count == 0)
                return new List<ProductDto>();

            branchFilter = branchFilter with { ProductIds = allowedProductIds };
            var products = await QueryCashierProductDtos(
                tenantId,
                isArabic,
                branchFilter,
                cancellationToken);

            return products
                .Select(product => ApplyPartnerPrice(product, partner, activeMappings.GetValueOrDefault(product.Id)))
                .Where(product => product != null)
                .Select(product => product!)
                .ToList();
        }

        private IQueryable<Product> BuildProductPageQuery(
            Guid tenantId,
            string? search,
            Guid? categoryId,
            string? status,
            int? stationRouting)
        {
            var query = _context.Products.AsNoTracking().Where(p => p.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, pattern) ||
                    (p.NameAr != null && EF.Functions.ILike(p.NameAr, pattern)));
            }

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);
            if (status?.Equals(ActiveStatus, StringComparison.OrdinalIgnoreCase) == true)
                query = query.Where(p => p.IsActive);
            if (status?.Equals(InactiveStatus, StringComparison.OrdinalIgnoreCase) == true)
                query = query.Where(p => !p.IsActive);
            if (stationRouting.HasValue)
                query = query.Where(p => (int)p.StationRouting == stationRouting.Value);

            return query;
        }

        private async Task<List<ProductDto>> QueryProductPageDtos(
            Guid tenantId,
            bool isArabic,
            IReadOnlyList<Guid> productIds,
            CancellationToken cancellationToken)
        {
            if (productIds.Count == 0)
                return new List<ProductDto>();

            var order = productIds.Select((id, index) => new { id, index })
                .ToDictionary(x => x.id, x => x.index);
            var products = await QueryProductDtos(
                tenantId,
                isArabic,
                activeOnly: false,
                cancellationToken,
                productIds);

            return products.OrderBy(p => order[p.Id]).ToList();
        }

        public async Task<List<ProductDto>> GetPublicActiveProductDtosAsync(Guid tenantId, bool isArabic, CancellationToken cancellationToken = default)
        {
            // Lean projection for the public menu: keeps every field consumed by the
            // showcase (grid card + detail panel) and drops internal cost/BOM data
            // (CostPrice, Markup, StationRouting, PrinterIds, UnitCost, TotalCost,
            // modifier recipe items). Inactive modifiers are filtered server-side —
            // the showcase discarded them locally anyway, so the wire payload carried
            // them for nothing.
            return await _context.Products
                .AsNoTracking()
                .AsSplitQuery()  // Projects 3 sibling collections (ModifierGroups, RecipeItems+Alternatives, Options). Single-query mode JOINs them into a cartesian product and does NOT dedupe projected collections — each child collection gets multiplied by the others' cardinality, ballooning 88 products to ~69 MB / 30s+. Split-query loads each collection independently → correct cardinality, no explosion.
                .Where(p => p.TenantId == tenantId && p.IsActive)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    NameAr = p.NameAr,
                    DisplayName = isArabic && p.NameAr != null && p.NameAr != string.Empty ? p.NameAr : p.Name,
                    Description = p.Description,
                    DescriptionAr = p.DescriptionAr,
                    Calories = p.Calories,
                    BasePrice = p.BasePrice,
                    DiscountPercentage = p.DiscountPercentage,
                    DiscountedPrice = p.DiscountedPrice,
                    CustomPrice = p.CustomPrice,
                    UseCustomPrice = p.UseCustomPrice,
                    DisplayPrice = p.UseCustomPrice && p.CustomPrice.HasValue
                        ? p.CustomPrice.Value
                        : (p.DiscountedPrice ?? p.BasePrice),
                    IsActive = p.IsActive,
                    IsSoon = p.IsSoon,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategoryNameAr = p.Category != null ? p.Category.NameAr : null,
                    CategoryDisplayName = p.Category != null
                        ? (isArabic && p.Category.NameAr != null && p.Category.NameAr != string.Empty ? p.Category.NameAr : p.Category.Name)
                        : null,
                    SubcategoryId = p.Subcategory != null && p.Subcategory.IsActive ? p.SubcategoryId : null,
                    SubcategoryName = p.Subcategory != null && p.Subcategory.IsActive ? p.Subcategory.Name : null,
                    SubcategoryNameAr = p.Subcategory != null && p.Subcategory.IsActive ? p.Subcategory.NameAr : null,
                    SubcategoryDisplayName = p.Subcategory != null && p.Subcategory.IsActive
                        ? (isArabic && p.Subcategory.NameAr != null && p.Subcategory.NameAr != string.Empty ? p.Subcategory.NameAr : p.Subcategory.Name)
                        : null,
                    Allergens = (int)p.Allergens,
                    ImageUrl = p.ImageUrl,
                    ImageKey = p.ImageKey,
                    IsAvailableNow = AvailabilityHelper.IsAvailableNow(
                        p.AvailableStartDate,
                        p.AvailableEndDate,
                        p.AvailableFrom,
                        p.AvailableTo),
                    AvailableStartDate = p.AvailableStartDate,
                    AvailableEndDate = p.AvailableEndDate,
                    AvailableFrom = p.AvailableFrom,
                    AvailableTo = p.AvailableTo,
                    ModifierGroups = p.ProductModifierGroups
                        .OrderBy(pmg => pmg.SortOrder)
                        .Select(pmg => new ModifierGroupDto
                        {
                            Id = pmg.ModifierGroup.Id,
                            Name = pmg.ModifierGroup.Name,
                            NameAr = pmg.ModifierGroup.NameAr,
                            DisplayName = isArabic && pmg.ModifierGroup.NameAr != null && pmg.ModifierGroup.NameAr != string.Empty
                                ? pmg.ModifierGroup.NameAr
                                : pmg.ModifierGroup.Name,
                            SelectionType = (int)pmg.ModifierGroup.SelectionType,
                            DisplayType = (int)pmg.ModifierGroup.DisplayType,
                            MinSelection = pmg.ModifierGroup.MinSelection,
                            MaxSelection = pmg.ModifierGroup.MaxSelection,
                            IsRequired = pmg.ModifierGroup.IsRequired,
                            AvailableForOrderTypes = (int)pmg.ModifierGroup.AvailableForOrderTypes,
                            PrintOnReceipt = pmg.ModifierGroup.PrintOnReceipt,
                            PrintInKitchen = pmg.ModifierGroup.PrintInKitchen,
                            Modifiers = pmg.ModifierGroup.Modifiers
                                .Where(m => m.IsActive)
                                .Select(m => new ModifierDto
                                {
                                    Id = m.Id,
                                    Name = m.Name,
                                    NameAr = m.NameAr,
                                    DisplayName = isArabic && m.NameAr != null && m.NameAr != string.Empty ? m.NameAr : m.Name,
                                    PriceAdjustment = m.PriceAdjustment,
                                    PricingType = m.LinkedProductId.HasValue ? (int)PricingType.Fixed : (int)m.PricingType,
                                    IsFree = m.IsFree,
                                    FreeQuantityLimit = m.FreeQuantityLimit,
                                    MaxQuantity = m.MaxQuantity,
                                    LinkedProductId = m.LinkedProductId,
                                    IsDefault = m.IsDefault,
                                    IsActive = m.IsActive
                                }).ToList()
                        }).ToList(),
                    RecipeItems = p.RecipeItems
                        .Where(r => r.RawMaterial.ShowInMenu)
                        .Select(r => new RecipeItemDto
                        {
                            Id = r.Id,
                            RawMaterialId = r.RawMaterialId,
                            RawMaterialName = r.RawMaterial.Name,
                            RawMaterialNameAr = r.RawMaterial.NameAr,
                            ShowInMenu = r.RawMaterial.ShowInMenu,
                            IsPostPrice = r.RawMaterial.IsPostPrice,
                            Amount = r.Amount,
                            Unit = r.RawMaterial.Unit.ToString(),
                            Alternatives = r.Alternatives
                                .Where(a => a.IsActive && a.RawMaterial.ShowInMenu)
                                .OrderBy(a => a.SortOrder)
                                .Select(a => new RecipeItemAlternativeDto
                                {
                                    Id = a.Id,
                                    RecipeItemId = a.RecipeItemId,
                                    RawMaterialId = a.RawMaterialId,
                                    RawMaterialName = a.RawMaterial.Name,
                                    RawMaterialNameAr = a.RawMaterial.NameAr,
                                    Name = a.Name,
                                    NameAr = a.NameAr,
                                    Amount = a.Amount,
                                    Unit = a.RawMaterial.Unit.ToString(),
                                    PriceAdjustment = a.PricingType == RecipeItemAlternativePricingType.Included
                                        ? 0m
                                        : a.PricingType == RecipeItemAlternativePricingType.FixedOverride
                                            ? (a.FixedOverridePrice ?? a.PriceAdjustment) - (p.DiscountedPrice ?? p.BasePrice)
                                            : a.CustomerAdditionalPrice ?? (a.PriceAdjustment - (r.Amount * r.RawMaterial.CostPerUnit)),
                                    AlternativePricingType = (int)a.PricingType,
                                    CustomerAdditionalPrice = a.CustomerAdditionalPrice,
                                    FixedOverridePrice = a.FixedOverridePrice,
                                    CustomerPriceImpact = a.PricingType == RecipeItemAlternativePricingType.Included
                                        ? 0m
                                        : a.PricingType == RecipeItemAlternativePricingType.FixedOverride
                                            ? (a.FixedOverridePrice ?? a.PriceAdjustment) - (p.DiscountedPrice ?? p.BasePrice)
                                            : a.CustomerAdditionalPrice ?? (a.PriceAdjustment - (r.Amount * r.RawMaterial.CostPerUnit)),
                                    CostImpact = 0m,
                                    IsDefault = a.IsDefault,
                                    IsActive = a.IsActive,
                                    SortOrder = a.SortOrder,
                                }).ToList()
                        }).ToList(),
                    // Variants ("Choose Your Variant" — Pita / Saj / etc.).
                    // Public shape: no RecipeItems / cost data — only what the
                    // showcase + mobile menu render (label + price + default flag).
                    Options = p.Options
                        .Where(o => o.IsActive)
                        .OrderBy(o => o.SortOrder)
                        .Select(o => new ProductOptionDto
                        {
                            Id = o.Id,
                            Name = o.Name,
                            NameAr = o.NameAr,
                            Price = o.Price,
                            IsDefault = o.IsDefault,
                            IsActive = o.IsActive,
                            SortOrder = o.SortOrder,
                        }).ToList()
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<ProductDto>> QueryCashierProductDtos(
            Guid tenantId,
            bool isArabic,
            CashierBranchCatalogFilter branchFilter,
            CancellationToken cancellationToken)
        {
            var query = _context.Products
                .AsNoTracking()
                .AsSplitQuery()
                .Where(p => p.TenantId == tenantId
                    && p.IsActive
                    && branchFilter.ProductIds.Contains(p.Id)
                    && (!p.CategoryId.HasValue || branchFilter.CategoryIds.Contains(p.CategoryId.Value))
                    && (!p.SubcategoryId.HasValue || branchFilter.SubcategoryIds.Contains(p.SubcategoryId.Value)));

            return await query
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    NameAr = p.NameAr,
                    DisplayName = isArabic && p.NameAr != null && p.NameAr != string.Empty ? p.NameAr : p.Name,
                    Description = p.Description,
                    DescriptionAr = p.DescriptionAr,
                    Calories = p.Calories,
                    BasePrice = p.BasePrice,
                    DiscountPercentage = p.DiscountPercentage,
                    DiscountedPrice = p.DiscountedPrice,
                    CustomPrice = p.CustomPrice,
                    UseCustomPrice = p.UseCustomPrice,
                    DisplayPrice = p.UseCustomPrice && p.CustomPrice.HasValue
                        ? p.CustomPrice.Value
                        : (p.DiscountedPrice ?? p.BasePrice),
                    IsActive = p.IsActive,
                    IsSoon = p.IsSoon,
                    CategoryId = p.CategoryId.HasValue && branchFilter.CategoryIds.Contains(p.CategoryId.Value) ? p.CategoryId : null,
                    CategoryName = p.Category != null && p.CategoryId.HasValue && branchFilter.CategoryIds.Contains(p.CategoryId.Value) ? p.Category.Name : null,
                    CategoryNameAr = p.Category != null && p.CategoryId.HasValue && branchFilter.CategoryIds.Contains(p.CategoryId.Value) ? p.Category.NameAr : null,
                    CategoryDisplayName = p.Category != null && p.CategoryId.HasValue && branchFilter.CategoryIds.Contains(p.CategoryId.Value)
                        ? (isArabic && p.Category.NameAr != null && p.Category.NameAr != string.Empty ? p.Category.NameAr : p.Category.Name)
                        : null,
                    SubcategoryId = p.Subcategory != null && p.Subcategory.IsActive && p.SubcategoryId.HasValue && branchFilter.SubcategoryIds.Contains(p.SubcategoryId.Value) ? p.SubcategoryId : null,
                    SubcategoryName = p.Subcategory != null && p.Subcategory.IsActive && p.SubcategoryId.HasValue && branchFilter.SubcategoryIds.Contains(p.SubcategoryId.Value) ? p.Subcategory.Name : null,
                    SubcategoryNameAr = p.Subcategory != null && p.Subcategory.IsActive && p.SubcategoryId.HasValue && branchFilter.SubcategoryIds.Contains(p.SubcategoryId.Value) ? p.Subcategory.NameAr : null,
                    SubcategoryDisplayName = p.Subcategory != null && p.Subcategory.IsActive && p.SubcategoryId.HasValue && branchFilter.SubcategoryIds.Contains(p.SubcategoryId.Value)
                        ? (isArabic && p.Subcategory.NameAr != null && p.Subcategory.NameAr != string.Empty ? p.Subcategory.NameAr : p.Subcategory.Name)
                        : null,
                    Allergens = (int)p.Allergens,
                    StationRouting = (int)p.StationRouting,
                    PrinterIds = p.PrinterIds,
                    KitchenId = p.KitchenId,
                    KitchenName = p.Kitchen != null ? p.Kitchen.Name : null,
                    KitchenNameAr = p.Kitchen != null ? p.Kitchen.NameAr : null,
                    AllKitchenPrinters = p.AllKitchenPrinters,
                    KitchenPrinterIds = p.KitchenPrinters.Select(kp => kp.KitchenPrinterId).ToList(),
                    ImageUrl = p.ImageUrl,
                    ImageKey = p.ImageKey,
                    IsAvailableNow = AvailabilityHelper.IsAvailableNow(
                        p.AvailableStartDate,
                        p.AvailableEndDate,
                        p.AvailableFrom,
                        p.AvailableTo),
                    AvailableStartDate = p.AvailableStartDate,
                    AvailableEndDate = p.AvailableEndDate,
                    AvailableFrom = p.AvailableFrom,
                    AvailableTo = p.AvailableTo,
                    ModifierGroups = p.ProductModifierGroups
                        .Where(pmg => branchFilter.ModifierGroupIds.Contains(pmg.ModifierGroupId))
                        .OrderBy(pmg => pmg.SortOrder)
                        .Select(pmg => new ModifierGroupDto
                        {
                            Id = pmg.ModifierGroup.Id,
                            Name = pmg.ModifierGroup.Name,
                            NameAr = pmg.ModifierGroup.NameAr,
                            DisplayName = isArabic && pmg.ModifierGroup.NameAr != null && pmg.ModifierGroup.NameAr != string.Empty
                                ? pmg.ModifierGroup.NameAr
                                : pmg.ModifierGroup.Name,
                            SelectionType = (int)pmg.ModifierGroup.SelectionType,
                            DisplayType = (int)pmg.ModifierGroup.DisplayType,
                            MinSelection = pmg.ModifierGroup.MinSelection,
                            MaxSelection = pmg.ModifierGroup.MaxSelection,
                            IsRequired = pmg.ModifierGroup.IsRequired,
                            AvailableForOrderTypes = (int)pmg.ModifierGroup.AvailableForOrderTypes,
                            PrintOnReceipt = pmg.ModifierGroup.PrintOnReceipt,
                            PrintInKitchen = pmg.ModifierGroup.PrintInKitchen,
                            Modifiers = pmg.ModifierGroup.Modifiers
                                .Where(m => m.IsActive && branchFilter.ModifierIds.Contains(m.Id))
                                .Select(m => new ModifierDto
                                {
                                    Id = m.Id,
                                    Name = m.Name,
                                    NameAr = m.NameAr,
                                    DisplayName = isArabic && m.NameAr != null && m.NameAr != string.Empty ? m.NameAr : m.Name,
                                    PriceAdjustment = m.PriceAdjustment,
                                    PricingType = m.LinkedProductId.HasValue ? (int)PricingType.Fixed : (int)m.PricingType,
                                    IsFree = m.IsFree,
                                    FreeQuantityLimit = m.FreeQuantityLimit,
                                    MaxQuantity = m.MaxQuantity,
                                    LinkedRawMaterialId = m.LinkedRawMaterialId,
                                    LinkedProductId = m.LinkedProductId,
                                    LinkedMaterialAmount = m.LinkedMaterialAmount,
                                    IsDefault = m.IsDefault,
                                    IsActive = m.IsActive,
                                    RecipeItems = m.RecipeItems.Select(ri => new ModifierRecipeItemDto
                                    {
                                        Id = ri.Id,
                                        RawMaterialId = ri.RawMaterialId,
                                        RawMaterialName = string.Empty,
                                        Amount = ri.Amount,
                                        Unit = string.Empty
                                    }).Take(1).ToList()
                                }).ToList()
                        }).ToList(),
                    RecipeItems = p.RecipeItems
                        .Where(r => r.RawMaterial.ShowInMenu || r.Alternatives.Any(a => a.IsActive))
                        .Select(r => new RecipeItemDto
                        {
                            Id = r.Id,
                            RawMaterialId = r.RawMaterialId,
                            RawMaterialName = r.RawMaterial.Name,
                            RawMaterialNameAr = r.RawMaterial.NameAr,
                            ShowInMenu = r.RawMaterial.ShowInMenu,
                            IsPostPrice = r.RawMaterial.IsPostPrice,
                            Amount = r.Amount,
                            Unit = r.RawMaterial.Unit.ToString(),
                            Alternatives = r.Alternatives
                                .Where(a => a.IsActive)
                                .OrderBy(a => a.SortOrder)
                                .Select(a => new RecipeItemAlternativeDto
                                {
                                    Id = a.Id,
                                    RecipeItemId = a.RecipeItemId,
                                    RawMaterialId = a.RawMaterialId,
                                    RawMaterialName = a.RawMaterial.Name,
                                    RawMaterialNameAr = a.RawMaterial.NameAr,
                                    Name = a.Name,
                                    NameAr = a.NameAr,
                                    Amount = a.Amount,
                                    Unit = a.RawMaterial.Unit.ToString(),
                                    PriceAdjustment = a.PriceAdjustment,
                                    AlternativePricingType = (int)a.PricingType,
                                    CustomerAdditionalPrice = a.CustomerAdditionalPrice,
                                    FixedOverridePrice = a.FixedOverridePrice,
                                    CustomerPriceImpact = a.PricingType == RecipeItemAlternativePricingType.Included
                                        ? 0m
                                        : a.PricingType == RecipeItemAlternativePricingType.FixedOverride
                                            ? (a.FixedOverridePrice ?? a.PriceAdjustment) - (p.DiscountedPrice ?? p.BasePrice)
                                            : a.CustomerAdditionalPrice ?? (a.PriceAdjustment - (r.Amount * r.RawMaterial.CostPerUnit)),
                                    IsDefault = a.IsDefault,
                                    IsActive = a.IsActive,
                                    SortOrder = a.SortOrder,
                                }).ToList()
                        }).ToList(),
                    Options = p.Options
                        .Where(o => o.IsActive && branchFilter.ProductOptionIds.Contains(o.Id))
                        .OrderBy(o => o.SortOrder)
                        .Select(o => new ProductOptionDto
                        {
                            Id = o.Id,
                            Name = o.Name,
                            NameAr = o.NameAr,
                            Price = o.Price,
                            IsDefault = o.IsDefault,
                            IsActive = o.IsActive,
                            SortOrder = o.SortOrder,
                        }).ToList()
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<ProductDto>> QueryProductDtos(
            Guid tenantId,
            bool isArabic,
            bool activeOnly,
            CancellationToken cancellationToken,
            IReadOnlyCollection<Guid>? productIds = null)
        {
            var query = _context.Products
                .AsNoTracking()
                .AsSplitQuery()  // 5-level nested collection projection (ModifierGroups → Modifiers → RecipeItems → Alternatives → RawMaterials) caused a cartesian explosion and 12s+ /products timeouts. Split-query emits one SQL per collection, keeping each result set linear in row count.
                .Where(p => p.TenantId == tenantId);

            if (activeOnly)
                query = query.Where(p => p.IsActive);
            if (productIds is { Count: > 0 })
                query = query.Where(p => productIds.Contains(p.Id));

            return await query
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    NameAr = p.NameAr,
                    DisplayName = isArabic && p.NameAr != null && p.NameAr != string.Empty ? p.NameAr : p.Name,
                    Description = p.Description,
                    DescriptionAr = p.DescriptionAr,
                    Calories = p.Calories,
                    BasePrice = p.BasePrice,
                    CostPrice = p.CostPrice,
                    PricingMode = p.PricingMode,
                    Markup = p.Markup,
                    MarkupType = p.MarkupType,
                    DiscountPercentage = p.DiscountPercentage,
                    DiscountedPrice = p.DiscountedPrice,
                    CustomPrice = p.CustomPrice,
                    UseCustomPrice = p.UseCustomPrice,
                    DisplayPrice = p.UseCustomPrice && p.CustomPrice.HasValue
                        ? p.CustomPrice.Value
                        : (p.DiscountedPrice ?? p.BasePrice),
                    IsActive = p.IsActive,
                    IsSoon = p.IsSoon,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategoryNameAr = p.Category != null ? p.Category.NameAr : null,
                    CategoryDisplayName = p.Category != null
                        ? (isArabic && p.Category.NameAr != null && p.Category.NameAr != string.Empty ? p.Category.NameAr : p.Category.Name)
                        : null,
                    SubcategoryId = p.SubcategoryId,
                    SubcategoryName = p.Subcategory != null ? p.Subcategory.Name : null,
                    SubcategoryNameAr = p.Subcategory != null ? p.Subcategory.NameAr : null,
                    SubcategoryDisplayName = p.Subcategory != null
                        ? (isArabic && p.Subcategory.NameAr != null && p.Subcategory.NameAr != string.Empty ? p.Subcategory.NameAr : p.Subcategory.Name)
                        : null,
                    Allergens = (int)p.Allergens,
                    StationRouting = (int)p.StationRouting,
                    PrinterIds = p.PrinterIds,
                    KitchenId = p.KitchenId,
                    KitchenName = p.Kitchen != null ? p.Kitchen.Name : null,
                    KitchenNameAr = p.Kitchen != null ? p.Kitchen.NameAr : null,
                    AllKitchenPrinters = p.AllKitchenPrinters,
                    KitchenPrinterIds = p.KitchenPrinters.Select(kp => kp.KitchenPrinterId).ToList(),
                    ImageUrl = p.ImageUrl,
                    ImageKey = p.ImageKey,
                    Images = p.Images
                        .OrderBy(i => i.SortOrder)
                        .Select(i => new ProductImageDto
                        {
                            Id = i.Id,
                            ImageUrl = i.ImageUrl,
                            ImageKey = i.ImageKey,
                            AltText = i.AltText,
                            SortOrder = i.SortOrder,
                        }).ToList(),
                    IsAvailableNow = AvailabilityHelper.IsAvailableNow(
                        p.AvailableStartDate,
                        p.AvailableEndDate,
                        p.AvailableFrom,
                        p.AvailableTo),
                    AvailableStartDate = p.AvailableStartDate,
                    AvailableEndDate = p.AvailableEndDate,
                    AvailableFrom = p.AvailableFrom,
                    AvailableTo = p.AvailableTo,
                    ModifierGroups = p.ProductModifierGroups
                        .OrderBy(pmg => pmg.SortOrder)
                        .Select(pmg => new ModifierGroupDto
                        {
                            Id = pmg.ModifierGroup.Id,
                            Name = pmg.ModifierGroup.Name,
                            NameAr = pmg.ModifierGroup.NameAr,
                            DisplayName = isArabic && pmg.ModifierGroup.NameAr != null && pmg.ModifierGroup.NameAr != string.Empty
                                ? pmg.ModifierGroup.NameAr
                                : pmg.ModifierGroup.Name,
                            SelectionType = (int)pmg.ModifierGroup.SelectionType,
                            DisplayType = (int)pmg.ModifierGroup.DisplayType,
                            MinSelection = pmg.ModifierGroup.MinSelection,
                            MaxSelection = pmg.ModifierGroup.MaxSelection,
                            IsRequired = pmg.ModifierGroup.IsRequired,
                            AvailableForOrderTypes = (int)pmg.ModifierGroup.AvailableForOrderTypes,
                            PrintOnReceipt = pmg.ModifierGroup.PrintOnReceipt,
                            PrintInKitchen = pmg.ModifierGroup.PrintInKitchen,
                            IsShared = pmg.ModifierGroup.ProductGroups.Count > 1,
                            Modifiers = pmg.ModifierGroup.Modifiers
                                .Select(m => new ModifierDto
                                {
                                    Id = m.Id,
                                    Name = m.Name,
                                    NameAr = m.NameAr,
                                    DisplayName = isArabic && m.NameAr != null && m.NameAr != string.Empty ? m.NameAr : m.Name,
                                    PriceAdjustment = m.PriceAdjustment,
                                    PricingType = m.LinkedProductId.HasValue ? (int)PricingType.Fixed : (int)m.PricingType,
                                    IsFree = m.IsFree,
                                    FreeQuantityLimit = m.FreeQuantityLimit,
                                    MaxQuantity = m.MaxQuantity,
                                    LinkedRawMaterialId = m.LinkedRawMaterialId,
                                    LinkedProductId = m.LinkedProductId,
                                    LinkedMaterialAmount = m.LinkedMaterialAmount,
                                    IsDefault = m.IsDefault,
                                    IsActive = m.IsActive,
                                    RecipeItems = m.RecipeItems.Select(ri => new ModifierRecipeItemDto
                                    {
                                        Id = ri.Id,
                                        RawMaterialId = ri.RawMaterialId,
                                        RawMaterialName = ri.RawMaterial.Name,
                                        RawMaterialNameAr = ri.RawMaterial.NameAr,
                                        Amount = ri.Amount,
                                        Unit = ri.RawMaterial.Unit.ToString()
                                    }).ToList()
                                }).ToList()
                        }).ToList(),
                    RecipeItems = p.RecipeItems.Select(r => new RecipeItemDto
                    {
                        Id = r.Id,
                        RawMaterialId = r.RawMaterialId,
                        RawMaterialName = r.RawMaterial.Name,
                        RawMaterialNameAr = r.RawMaterial.NameAr,
                        ShowInMenu = r.RawMaterial.ShowInMenu,
                        IsPostPrice = r.RawMaterial.IsPostPrice,
                        Amount = r.Amount,
                        Unit = r.RawMaterial.Unit.ToString(),
                        UnitCost = r.RawMaterial.CostPerUnit,
                        TotalCost = r.Amount * r.RawMaterial.CostPerUnit,
                        Alternatives = r.Alternatives
                            .Where(a => a.IsActive)
                            .OrderBy(a => a.SortOrder)
                            .Select(a => new RecipeItemAlternativeDto
                            {
                                Id = a.Id,
                                RecipeItemId = a.RecipeItemId,
                                RawMaterialId = a.RawMaterialId,
                                RawMaterialName = a.RawMaterial.Name,
                                RawMaterialNameAr = a.RawMaterial.NameAr,
                                Name = a.Name,
                                NameAr = a.NameAr,
                                Amount = a.Amount,
                                Unit = a.RawMaterial.Unit.ToString(),
                                PriceAdjustment = a.PriceAdjustment,
                                AlternativePricingType = (int)a.PricingType,
                                CustomerAdditionalPrice = a.CustomerAdditionalPrice,
                                FixedOverridePrice = a.FixedOverridePrice,
                                CustomerPriceImpact = a.PricingType == RecipeItemAlternativePricingType.Included
                                    ? 0m
                                    : a.PricingType == RecipeItemAlternativePricingType.FixedOverride
                                        ? (a.FixedOverridePrice ?? a.PriceAdjustment) - (p.DiscountedPrice ?? p.BasePrice)
                                        : a.CustomerAdditionalPrice ?? (a.PriceAdjustment - (r.Amount * r.RawMaterial.CostPerUnit)),
                                CostImpact = (a.Amount * a.RawMaterial.CostPerUnit) - (r.Amount * r.RawMaterial.CostPerUnit),
                                UnitCost = a.RawMaterial.CostPerUnit,
                                TotalCost = a.Amount * a.RawMaterial.CostPerUnit,
                                IsDefault = a.IsDefault,
                                IsActive = a.IsActive,
                                SortOrder = a.SortOrder,
                            }).ToList()
                    }).ToList()
                })
                .ToListAsync(cancellationToken);
        }

        private static ProductDto? ApplyPartnerPrice(
            ProductDto product,
            PartnerProjection partner,
            ProductMappingProjection? mapping)
        {
            if (mapping == null ||
                !mapping.IsEnabled ||
                !mapping.IsAvailable ||
                !AvailabilityHelper.IsAvailableNow(
                    mapping.AvailableStartDate,
                    mapping.AvailableEndDate,
                    mapping.AvailableFrom,
                    mapping.AvailableTo))
            {
                return null;
            }

            var rule = mapping?.PricingRuleType ?? partner.DefaultRule;
            var value = mapping?.PricingRuleType.HasValue == true
                ? mapping.PricingRuleValue ?? 0m
                : partner.DefaultValue;
            var customPrice = rule == DeliveryPartnerPricingRuleType.CustomPrice
                ? mapping?.CustomPrice
                : null;
            var partnerPrice = DeliveryPartnerPricingHelper.CalculatePrice(product.BasePrice, rule, value, customPrice);

            product.DeliveryPartnerId = partner.Id;
            product.DeliveryPartnerName = partner.Name;
            product.DeliveryPartnerNameAr = partner.NameAr;
            product.DeliveryPartnerCode = partner.Code;
            product.PartnerPrice = partnerPrice;
            product.PriceSource = mapping?.PricingRuleType.HasValue == true ? "ProductOverride" : "PartnerDefault";
            product.DisplayPrice = partnerPrice;
            product.DiscountedPrice = null;
            return product;
        }

        private sealed record PartnerProjection(
            Guid Id,
            string Name,
            string? NameAr,
            string Code,
            DeliveryPartnerPricingRuleType DefaultRule,
            decimal DefaultValue);

        private sealed record ProductMappingProjection(
            Guid ProductId,
            bool IsEnabled,
            bool IsAvailable,
            DeliveryPartnerPricingRuleType? PricingRuleType,
            decimal? PricingRuleValue,
            decimal? CustomPrice,
            DateOnly? AvailableStartDate,
            DateOnly? AvailableEndDate,
            TimeOnly? AvailableFrom,
            TimeOnly? AvailableTo);

        public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(mg => mg.Modifiers)
                            .ThenInclude(m => m.RecipeItems)
                                .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.RecipeItems)
                .Include(p => p.KitchenPrinters)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<bool> ProductNameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            var normalizedName = name.Trim().ToLower();
            var query = _context.Products.Where(p => p.Name.ToLower() == normalizedName);

            if (excludeId.HasValue)
                query = query.Where(p => p.Id != excludeId.Value);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> ProductNameArExistsAsync(string nameAr, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            var normalizedNameAr = nameAr.Trim();
            var query = _context.Products.Where(p => p.NameAr != null && p.NameAr == normalizedNameAr);

            if (excludeId.HasValue)
                query = query.Where(p => p.Id != excludeId.Value);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken);
        }

        public async Task<bool> SubcategoryBelongsToCategoryAsync(Guid subcategoryId, Guid categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Subcategories.AnyAsync(s => s.Id == subcategoryId && s.CategoryId == categoryId, cancellationToken);
        }

        public async Task<bool> ModifierGroupExistsAsync(Guid modifierGroupId, CancellationToken cancellationToken = default)
        {
            return await _context.ModifierGroups.AnyAsync(mg => mg.Id == modifierGroupId, cancellationToken);
        }

        public async Task<List<Guid>> GetExistingRawMaterialIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default)
        {
            return await _context.RawMaterials
                .Where(rm => ids.Contains(rm.Id))
                .Select(rm => rm.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<Product> CreateProductAsync(Product product, CancellationToken cancellationToken = default)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);
            return product;
        }

        public async Task<ProductBranchConfigurationCleanup> GetProductBranchConfigurationCleanupAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            var groupIds = await _context.ProductModifierGroups
                .AsNoTracking()
                .Where(pmg => pmg.ProductId == productId)
                .Select(pmg => pmg.ModifierGroupId)
                .ToListAsync(cancellationToken);

            var orphanGroupIds = await GetOrphanModifierGroupIdsAsync(productId, groupIds, cancellationToken);
            var modifierIds = await GetModifierIdsAsync(orphanGroupIds, cancellationToken);
            return new ProductBranchConfigurationCleanup(productId, orphanGroupIds, modifierIds);
        }

        public async Task<ProductModifierBranchConfigurationSeeds> GetProductModifierBranchConfigurationSeedsAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            var modifierGroups = await _context.ProductModifierGroups
                .AsNoTracking()
                .Where(pmg => pmg.ProductId == productId)
                .Select(pmg => new ProductBranchConfigurationSeed(
                    pmg.ModifierGroup.TenantId,
                    pmg.ModifierGroupId,
                    0))
                .ToListAsync(cancellationToken);
            var groupIds = modifierGroups.Select(group => group.EntityId).ToList();
            var modifiers = groupIds.Count == 0
                ? new List<ProductBranchConfigurationSeed>()
                : await _context.Modifiers
                    .AsNoTracking()
                    .Where(modifier => groupIds.Contains(modifier.ModifierGroupId))
                    .Select(modifier => new ProductBranchConfigurationSeed(
                        modifier.TenantId,
                        modifier.Id,
                        0))
                    .ToListAsync(cancellationToken);

            return new ProductModifierBranchConfigurationSeeds(modifierGroups, modifiers);
        }

        public async Task SyncProductKitchenPrintersAsync(
            Guid productId,
            Guid tenantId,
            IReadOnlyCollection<Guid> kitchenPrinterIds,
            CancellationToken cancellationToken = default)
        {
            var existing = await _context.ProductKitchenPrinters
                .Where(m => m.ProductId == productId)
                .ToListAsync(cancellationToken);

            var desired = kitchenPrinterIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToHashSet() ?? new HashSet<Guid>();

            var toRemove = existing.Where(m => !desired.Contains(m.KitchenPrinterId)).ToList();
            if (toRemove.Count > 0)
            {
                _context.ProductKitchenPrinters.RemoveRange(toRemove);
            }

            var existingIds = existing.Select(m => m.KitchenPrinterId).ToHashSet();
            foreach (var printerId in desired.Where(id => !existingIds.Contains(id)))
            {
                _context.ProductKitchenPrinters.Add(new Models.ProductKitchenPrinter
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    KitchenPrinterId = printerId,
                    TenantId = tenantId
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task SyncProductImagesAsync(
            Guid productId,
            Guid tenantId,
            IReadOnlyList<ProductImageInputDto> images,
            CancellationToken cancellationToken = default)
        {
            var existing = await _context.ProductImages
                .Where(i => i.ProductId == productId)
                .ToListAsync(cancellationToken);

            if (existing.Count > 0)
                _context.ProductImages.RemoveRange(existing);

            var order = 0;
            foreach (var image in images)
            {
                var url = image.ImageUrl?.Trim();
                if (string.IsNullOrEmpty(url)) continue;

                _context.ProductImages.Add(new Models.ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    TenantId = tenantId,
                    ImageUrl = url,
                    ImageKey = string.IsNullOrWhiteSpace(image.ImageKey) ? null : image.ImageKey.Trim(),
                    AltText = string.IsNullOrWhiteSpace(image.AltText) ? null : image.AltText.Trim(),
                    SortOrder = order++,
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<Guid>> GetProductKitchenPrinterIdsAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            return await _context.ProductKitchenPrinters
                .AsNoTracking()
                .Where(m => m.ProductId == productId)
                .Select(m => m.KitchenPrinterId)
                .ToListAsync(cancellationToken);
        }

        public async Task<Product> UpdateProductAsync(Product product, CancellationToken cancellationToken = default)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync(cancellationToken);
            return product;
        }

        public async Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var product = await _context.Products
                .Include(p => p.ProductModifierGroups)
                .Include(p => p.RecipeItems)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (product == null)
                throw new InvalidOperationException($"Product {id} not found.");

            var groupIdsToCheck = product.ProductModifierGroups.Select(pmg => pmg.ModifierGroupId).ToList();
            _context.ProductModifierGroups.RemoveRange(product.ProductModifierGroups);
            _context.RecipeItems.RemoveRange(product.RecipeItems);
            _context.Products.Remove(product);

            await _context.SaveChangesAsync(cancellationToken);

            // Clean up orphaned modifier groups
            foreach (var gid in groupIdsToCheck)
            {
                var usedElsewhere = await _context.ProductModifierGroups.AnyAsync(pmg => pmg.ModifierGroupId == gid, cancellationToken);
                if (!usedElsewhere)
                {
                    var orphan = await _context.ModifierGroups.Include(g => g.Modifiers).FirstOrDefaultAsync(g => g.Id == gid, cancellationToken);
                    if (orphan != null)
                    {
                        _context.Modifiers.RemoveRange(orphan.Modifiers);
                        _context.ModifierGroups.Remove(orphan);
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<RecipeItem> AddRecipeItemAsync(RecipeItem recipeItem, CancellationToken cancellationToken = default)
        {
            _context.RecipeItems.Add(recipeItem);
            await _context.SaveChangesAsync(cancellationToken);
            return recipeItem;
        }

        public async Task<RecipeItem?> GetRecipeItemWithProductAsync(
            Guid recipeItemId,
            CancellationToken cancellationToken = default)
        {
            return await _context.RecipeItems
                .Include(r => r.Product)
                    .ThenInclude(p => p.RecipeItems)
                        .ThenInclude(r => r.RawMaterial)
                .FirstOrDefaultAsync(r => r.Id == recipeItemId, cancellationToken);
        }

        public async Task UpdateRecipeItemAsync(
            RecipeItem recipeItem,
            CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteRecipeItemAsync(
            RecipeItem recipeItem,
            CancellationToken cancellationToken = default)
        {
            _context.RecipeItems.Remove(recipeItem);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<RecipeItem>> GetRecipeItemsWithMaterialsAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            return await _context.RecipeItems
                .Where(r => r.ProductId == productId)
                .Include(r => r.RawMaterial)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Product>> GetProductsByRawMaterialIdWithRecipeItemsAsync(
            Guid rawMaterialId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Where(p => p.RecipeItems.Any(r => r.RawMaterialId == rawMaterialId))
                .Include(p => p.RecipeItems)
                    .ThenInclude(r => r.RawMaterial)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateProductsAsync(
            IEnumerable<Product> products,
            CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> BulkImportProductsAsync(List<Product> products, CancellationToken cancellationToken = default)
        {
            _context.Products.AddRange(products);
            await _context.SaveChangesAsync(cancellationToken);
            return products.Count;
        }

        public async Task<ProductBranchConfigurationCleanup> GetModifierGroupCleanupForProductUpdateAsync(
            Guid productId,
            IReadOnlyCollection<Guid> newLinkedGroupIds,
            CancellationToken cancellationToken = default)
        {
            var oldGroupIds = await _context.ProductModifierGroups
                .AsNoTracking()
                .Where(pmg => pmg.ProductId == productId)
                .Select(pmg => pmg.ModifierGroupId)
                .ToListAsync(cancellationToken);

            var newGroupIds = newLinkedGroupIds.ToHashSet();
            var candidates = oldGroupIds
                .Where(groupId => !newGroupIds.Contains(groupId))
                .ToList();
            var orphanGroupIds = await GetOrphanModifierGroupIdsAsync(productId, candidates, cancellationToken);
            var modifierIds = await GetModifierIdsAsync(orphanGroupIds, cancellationToken);
            return new ProductBranchConfigurationCleanup(productId, orphanGroupIds, modifierIds);
        }

        public async Task UpdateProductModifierGroupsAsync(Product product, List<ModifierGroupCreateDto> modifierGroupDtos, CancellationToken cancellationToken = default)
        {
            // Phase 1: Remove bridge rows, then clean up orphaned groups
            var oldGroupIds = product.ProductModifierGroups
                .Select(pmg => pmg.ModifierGroupId)
                .ToList();

            _context.ProductModifierGroups.RemoveRange(product.ProductModifierGroups);
            await _context.SaveChangesAsync(cancellationToken);

            // Collect new group IDs being linked (to know which old groups are still referenced)
            var newLinkedGroupIds = modifierGroupDtos
                .Where(mg => mg.ExistingGroupId.HasValue && mg.ExistingGroupId.Value != Guid.Empty)
                .Select(mg => mg.ExistingGroupId!.Value)
                .ToHashSet();

            // Delete orphaned groups: groups that were linked to THIS product
            // and are NOT used by any OTHER product and NOT being re-linked
            foreach (var oldGroupId in oldGroupIds)
            {
                if (newLinkedGroupIds.Contains(oldGroupId)) continue; // being re-linked

                var usedElsewhere = await _context.ProductModifierGroups
                    .AnyAsync(pmg => pmg.ModifierGroupId == oldGroupId, cancellationToken);

                if (!usedElsewhere)
                {
                    var orphan = await _context.ModifierGroups
                        .Include(g => g.Modifiers)
                        .FirstOrDefaultAsync(g => g.Id == oldGroupId, cancellationToken);

                    if (orphan != null)
                    {
                        _context.Modifiers.RemoveRange(orphan.Modifiers);
                        _context.ModifierGroups.Remove(orphan);
                    }
                }
            }
            await _context.SaveChangesAsync(cancellationToken);

            // Phase 2: Create bridges — link existing or create inline
            var newBridges = new List<ProductModifierGroup>();

            foreach (var (mg, index) in modifierGroupDtos.Select((mg, i) => (mg, i)))
            {
                if (mg.ExistingGroupId.HasValue && mg.ExistingGroupId.Value != Guid.Empty)
                {
                    // Link to existing library group
                    var exists = await _context.ModifierGroups.AnyAsync(g => g.Id == mg.ExistingGroupId.Value, cancellationToken);
                    if (!exists)
                        throw new InvalidOperationException($"Modifier group '{mg.ExistingGroupId}' not found.");

                    newBridges.Add(new ProductModifierGroup
                    {
                        ProductId = product.Id,
                        ModifierGroupId = mg.ExistingGroupId.Value,
                        SortOrder = index,
                        TenantId = product.TenantId
                    });
                }
                else
                {
                    // Create new inline group
                    var newGroup = new ModifierGroup
                    {
                        Id = Guid.NewGuid(),
                        Name = mg.Name,
                        NameAr = mg.NameAr,
                        SelectionType = (SelectionType)mg.SelectionType,
                        MinSelection = mg.MinSelection,
                        MaxSelection = mg.MaxSelection,
                        TenantId = product.TenantId,
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
                            TenantId = product.TenantId,
                            RecipeItems = m.RecipeItems.Select(ri => new ModifierRecipeItem
                            {
                                Id = Guid.NewGuid(),
                                RawMaterialId = ri.RawMaterialId,
                                Amount = ri.Amount,
                                TenantId = product.TenantId
                            }).ToList()
                        }).ToList()
                    };

                    _context.ModifierGroups.Add(newGroup);
                    await _context.SaveChangesAsync(cancellationToken); // Save group first

                    newBridges.Add(new ProductModifierGroup
                    {
                        ProductId = product.Id,
                        ModifierGroupId = newGroup.Id,
                        SortOrder = index,
                        TenantId = product.TenantId
                    });
                }
            }

            // Add all bridge rows
            _context.ProductModifierGroups.AddRange(newBridges);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<List<Guid>> GetOrphanModifierGroupIdsAsync(
            Guid productId,
            IReadOnlyCollection<Guid> groupIds,
            CancellationToken cancellationToken)
        {
            if (groupIds.Count == 0)
                return new List<Guid>();

            var usedElsewhere = await _context.ProductModifierGroups
                .AsNoTracking()
                .Where(pmg => pmg.ProductId != productId && groupIds.Contains(pmg.ModifierGroupId))
                .Select(pmg => pmg.ModifierGroupId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var usedElsewhereSet = usedElsewhere.ToHashSet();
            return groupIds.Where(groupId => !usedElsewhereSet.Contains(groupId)).Distinct().ToList();
        }

        private async Task<List<Guid>> GetModifierIdsAsync(
            IReadOnlyCollection<Guid> modifierGroupIds,
            CancellationToken cancellationToken)
        {
            if (modifierGroupIds.Count == 0)
                return new List<Guid>();

            return await _context.Modifiers
                .AsNoTracking()
                .Where(modifier => modifierGroupIds.Contains(modifier.ModifierGroupId))
                .Select(modifier => modifier.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
