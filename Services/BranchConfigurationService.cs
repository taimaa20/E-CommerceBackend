using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services.Caching;

namespace RestaurantPos.Api.Services
{
    public class BranchConfigurationService : IBranchConfigurationService
    {
        private readonly IBranchConfigurationRepository _repository;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IConfigAuditService _audit;
        private readonly ICacheService _cache;

        private Guid TenantId => _tenantResolver.GetTenantId();
        private Guid? UserId => _currentUser.UserIdOrNull;

        public BranchConfigurationService(
            IBranchConfigurationRepository repository,
            ITenantResolver tenantResolver,
            ICurrentUserAccessor currentUser,
            IConfigAuditService audit,
            ICacheService cache)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<BranchConfigurationOptionsDto> GetOptionsAsync(CancellationToken ct = default)
            => new()
            {
                Branches = (await _repository.GetBranchOptionsAsync(ct)).Select(MapBranchOption).ToList(),
                Products = (await _repository.GetProductOptionsAsync(ct)).Select(MapProductOption).ToList(),
                Categories = (await _repository.GetCategoryOptionsAsync(ct)).Select(MapCategoryOption).ToList(),
                Subcategories = (await _repository.GetSubcategoryOptionsAsync(ct)).Select(MapSubcategoryOption).ToList(),
                Modifiers = (await _repository.GetModifierOptionsAsync(ct)).Select(MapModifierOption).ToList(),
                ModifierGroups = (await _repository.GetModifierGroupOptionsAsync(ct)).Select(MapModifierGroupOption).ToList(),
                ProductOptions = (await _repository.GetProductOptionOptionsAsync(ct)).Select(MapProductOptionOption).ToList(),
                PaymentMethods = (await _repository.GetPaymentMethodOptionsAsync(ct)).Select(MapPaymentMethodOption).ToList(),
                DeliveryPartners = (await _repository.GetDeliveryPartnerOptionsAsync(ct)).Select(MapDeliveryPartnerOption).ToList(),
                Printers = (await _repository.GetPrinterOptionsAsync(ct)).Select(MapPrinterOption).ToList(),
                Offers = (await _repository.GetOfferOptionsAsync(ct)).Select(MapOfferOption).ToList()
            };

        public async Task<IReadOnlySet<Guid>> GetAvailableProductIdsAsync(Guid branchId, CancellationToken ct = default)
            => (await _repository.GetAvailableProductIdsAsync(branchId, ct)).ToHashSet();

        public async Task<IReadOnlySet<Guid>> GetVisibleCategoryIdsAsync(Guid branchId, CancellationToken ct = default)
            => (await _repository.GetVisibleCategoryIdsAsync(branchId, ct)).ToHashSet();

        public async Task<IReadOnlySet<Guid>> GetVisibleSubcategoryIdsAsync(Guid branchId, CancellationToken ct = default)
            => (await _repository.GetVisibleSubcategoryIdsAsync(branchId, ct)).ToHashSet();

        public async Task<IReadOnlySet<Guid>> GetAvailableModifierIdsAsync(Guid branchId, CancellationToken ct = default)
            => (await _repository.GetAvailableModifierIdsAsync(branchId, ct)).ToHashSet();

        public async Task<IReadOnlySet<Guid>> GetAvailableModifierGroupIdsAsync(Guid branchId, CancellationToken ct = default)
            => (await _repository.GetAvailableModifierGroupIdsAsync(branchId, ct)).ToHashSet();

        public async Task<IReadOnlySet<Guid>> GetAvailableProductOptionIdsAsync(Guid branchId, CancellationToken ct = default)
            => (await _repository.GetAvailableProductOptionIdsAsync(branchId, ct)).ToHashSet();

        public async Task<IReadOnlySet<Guid>> GetEnabledPaymentMethodIdsAsync(Guid branchId, CancellationToken ct = default)
            => (await _repository.GetEnabledPaymentMethodIdsAsync(branchId, ct)).ToHashSet();

        public async Task<IReadOnlySet<Guid>> GetEnabledDeliveryPartnerIdsAsync(Guid branchId, CancellationToken ct = default)
            => (await _repository.GetEnabledDeliveryPartnerIdsAsync(branchId, ct)).ToHashSet();

        public async Task<IReadOnlySet<Guid>> GetEnabledPrinterIdsAsync(Guid branchId, CancellationToken ct = default)
            => (await _repository.GetEnabledPrinterIdsAsync(branchId, ct)).ToHashSet();

        public async Task<IReadOnlySet<int>> GetEnabledOfferIdsAsync(Guid branchId, CancellationToken ct = default)
            => (await _repository.GetEnabledOfferIdsAsync(branchId, ct)).ToHashSet();

        public Task EnsureMainBranchProductsAsync(
            IReadOnlyCollection<MainBranchConfigurationSeed> products,
            CancellationToken ct = default)
            => EnsureMainBranchConfigurationsAsync(
                products,
                _repository.GetBranchProductKeysAsync,
                (tenantId, branchId, productId, displayOrder) => new BranchProduct
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    ProductId = productId,
                    IsAvailable = true,
                    IsVisible = true,
                    DisplayOrder = displayOrder
                },
                ct);

        public Task EnsureMainBranchCategoriesAsync(
            IReadOnlyCollection<MainBranchConfigurationSeed> categories,
            CancellationToken ct = default)
            => EnsureMainBranchConfigurationsAsync(
                categories,
                _repository.GetBranchCategoryKeysAsync,
                (tenantId, branchId, categoryId, displayOrder) => new BranchCategory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    CategoryId = categoryId,
                    IsVisible = true,
                    DisplayOrder = displayOrder
                },
                ct);

        public Task EnsureMainBranchSubcategoriesAsync(
            IReadOnlyCollection<MainBranchConfigurationSeed> subcategories,
            CancellationToken ct = default)
            => EnsureMainBranchConfigurationsAsync(
                subcategories,
                _repository.GetBranchSubcategoryKeysAsync,
                (tenantId, branchId, subcategoryId, displayOrder) => new BranchSubcategory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    SubcategoryId = subcategoryId,
                    IsVisible = true,
                    DisplayOrder = displayOrder
                },
                ct);

        public Task EnsureMainBranchModifierGroupsAsync(
            IReadOnlyCollection<MainBranchConfigurationSeed> modifierGroups,
            CancellationToken ct = default)
            => EnsureMainBranchConfigurationsAsync(
                modifierGroups,
                _repository.GetBranchModifierGroupKeysAsync,
                (tenantId, branchId, modifierGroupId, displayOrder) => new BranchModifierGroup
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    ModifierGroupId = modifierGroupId,
                    IsAvailable = true,
                    IsVisible = true,
                    DisplayOrder = displayOrder
                },
                ct);

        public Task EnsureMainBranchModifiersAsync(
            IReadOnlyCollection<MainBranchConfigurationSeed> modifiers,
            CancellationToken ct = default)
            => EnsureMainBranchConfigurationsAsync(
                modifiers,
                _repository.GetBranchModifierKeysAsync,
                (tenantId, branchId, modifierId, _) => new BranchModifier
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    ModifierId = modifierId,
                    IsAvailable = true
                },
                ct);

        public Task EnsureMainBranchProductOptionsAsync(
            IReadOnlyCollection<MainBranchConfigurationSeed> productOptions,
            CancellationToken ct = default)
            => EnsureMainBranchConfigurationsAsync(
                productOptions,
                _repository.GetBranchProductOptionKeysAsync,
                (tenantId, branchId, productOptionId, displayOrder) => new BranchProductOption
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    ProductOptionId = productOptionId,
                    IsAvailable = true,
                    IsVisible = true,
                    DisplayOrder = displayOrder
                },
                ct);

        public Task EnsureMainBranchPaymentMethodsAsync(
            IReadOnlyCollection<MainBranchConfigurationSeed> paymentMethods,
            CancellationToken ct = default)
            => EnsureMainBranchConfigurationsAsync(
                paymentMethods,
                _repository.GetBranchPaymentMethodKeysAsync,
                (tenantId, branchId, paymentMethodId, _) => new BranchPaymentMethod
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    PaymentMethodId = paymentMethodId,
                    IsEnabled = true
                },
                ct);

        public Task EnsureMainBranchDeliveryPartnersAsync(
            IReadOnlyCollection<MainBranchConfigurationSeed> deliveryPartners,
            CancellationToken ct = default)
            => EnsureMainBranchConfigurationsAsync(
                deliveryPartners,
                _repository.GetBranchDeliveryPartnerKeysAsync,
                (tenantId, branchId, deliveryPartnerId, _) => new BranchDeliveryPartner
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    DeliveryPartnerId = deliveryPartnerId,
                    IsEnabled = true
                },
                ct);

        public Task EnsureMainBranchPrintersAsync(
            IReadOnlyCollection<MainBranchConfigurationSeed> printers,
            CancellationToken ct = default)
            => EnsureMainBranchConfigurationsAsync(
                printers,
                _repository.GetBranchPrinterKeysAsync,
                (tenantId, branchId, printerId, displayOrder) => new BranchPrinter
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    PrinterId = printerId,
                    IsEnabled = true,
                    DisplayOrder = displayOrder
                },
                ct);

        // Offers use an int PK, so they bypass the Guid-seed helper: one offer, main branch only.
        public async Task EnsureMainBranchOfferAsync(Guid tenantId, int offerId, CancellationToken ct = default)
        {
            if (tenantId == Guid.Empty || offerId <= 0)
                return;

            var mainBranches = await _repository.GetMainBranchIdsByTenantAsync(new[] { tenantId }, ct);
            if (!mainBranches.TryGetValue(tenantId, out var mainBranchId))
                return;

            var existing = (await _repository.GetBranchOfferKeysAsync(new[] { offerId }, ct)).ToHashSet();
            if (existing.Contains((mainBranchId, offerId)))
                return;

            await _repository.AddAsync(new BranchOffer
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = mainBranchId,
                OfferId = offerId,
                IsEnabled = true
            }, ct);
        }

        public Task RemoveOfferBranchConfigurationsAsync(int offerId, CancellationToken ct = default)
            => _repository.RemoveBranchOffersByOfferIdAsync(offerId, ct);

        public async Task RemoveProductBranchConfigurationsAsync(Guid productId, CancellationToken ct = default)
        {
            await _repository.RemoveBranchProductsByProductIdAsync(productId, ct);
            await _repository.RemoveBranchProductOptionsByProductIdAsync(productId, ct);
        }

        public Task RemoveProductOptionBranchConfigurationsAsync(Guid productOptionId, CancellationToken ct = default)
            => _repository.RemoveBranchProductOptionsByProductOptionIdAsync(productOptionId, ct);

        public Task RemoveCategoryBranchConfigurationsAsync(Guid categoryId, CancellationToken ct = default)
            => _repository.RemoveBranchCategoriesByCategoryIdAsync(categoryId, ct);

        public Task RemoveSubcategoryBranchConfigurationsAsync(Guid subcategoryId, CancellationToken ct = default)
            => _repository.RemoveBranchSubcategoriesBySubcategoryIdAsync(subcategoryId, ct);

        public Task RemoveModifierBranchConfigurationsAsync(
            IReadOnlyCollection<Guid> modifierIds,
            CancellationToken ct = default)
            => _repository.RemoveBranchModifiersByModifierIdsAsync(modifierIds, ct);

        public Task RemoveModifierGroupBranchConfigurationsAsync(
            IReadOnlyCollection<Guid> modifierGroupIds,
            CancellationToken ct = default)
            => _repository.RemoveBranchModifierGroupsByModifierGroupIdsAsync(modifierGroupIds, ct);

        public async Task<BranchConfigurationPagedResultDto<BranchProductConfigurationDto>> GetBranchProductsAsync(
            Guid? branchId,
            Guid? productId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchProductsAsync(branchId, productId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchProduct);
        }

        public async Task<BranchProductConfigurationDto> CreateBranchProductAsync(BranchProductConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            await ValidateBranchProductAsync(dto, null, ct);
            var entity = new BranchProduct
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = dto.BranchId,
                ProductId = dto.ProductId,
                IsAvailable = dto.IsAvailable,
                IsVisible = dto.IsVisible,
                DisplayOrder = dto.DisplayOrder,
                CreatedById = UserId,
                UpdatedById = UserId
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchProductAsync(entity.Id, ct);
            var next = MapBranchProduct(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchProductCreated", ct);
            await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task<BranchProductConfigurationDto> UpdateBranchProductAsync(Guid id, BranchProductConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchProductAsync(id, ct);
            var previous = MapBranchProduct(entity);
            await ValidateBranchProductAsync(dto, id, ct);
            entity.BranchId = dto.BranchId;
            entity.ProductId = dto.ProductId;
            entity.IsAvailable = dto.IsAvailable;
            entity.IsVisible = dto.IsVisible;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.UpdatedById = UserId;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchProductAsync(id, ct);
            var next = MapBranchProduct(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchProductUpdated", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
            if (previous.BranchId != next.BranchId)
                await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task DeleteBranchProductAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchProductAsync(id, ct);
            var previous = MapBranchProduct(entity);
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchProductDeleted", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
        }

        public async Task<BranchConfigurationPagedResultDto<BranchCategoryConfigurationDto>> GetBranchCategoriesAsync(
            Guid? branchId,
            Guid? categoryId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchCategoriesAsync(branchId, categoryId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchCategory);
        }

        public async Task<BranchCategoryConfigurationDto> CreateBranchCategoryAsync(BranchCategoryConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            await ValidateBranchCategoryAsync(dto, null, ct);
            var entity = new BranchCategory
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = dto.BranchId,
                CategoryId = dto.CategoryId,
                IsVisible = dto.IsVisible,
                DisplayOrder = dto.DisplayOrder,
                CreatedById = UserId,
                UpdatedById = UserId
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchCategoryAsync(entity.Id, ct);
            var next = MapBranchCategory(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchCategoryCreated", ct);
            await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task<BranchCategoryConfigurationDto> UpdateBranchCategoryAsync(Guid id, BranchCategoryConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchCategoryAsync(id, ct);
            var previous = MapBranchCategory(entity);
            await ValidateBranchCategoryAsync(dto, id, ct);
            entity.BranchId = dto.BranchId;
            entity.CategoryId = dto.CategoryId;
            entity.IsVisible = dto.IsVisible;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.UpdatedById = UserId;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchCategoryAsync(id, ct);
            var next = MapBranchCategory(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchCategoryUpdated", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
            if (previous.BranchId != next.BranchId)
                await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task DeleteBranchCategoryAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchCategoryAsync(id, ct);
            var previous = MapBranchCategory(entity);
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchCategoryDeleted", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
        }

        public async Task<BranchConfigurationPagedResultDto<BranchSubcategoryConfigurationDto>> GetBranchSubcategoriesAsync(
            Guid? branchId,
            Guid? subcategoryId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchSubcategoriesAsync(branchId, subcategoryId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchSubcategory);
        }

        public async Task<BranchSubcategoryConfigurationDto> CreateBranchSubcategoryAsync(BranchSubcategoryConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            await ValidateBranchSubcategoryAsync(dto, null, ct);
            var entity = new BranchSubcategory
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = dto.BranchId,
                SubcategoryId = dto.SubcategoryId,
                IsVisible = dto.IsVisible,
                DisplayOrder = dto.DisplayOrder,
                CreatedById = UserId,
                UpdatedById = UserId
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchSubcategoryAsync(entity.Id, ct);
            var next = MapBranchSubcategory(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchSubcategoryCreated", ct);
            await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task<BranchSubcategoryConfigurationDto> UpdateBranchSubcategoryAsync(Guid id, BranchSubcategoryConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchSubcategoryAsync(id, ct);
            var previous = MapBranchSubcategory(entity);
            await ValidateBranchSubcategoryAsync(dto, id, ct);
            entity.BranchId = dto.BranchId;
            entity.SubcategoryId = dto.SubcategoryId;
            entity.IsVisible = dto.IsVisible;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.UpdatedById = UserId;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchSubcategoryAsync(id, ct);
            var next = MapBranchSubcategory(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchSubcategoryUpdated", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
            if (previous.BranchId != next.BranchId)
                await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task DeleteBranchSubcategoryAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchSubcategoryAsync(id, ct);
            var previous = MapBranchSubcategory(entity);
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchSubcategoryDeleted", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
        }

        public async Task<BranchConfigurationPagedResultDto<BranchModifierConfigurationDto>> GetBranchModifiersAsync(
            Guid? branchId,
            Guid? modifierId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchModifiersAsync(branchId, modifierId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchModifier);
        }

        public async Task<BranchModifierConfigurationDto> CreateBranchModifierAsync(BranchModifierConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            await ValidateBranchModifierAsync(dto, null, ct);
            var entity = new BranchModifier
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = dto.BranchId,
                ModifierId = dto.ModifierId,
                IsAvailable = dto.IsAvailable,
                CreatedById = UserId,
                UpdatedById = UserId
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchModifierAsync(entity.Id, ct);
            var next = MapBranchModifier(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchModifierCreated", ct);
            await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task<BranchModifierConfigurationDto> UpdateBranchModifierAsync(Guid id, BranchModifierConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchModifierAsync(id, ct);
            var previous = MapBranchModifier(entity);
            await ValidateBranchModifierAsync(dto, id, ct);
            entity.BranchId = dto.BranchId;
            entity.ModifierId = dto.ModifierId;
            entity.IsAvailable = dto.IsAvailable;
            entity.UpdatedById = UserId;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchModifierAsync(id, ct);
            var next = MapBranchModifier(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchModifierUpdated", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
            if (previous.BranchId != next.BranchId)
                await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task DeleteBranchModifierAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchModifierAsync(id, ct);
            var previous = MapBranchModifier(entity);
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchModifierDeleted", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
        }

        public async Task<BranchConfigurationPagedResultDto<BranchModifierGroupConfigurationDto>> GetBranchModifierGroupsAsync(
            Guid? branchId,
            Guid? modifierGroupId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchModifierGroupsAsync(branchId, modifierGroupId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchModifierGroup);
        }

        public async Task<BranchModifierGroupConfigurationDto> CreateBranchModifierGroupAsync(BranchModifierGroupConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            await ValidateBranchModifierGroupAsync(dto, null, ct);
            var entity = new BranchModifierGroup
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = dto.BranchId,
                ModifierGroupId = dto.ModifierGroupId,
                IsAvailable = dto.IsAvailable,
                IsVisible = dto.IsVisible,
                DisplayOrder = dto.DisplayOrder,
                CreatedById = UserId,
                UpdatedById = UserId
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchModifierGroupAsync(entity.Id, ct);
            var next = MapBranchModifierGroup(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchModifierGroupCreated", ct);
            await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task<BranchModifierGroupConfigurationDto> UpdateBranchModifierGroupAsync(Guid id, BranchModifierGroupConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchModifierGroupAsync(id, ct);
            var previous = MapBranchModifierGroup(entity);
            await ValidateBranchModifierGroupAsync(dto, id, ct);
            entity.BranchId = dto.BranchId;
            entity.ModifierGroupId = dto.ModifierGroupId;
            entity.IsAvailable = dto.IsAvailable;
            entity.IsVisible = dto.IsVisible;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.UpdatedById = UserId;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchModifierGroupAsync(id, ct);
            var next = MapBranchModifierGroup(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchModifierGroupUpdated", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
            if (previous.BranchId != next.BranchId)
                await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task DeleteBranchModifierGroupAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchModifierGroupAsync(id, ct);
            var previous = MapBranchModifierGroup(entity);
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchModifierGroupDeleted", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
        }

        public async Task<BranchConfigurationPagedResultDto<BranchProductOptionConfigurationDto>> GetBranchProductOptionsAsync(
            Guid? branchId,
            Guid? productOptionId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchProductOptionsAsync(branchId, productOptionId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchProductOption);
        }

        public async Task<BranchProductOptionConfigurationDto> CreateBranchProductOptionAsync(BranchProductOptionConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            await ValidateBranchProductOptionAsync(dto, null, ct);
            var entity = new BranchProductOption
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = dto.BranchId,
                ProductOptionId = dto.ProductOptionId,
                IsAvailable = dto.IsAvailable,
                IsVisible = dto.IsVisible,
                DisplayOrder = dto.DisplayOrder,
                CreatedById = UserId,
                UpdatedById = UserId
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchProductOptionAsync(entity.Id, ct);
            var next = MapBranchProductOption(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchProductOptionCreated", ct);
            await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task<BranchProductOptionConfigurationDto> UpdateBranchProductOptionAsync(Guid id, BranchProductOptionConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchProductOptionAsync(id, ct);
            var previous = MapBranchProductOption(entity);
            await ValidateBranchProductOptionAsync(dto, id, ct);
            entity.BranchId = dto.BranchId;
            entity.ProductOptionId = dto.ProductOptionId;
            entity.IsAvailable = dto.IsAvailable;
            entity.IsVisible = dto.IsVisible;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.UpdatedById = UserId;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchProductOptionAsync(id, ct);
            var next = MapBranchProductOption(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchProductOptionUpdated", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
            if (previous.BranchId != next.BranchId)
                await InvalidateBranchProductCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task DeleteBranchProductOptionAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchProductOptionAsync(id, ct);
            var previous = MapBranchProductOption(entity);
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchProductOptionDeleted", ct);
            await InvalidateBranchProductCachesAsync(previous.BranchId, ct);
        }

        public async Task<BranchConfigurationPagedResultDto<BranchPaymentMethodConfigurationDto>> GetBranchPaymentMethodsAsync(
            Guid? branchId,
            Guid? paymentMethodId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchPaymentMethodsAsync(branchId, paymentMethodId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchPaymentMethod);
        }

        public async Task<BranchPaymentMethodConfigurationDto> CreateBranchPaymentMethodAsync(BranchPaymentMethodConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            await ValidateBranchPaymentMethodAsync(dto, null, ct);
            var entity = new BranchPaymentMethod
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = dto.BranchId,
                PaymentMethodId = dto.PaymentMethodId,
                IsEnabled = dto.IsEnabled,
                CreatedById = UserId,
                UpdatedById = UserId
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchPaymentMethodAsync(entity.Id, ct);
            var next = MapBranchPaymentMethod(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchPaymentMethodCreated", ct);
            await InvalidateBranchPaymentMethodCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task<BranchPaymentMethodConfigurationDto> UpdateBranchPaymentMethodAsync(Guid id, BranchPaymentMethodConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchPaymentMethodAsync(id, ct);
            var previous = MapBranchPaymentMethod(entity);
            await ValidateBranchPaymentMethodAsync(dto, id, ct);
            entity.BranchId = dto.BranchId;
            entity.PaymentMethodId = dto.PaymentMethodId;
            entity.IsEnabled = dto.IsEnabled;
            entity.UpdatedById = UserId;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchPaymentMethodAsync(id, ct);
            var next = MapBranchPaymentMethod(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchPaymentMethodUpdated", ct);
            await InvalidateBranchPaymentMethodCachesAsync(previous.BranchId, ct);
            if (previous.BranchId != next.BranchId)
                await InvalidateBranchPaymentMethodCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task DeleteBranchPaymentMethodAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchPaymentMethodAsync(id, ct);
            var previous = MapBranchPaymentMethod(entity);
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchPaymentMethodDeleted", ct);
            await InvalidateBranchPaymentMethodCachesAsync(previous.BranchId, ct);
        }

        public async Task<BranchConfigurationPagedResultDto<BranchDeliveryPartnerConfigurationDto>> GetBranchDeliveryPartnersAsync(
            Guid? branchId,
            Guid? deliveryPartnerId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchDeliveryPartnersAsync(branchId, deliveryPartnerId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchDeliveryPartner);
        }

        public async Task<BranchDeliveryPartnerConfigurationDto> CreateBranchDeliveryPartnerAsync(BranchDeliveryPartnerConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            await ValidateBranchDeliveryPartnerAsync(dto, null, ct);
            var entity = new BranchDeliveryPartner
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = dto.BranchId,
                DeliveryPartnerId = dto.DeliveryPartnerId,
                IsEnabled = dto.IsEnabled,
                CreatedById = UserId,
                UpdatedById = UserId
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchDeliveryPartnerAsync(entity.Id, ct);
            var next = MapBranchDeliveryPartner(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchDeliveryPartnerCreated", ct);
            await InvalidateBranchDeliveryPartnerCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task<BranchDeliveryPartnerConfigurationDto> UpdateBranchDeliveryPartnerAsync(Guid id, BranchDeliveryPartnerConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchDeliveryPartnerAsync(id, ct);
            var previous = MapBranchDeliveryPartner(entity);
            await ValidateBranchDeliveryPartnerAsync(dto, id, ct);
            entity.BranchId = dto.BranchId;
            entity.DeliveryPartnerId = dto.DeliveryPartnerId;
            entity.IsEnabled = dto.IsEnabled;
            entity.UpdatedById = UserId;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchDeliveryPartnerAsync(id, ct);
            var next = MapBranchDeliveryPartner(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchDeliveryPartnerUpdated", ct);
            await InvalidateBranchDeliveryPartnerCachesAsync(previous.BranchId, ct);
            if (previous.BranchId != next.BranchId)
                await InvalidateBranchDeliveryPartnerCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task DeleteBranchDeliveryPartnerAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchDeliveryPartnerAsync(id, ct);
            var previous = MapBranchDeliveryPartner(entity);
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchDeliveryPartnerDeleted", ct);
            await InvalidateBranchDeliveryPartnerCachesAsync(previous.BranchId, ct);
        }

        public async Task<BranchConfigurationPagedResultDto<BranchDeliveryZoneConfigurationDto>> GetBranchDeliveryZonesAsync(
            Guid? branchId,
            Guid? deliveryZoneId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchDeliveryZonesAsync(branchId, deliveryZoneId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchDeliveryZone);
        }

        public async Task<BranchDeliveryZoneConfigurationDto> CreateBranchDeliveryZoneAsync(BranchDeliveryZoneConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var normalized = NormalizeBranchDeliveryZone(dto);
            await ValidateBranchDeliveryZoneAsync(normalized, null, ct);
            var entity = new DeliveryZone
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = normalized.BranchId,
                Name = normalized.NameEn,
                NameAr = normalized.NameAr,
                Code = normalized.Code,
                DeliveryFee = normalized.DeliveryFee,
                DeliveryCost = normalized.DeliveryCost,
                PaymentMode = normalized.PaymentMode,
                CenterLatitude = normalized.CenterLatitude,
                CenterLongitude = normalized.CenterLongitude,
                RadiusMeters = normalized.RadiusMeters,
                IsActive = normalized.IsActive,
                DisplayOrder = normalized.DisplayOrder,
                Notes = normalized.Notes
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchDeliveryZoneAsync(entity.Id, ct);
            var next = MapBranchDeliveryZone(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchDeliveryZoneCreated", ct);
            await InvalidateBranchDeliveryZoneCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task<BranchDeliveryZoneConfigurationDto> UpdateBranchDeliveryZoneAsync(Guid id, BranchDeliveryZoneConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchDeliveryZoneAsync(id, ct);
            var previous = MapBranchDeliveryZone(entity);
            var normalized = NormalizeBranchDeliveryZone(dto);
            await ValidateBranchDeliveryZoneAsync(normalized, id, ct);
            entity.BranchId = normalized.BranchId;
            entity.Name = normalized.NameEn;
            entity.NameAr = normalized.NameAr;
            entity.Code = normalized.Code;
            entity.DeliveryFee = normalized.DeliveryFee;
            entity.DeliveryCost = normalized.DeliveryCost;
            entity.PaymentMode = normalized.PaymentMode;
            entity.CenterLatitude = normalized.CenterLatitude;
            entity.CenterLongitude = normalized.CenterLongitude;
            entity.RadiusMeters = normalized.RadiusMeters;
            entity.IsActive = normalized.IsActive;
            entity.DisplayOrder = normalized.DisplayOrder;
            entity.Notes = normalized.Notes;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchDeliveryZoneAsync(id, ct);
            var next = MapBranchDeliveryZone(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchDeliveryZoneUpdated", ct);
            await InvalidateBranchDeliveryZoneCachesAsync(previous.BranchId, ct);
            if (previous.BranchId != next.BranchId)
                await InvalidateBranchDeliveryZoneCachesAsync(next.BranchId, ct);
            return next;
        }

        public async Task DeleteBranchDeliveryZoneAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchDeliveryZoneAsync(id, ct);
            var previous = MapBranchDeliveryZone(entity);
            entity.IsActive = false;
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchDeliveryZoneDeleted", ct);
            await InvalidateBranchDeliveryZoneCachesAsync(previous.BranchId, ct);
        }

        public async Task<BranchConfigurationPagedResultDto<BranchPrinterConfigurationDto>> GetBranchPrintersAsync(
            Guid? branchId,
            Guid? printerId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchPrintersAsync(branchId, printerId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchPrinter);
        }

        public async Task<BranchPrinterConfigurationDto> CreateBranchPrinterAsync(BranchPrinterConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            await ValidateBranchPrinterAsync(dto, null, ct);
            var entity = new BranchPrinter
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = dto.BranchId,
                PrinterId = dto.PrinterId,
                IsEnabled = dto.IsEnabled,
                DisplayOrder = dto.DisplayOrder,
                CreatedById = UserId,
                UpdatedById = UserId
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchPrinterAsync(entity.Id, ct);
            var next = MapBranchPrinter(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchPrinterCreated", ct);
            return next;
        }

        public async Task<BranchPrinterConfigurationDto> UpdateBranchPrinterAsync(Guid id, BranchPrinterConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchPrinterAsync(id, ct);
            var previous = MapBranchPrinter(entity);
            await ValidateBranchPrinterAsync(dto, id, ct);
            entity.BranchId = dto.BranchId;
            entity.PrinterId = dto.PrinterId;
            entity.IsEnabled = dto.IsEnabled;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.UpdatedById = UserId;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchPrinterAsync(id, ct);
            var next = MapBranchPrinter(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchPrinterUpdated", ct);
            return next;
        }

        public async Task DeleteBranchPrinterAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchPrinterAsync(id, ct);
            var previous = MapBranchPrinter(entity);
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchPrinterDeleted", ct);
        }

        public async Task<BranchConfigurationPagedResultDto<BranchOfferConfigurationDto>> GetBranchOffersAsync(
            Guid? branchId,
            int? offerId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchOffersAsync(branchId, offerId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchOffer);
        }

        public async Task<BranchOfferConfigurationDto> CreateBranchOfferAsync(BranchOfferConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            await ValidateBranchOfferAsync(dto, null, ct);
            var entity = new BranchOffer
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = dto.BranchId,
                OfferId = dto.OfferId,
                IsEnabled = dto.IsEnabled,
                CreatedById = UserId,
                UpdatedById = UserId
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchOfferAsync(entity.Id, ct);
            var next = MapBranchOffer(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchOfferCreated", ct);
            return next;
        }

        public async Task<BranchOfferConfigurationDto> UpdateBranchOfferAsync(Guid id, BranchOfferConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchOfferAsync(id, ct);
            var previous = MapBranchOffer(entity);
            await ValidateBranchOfferAsync(dto, id, ct);
            entity.BranchId = dto.BranchId;
            entity.OfferId = dto.OfferId;
            entity.IsEnabled = dto.IsEnabled;
            entity.UpdatedById = UserId;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchOfferAsync(id, ct);
            var next = MapBranchOffer(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchOfferUpdated", ct);
            return next;
        }

        public async Task DeleteBranchOfferAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchOfferAsync(id, ct);
            var previous = MapBranchOffer(entity);
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchOfferDeleted", ct);
        }

        public async Task<BranchConfigurationPagedResultDto<BranchSettingsConfigurationDto>> GetBranchSettingsAsync(
            Guid? branchId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            (page, pageSize) = NormalizePage(page, pageSize);
            var result = await _repository.GetBranchSettingsAsync(branchId, search, page, pageSize, ct);
            return ToPage(result, page, pageSize, MapBranchSettings);
        }

        public async Task<BranchSettingsConfigurationDto> CreateBranchSettingsAsync(BranchSettingsConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var normalized = NormalizeBranchSettings(dto);
            await ValidateBranchSettingsAsync(normalized, null, ct);
            var entity = new BranchSettings
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BranchId = normalized.BranchId,
                SettingKey = normalized.SettingKey,
                SettingValue = normalized.SettingValue,
                ValueType = normalized.ValueType,
                Description = normalized.Description,
                IsEnabled = normalized.IsEnabled,
                CreatedById = UserId,
                UpdatedById = UserId
            };

            await _repository.AddAsync(entity, ct);
            var created = await RequireBranchSettingsAsync(entity.Id, ct);
            var next = MapBranchSettings(created);
            await AuditAsync(null, next, next.Id, next.BranchCode, "BranchSettingsCreated", ct);
            return next;
        }

        public async Task<BranchSettingsConfigurationDto> UpdateBranchSettingsAsync(Guid id, BranchSettingsConfigurationUpsertDto dto, CancellationToken ct = default)
        {
            var entity = await RequireBranchSettingsAsync(id, ct);
            var previous = MapBranchSettings(entity);
            var normalized = NormalizeBranchSettings(dto);
            await ValidateBranchSettingsAsync(normalized, id, ct);
            entity.BranchId = normalized.BranchId;
            entity.SettingKey = normalized.SettingKey;
            entity.SettingValue = normalized.SettingValue;
            entity.ValueType = normalized.ValueType;
            entity.Description = normalized.Description;
            entity.IsEnabled = normalized.IsEnabled;
            entity.UpdatedById = UserId;
            await _repository.SaveChangesAsync(ct);
            _repository.Detach(entity);

            var updated = await RequireBranchSettingsAsync(id, ct);
            var next = MapBranchSettings(updated);
            await AuditAsync(previous, next, next.Id, next.BranchCode, "BranchSettingsUpdated", ct);
            return next;
        }

        public async Task DeleteBranchSettingsAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await RequireBranchSettingsAsync(id, ct);
            var previous = MapBranchSettings(entity);
            await _repository.DeleteAsync(entity, ct);
            await AuditAsync(previous, null, previous.Id, previous.BranchCode, "BranchSettingsDeleted", ct);
        }

        private async Task ValidateBranchProductAsync(BranchProductConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (dto.ProductId == Guid.Empty || !await _repository.ProductExistsAsync(dto.ProductId, ct))
                throw FieldValidation("productId", "Product is required.");
            if (await _repository.BranchProductExistsAsync(dto.BranchId, dto.ProductId, excludeId, ct))
                throw FieldValidation("productId", "This product is already configured for the selected branch.");
        }

        private async Task ValidateBranchCategoryAsync(BranchCategoryConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (dto.CategoryId == Guid.Empty || !await _repository.CategoryExistsAsync(dto.CategoryId, ct))
                throw FieldValidation("categoryId", "Category is required.");
            if (await _repository.BranchCategoryExistsAsync(dto.BranchId, dto.CategoryId, excludeId, ct))
                throw FieldValidation("categoryId", "This category is already configured for the selected branch.");
        }

        private async Task ValidateBranchSubcategoryAsync(BranchSubcategoryConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (dto.SubcategoryId == Guid.Empty || !await _repository.SubcategoryExistsAsync(dto.SubcategoryId, ct))
                throw FieldValidation("subcategoryId", "Subcategory is required.");
            if (await _repository.BranchSubcategoryExistsAsync(dto.BranchId, dto.SubcategoryId, excludeId, ct))
                throw FieldValidation("subcategoryId", "This subcategory is already configured for the selected branch.");
        }

        private async Task ValidateBranchModifierAsync(BranchModifierConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (dto.ModifierId == Guid.Empty || !await _repository.ModifierExistsAsync(dto.ModifierId, ct))
                throw FieldValidation("modifierId", "Modifier is required.");
            if (await _repository.BranchModifierExistsAsync(dto.BranchId, dto.ModifierId, excludeId, ct))
                throw FieldValidation("modifierId", "This modifier is already configured for the selected branch.");
        }

        private async Task ValidateBranchModifierGroupAsync(BranchModifierGroupConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (dto.ModifierGroupId == Guid.Empty || !await _repository.ModifierGroupExistsAsync(dto.ModifierGroupId, ct))
                throw FieldValidation("modifierGroupId", "Modifier group is required.");
            if (await _repository.BranchModifierGroupExistsAsync(dto.BranchId, dto.ModifierGroupId, excludeId, ct))
                throw FieldValidation("modifierGroupId", "This modifier group is already configured for the selected branch.");
        }

        private async Task ValidateBranchProductOptionAsync(BranchProductOptionConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (dto.ProductOptionId == Guid.Empty || !await _repository.ProductOptionExistsAsync(dto.ProductOptionId, ct))
                throw FieldValidation("productOptionId", "Product option is required.");
            if (await _repository.BranchProductOptionExistsAsync(dto.BranchId, dto.ProductOptionId, excludeId, ct))
                throw FieldValidation("productOptionId", "This product option is already configured for the selected branch.");
        }

        private async Task ValidateBranchPaymentMethodAsync(BranchPaymentMethodConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (dto.PaymentMethodId == Guid.Empty || !await _repository.PaymentMethodExistsAsync(dto.PaymentMethodId, ct))
                throw FieldValidation("paymentMethodId", "Payment method is required.");
            if (await _repository.BranchPaymentMethodExistsAsync(dto.BranchId, dto.PaymentMethodId, excludeId, ct))
                throw FieldValidation("paymentMethodId", "This payment method is already configured for the selected branch.");
        }

        private async Task ValidateBranchDeliveryPartnerAsync(BranchDeliveryPartnerConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (dto.DeliveryPartnerId == Guid.Empty || !await _repository.DeliveryPartnerExistsAsync(dto.DeliveryPartnerId, ct))
                throw FieldValidation("deliveryPartnerId", "Delivery partner is required.");
            if (await _repository.BranchDeliveryPartnerExistsAsync(dto.BranchId, dto.DeliveryPartnerId, excludeId, ct))
                throw FieldValidation("deliveryPartnerId", "This delivery partner is already configured for the selected branch.");
        }

        private async Task ValidateBranchDeliveryZoneAsync(BranchDeliveryZoneConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (string.IsNullOrWhiteSpace(dto.NameEn))
                throw FieldValidation("nameEn", "Delivery zone name is required.");
            if (string.IsNullOrWhiteSpace(dto.Code))
                throw FieldValidation("code", "Delivery zone code is required.");
            if (dto.DeliveryFee < 0)
                throw FieldValidation("deliveryFee", "Delivery fee cannot be negative.");
            if (dto.DeliveryCost < 0)
                throw FieldValidation("deliveryCost", "Delivery cost cannot be negative.");
            if (dto.CenterLatitude.HasValue != dto.CenterLongitude.HasValue)
                throw FieldValidation("centerLatitude", "Latitude and longitude must be configured together.");
            if (dto.CenterLatitude is < -90m or > 90m || dto.CenterLongitude is < -180m or > 180m)
                throw FieldValidation("centerLatitude", "Delivery zone coordinates are invalid.");
            if (dto.RadiusMeters is <= 0)
                throw FieldValidation("radiusMeters", "Delivery zone radius must be greater than zero.");
            if (dto.RadiusMeters.HasValue && !dto.CenterLatitude.HasValue)
                throw FieldValidation("radiusMeters", "Delivery zone radius requires latitude and longitude.");
            if (await _repository.BranchDeliveryZoneCodeExistsAsync(dto.BranchId, dto.Code, excludeId, ct))
                throw FieldValidation("code", "This delivery zone code is already configured for the selected branch.");
        }

        private async Task ValidateBranchPrinterAsync(BranchPrinterConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (dto.PrinterId == Guid.Empty || !await _repository.PrinterExistsAsync(dto.PrinterId, ct))
                throw FieldValidation("printerId", "Printer is required.");
            if (await _repository.BranchPrinterExistsAsync(dto.BranchId, dto.PrinterId, excludeId, ct))
                throw FieldValidation("printerId", "This printer is already configured for the selected branch.");
        }

        private async Task ValidateBranchOfferAsync(BranchOfferConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (dto.OfferId <= 0 || !await _repository.OfferExistsAsync(dto.OfferId, ct))
                throw FieldValidation("offerId", "Offer is required.");
            if (await _repository.BranchOfferExistsAsync(dto.BranchId, dto.OfferId, excludeId, ct))
                throw FieldValidation("offerId", "This offer is already configured for the selected branch.");
        }

        private async Task ValidateBranchSettingsAsync(BranchSettingsConfigurationUpsertDto dto, Guid? excludeId, CancellationToken ct)
        {
            await ValidateBranchAsync(dto.BranchId, ct);
            if (string.IsNullOrWhiteSpace(dto.SettingKey))
                throw FieldValidation("settingKey", "Setting key is required.");
            if (await _repository.BranchSettingsExistsAsync(dto.BranchId, dto.SettingKey, excludeId, ct))
                throw FieldValidation("settingKey", "This setting key is already configured for the selected branch.");
        }

        private async Task ValidateBranchAsync(Guid branchId, CancellationToken ct)
        {
            if (branchId == Guid.Empty || !await _repository.BranchExistsAsync(branchId, ct))
                throw FieldValidation("branchId", "Branch is required.");
        }

        private Task<BranchProduct> RequireBranchProductAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchProductTrackedAsync(id, ct), nameof(BranchProduct), id);

        private Task<BranchCategory> RequireBranchCategoryAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchCategoryTrackedAsync(id, ct), nameof(BranchCategory), id);

        private Task<BranchSubcategory> RequireBranchSubcategoryAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchSubcategoryTrackedAsync(id, ct), nameof(BranchSubcategory), id);

        private Task<BranchModifier> RequireBranchModifierAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchModifierTrackedAsync(id, ct), nameof(BranchModifier), id);

        private Task<BranchModifierGroup> RequireBranchModifierGroupAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchModifierGroupTrackedAsync(id, ct), nameof(BranchModifierGroup), id);

        private Task<BranchProductOption> RequireBranchProductOptionAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchProductOptionTrackedAsync(id, ct), nameof(BranchProductOption), id);

        private Task<BranchPaymentMethod> RequireBranchPaymentMethodAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchPaymentMethodTrackedAsync(id, ct), nameof(BranchPaymentMethod), id);

        private Task<BranchDeliveryPartner> RequireBranchDeliveryPartnerAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchDeliveryPartnerTrackedAsync(id, ct), nameof(BranchDeliveryPartner), id);

        private Task<DeliveryZone> RequireBranchDeliveryZoneAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchDeliveryZoneTrackedAsync(id, ct), nameof(DeliveryZone), id);

        private Task<BranchPrinter> RequireBranchPrinterAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchPrinterTrackedAsync(id, ct), nameof(BranchPrinter), id);

        private Task<BranchOffer> RequireBranchOfferAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchOfferTrackedAsync(id, ct), nameof(BranchOffer), id);

        private Task<BranchSettings> RequireBranchSettingsAsync(Guid id, CancellationToken ct)
            => RequireAsync(_repository.GetBranchSettingsTrackedAsync(id, ct), nameof(BranchSettings), id);

        private static async Task<TEntity> RequireAsync<TEntity>(Task<TEntity?> task, string entityName, Guid id)
            where TEntity : class
            => await task ?? throw new NotFoundException(entityName, id);

        private Task AuditAsync(object? previous, object? next, Guid targetId, string branchCode, string reason, CancellationToken ct)
            => _audit.LogAsync(
                TenantId,
                ConfigAuditEventType.BranchConfigurationChanged,
                previous,
                next,
                targetId: targetId,
                branchCode: branchCode,
                reason: reason,
                ct: ct);

        private Task InvalidateBranchProductCachesAsync(Guid branchId, CancellationToken ct)
            => _cache.RemoveByPatternAsync($"pos:products:cashier:*:{TenantId}:{branchId:N}", ct);

        private Task InvalidateBranchPaymentMethodCachesAsync(Guid branchId, CancellationToken ct)
            => _cache.RemoveAsync($"{CacheKeys.PaymentMethodsActive(TenantId)}:{branchId:N}", ct);

        private async Task InvalidateBranchDeliveryPartnerCachesAsync(Guid branchId, CancellationToken ct)
        {
            await _cache.RemoveAsync($"deliverypartners:active:{TenantId}:{branchId:N}", ct);
            await InvalidateBranchProductCachesAsync(branchId, ct);
        }

        private Task InvalidateBranchDeliveryZoneCachesAsync(Guid branchId, CancellationToken ct)
            => _cache.RemoveAsync($"deliveryzones:active:{TenantId}:{branchId}", ct);

        private async Task EnsureMainBranchConfigurationsAsync<TEntity>(
            IReadOnlyCollection<MainBranchConfigurationSeed> seeds,
            Func<IReadOnlyCollection<Guid>, CancellationToken, Task<List<(Guid BranchId, Guid EntityId)>>> getExistingKeys,
            Func<Guid, Guid, Guid, int, TEntity> createEntity,
            CancellationToken ct)
            where TEntity : BaseEntity
        {
            var resolvedSeeds = await ResolveMainBranchSeedsAsync(seeds, ct);
            if (resolvedSeeds.Count == 0)
                return;

            var entityIds = resolvedSeeds.Select(seed => seed.EntityId).Distinct().ToList();
            var existingKeys = (await getExistingKeys(entityIds, ct)).ToHashSet();
            var entities = resolvedSeeds
                .Where(seed => !existingKeys.Contains((seed.BranchId, seed.EntityId)))
                .Select(seed => createEntity(seed.TenantId, seed.BranchId, seed.EntityId, seed.DisplayOrder))
                .ToList();

            await _repository.AddRangeAsync(entities, ct);
        }

        private async Task<List<(Guid TenantId, Guid BranchId, Guid EntityId, int DisplayOrder)>> ResolveMainBranchSeedsAsync(
            IReadOnlyCollection<MainBranchConfigurationSeed> seeds,
            CancellationToken ct)
        {
            var normalizedSeeds = seeds
                .Where(seed => seed.TenantId != Guid.Empty && seed.EntityId != Guid.Empty)
                .Distinct()
                .ToList();
            if (normalizedSeeds.Count == 0)
                return new List<(Guid TenantId, Guid BranchId, Guid EntityId, int DisplayOrder)>();

            var tenantIds = normalizedSeeds.Select(seed => seed.TenantId).Distinct().ToList();
            var mainBranches = await _repository.GetMainBranchIdsByTenantAsync(tenantIds, ct);
            return normalizedSeeds
                .Where(seed => mainBranches.ContainsKey(seed.TenantId))
                .Select(seed => (
                    seed.TenantId,
                    mainBranches[seed.TenantId],
                    seed.EntityId,
                    seed.DisplayOrder))
                .ToList();
        }

        private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
            => (page < 1 ? 1 : page, pageSize is < 1 or > 200 ? 25 : pageSize);

        private static BranchSettingsConfigurationUpsertDto NormalizeBranchSettings(BranchSettingsConfigurationUpsertDto dto)
            => new()
            {
                BranchId = dto.BranchId,
                SettingKey = dto.SettingKey.Trim(),
                SettingValue = string.IsNullOrWhiteSpace(dto.SettingValue) ? null : dto.SettingValue.Trim(),
                ValueType = string.IsNullOrWhiteSpace(dto.ValueType) ? "String" : dto.ValueType.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                IsEnabled = dto.IsEnabled
            };

        private static BranchDeliveryZoneConfigurationUpsertDto NormalizeBranchDeliveryZone(BranchDeliveryZoneConfigurationUpsertDto dto)
            => new()
            {
                BranchId = dto.BranchId,
                NameEn = dto.NameEn.Trim(),
                NameAr = string.IsNullOrWhiteSpace(dto.NameAr) ? null : dto.NameAr.Trim(),
                Code = dto.Code.Trim(),
                DeliveryFee = dto.DeliveryFee,
                DeliveryCost = dto.DeliveryCost,
                PaymentMode = dto.PaymentMode,
                CenterLatitude = dto.CenterLatitude,
                CenterLongitude = dto.CenterLongitude,
                RadiusMeters = dto.RadiusMeters,
                IsActive = dto.IsActive,
                DisplayOrder = dto.DisplayOrder,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()
            };

        private static ValidationException FieldValidation(string field, string message)
            => new(new Dictionary<string, string[]> { [field] = new[] { message } });

        private static BranchConfigurationPagedResultDto<TDto> ToPage<TEntity, TDto>(
            (List<TEntity> Items, int TotalCount) result,
            int page,
            int pageSize,
            Func<TEntity, TDto> map)
            => new()
            {
                Items = result.Items.Select(map).ToList(),
                TotalCount = result.TotalCount,
                Page = page,
                PageSize = pageSize
            };

        private static BranchConfigurationOptionDto MapBranchOption(Branch branch)
            => new()
            {
                Id = branch.Id,
                Name = branch.Name,
                NameAr = branch.NameAr,
                Code = branch.Code,
                SecondaryLabel = branch.IsMainBranch ? "Main Branch" : null,
                IsActive = branch.IsActive
            };

        private static BranchConfigurationOptionDto MapProductOption(Product product)
            => new()
            {
                Id = product.Id,
                Name = product.Name,
                NameAr = product.NameAr,
                SecondaryLabel = product.Category?.Name,
                IsActive = product.IsActive
            };

        private static BranchConfigurationOptionDto MapCategoryOption(Category category)
            => new()
            {
                Id = category.Id,
                Name = category.Name,
                NameAr = category.NameAr,
                SecondaryLabel = category.DisplayMode.ToString(),
                IsActive = category.IsActive
            };

        private static BranchConfigurationOptionDto MapSubcategoryOption(Subcategory subcategory)
            => new()
            {
                Id = subcategory.Id,
                Name = subcategory.Name,
                NameAr = subcategory.NameAr,
                SecondaryLabel = subcategory.Category.Name,
                IsActive = subcategory.IsActive
            };

        private static BranchConfigurationOptionDto MapModifierOption(Modifier modifier)
            => new()
            {
                Id = modifier.Id,
                Name = modifier.Name,
                NameAr = modifier.NameAr,
                SecondaryLabel = modifier.ModifierGroup.Name,
                IsActive = modifier.IsActive
            };

        private static BranchConfigurationOptionDto MapModifierGroupOption(ModifierGroup group)
            => new()
            {
                Id = group.Id,
                Name = group.Name,
                NameAr = group.NameAr,
                SecondaryLabel = group.SelectionType.ToString(),
                IsActive = true
            };

        private static BranchConfigurationOptionDto MapProductOptionOption(ProductOption option)
            => new()
            {
                Id = option.Id,
                Name = option.Name,
                NameAr = option.NameAr,
                SecondaryLabel = option.Product.Name,
                IsActive = option.IsActive
            };

        private static BranchConfigurationOptionDto MapPaymentMethodOption(PaymentMethod method)
            => new()
            {
                Id = method.Id,
                Name = method.NameEn,
                NameAr = method.NameAr,
                Code = method.Code,
                IsActive = method.IsActive
            };

        private static BranchConfigurationOptionDto MapDeliveryPartnerOption(DeliveryPartner partner)
            => new()
            {
                Id = partner.Id,
                Name = partner.Name,
                NameAr = partner.NameAr,
                Code = partner.Code,
                IsActive = partner.Status == DeliveryPartnerStatus.Active
            };

        private static BranchConfigurationOptionDto MapPrinterOption(Printer printer)
            => new()
            {
                Id = printer.Id,
                Name = printer.Name,
                NameAr = printer.NameAr,
                SecondaryLabel = printer.Kitchen?.Name ?? "Receipt",
                IsActive = printer.IsActive
            };

        private static BranchConfigurationOfferOptionDto MapOfferOption(Offer offer)
            => new()
            {
                Id = offer.Id,
                Name = offer.Name,
                NameAr = offer.NameAr,
                IsActive = offer.IsActive
            };

        private static BranchOfferConfigurationDto MapBranchOffer(BranchOffer config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch.Name,
                BranchNameAr = config.Branch.NameAr,
                BranchCode = config.Branch.Code,
                OfferId = config.OfferId,
                OfferName = config.Offer.Name,
                OfferNameAr = config.Offer.NameAr,
                OfferIsActive = config.Offer.IsActive,
                IsEnabled = config.IsEnabled,
                CreatedById = config.CreatedById,
                UpdatedById = config.UpdatedById,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };

        private static BranchProductConfigurationDto MapBranchProduct(BranchProduct config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch.Name,
                BranchNameAr = config.Branch.NameAr,
                BranchCode = config.Branch.Code,
                ProductId = config.ProductId,
                ProductName = config.Product.Name,
                ProductNameAr = config.Product.NameAr,
                CategoryName = config.Product.Category?.Name,
                CategoryNameAr = config.Product.Category?.NameAr,
                ProductIsActive = config.Product.IsActive,
                IsAvailable = config.IsAvailable,
                IsVisible = config.IsVisible,
                DisplayOrder = config.DisplayOrder,
                CreatedById = config.CreatedById,
                UpdatedById = config.UpdatedById,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };

        private static BranchCategoryConfigurationDto MapBranchCategory(BranchCategory config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch.Name,
                BranchNameAr = config.Branch.NameAr,
                BranchCode = config.Branch.Code,
                CategoryId = config.CategoryId,
                CategoryName = config.Category.Name,
                CategoryNameAr = config.Category.NameAr,
                CategoryIsActive = config.Category.IsActive,
                IsVisible = config.IsVisible,
                DisplayOrder = config.DisplayOrder,
                CreatedById = config.CreatedById,
                UpdatedById = config.UpdatedById,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };

        private static BranchSubcategoryConfigurationDto MapBranchSubcategory(BranchSubcategory config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch.Name,
                BranchNameAr = config.Branch.NameAr,
                BranchCode = config.Branch.Code,
                SubcategoryId = config.SubcategoryId,
                SubcategoryName = config.Subcategory.Name,
                SubcategoryNameAr = config.Subcategory.NameAr,
                CategoryName = config.Subcategory.Category.Name,
                CategoryNameAr = config.Subcategory.Category.NameAr,
                SubcategoryIsActive = config.Subcategory.IsActive,
                IsVisible = config.IsVisible,
                DisplayOrder = config.DisplayOrder,
                CreatedById = config.CreatedById,
                UpdatedById = config.UpdatedById,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };

        private static BranchModifierConfigurationDto MapBranchModifier(BranchModifier config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch.Name,
                BranchNameAr = config.Branch.NameAr,
                BranchCode = config.Branch.Code,
                ModifierId = config.ModifierId,
                ModifierName = config.Modifier.Name,
                ModifierNameAr = config.Modifier.NameAr,
                ModifierGroupName = config.Modifier.ModifierGroup.Name,
                ModifierGroupNameAr = config.Modifier.ModifierGroup.NameAr,
                ModifierIsActive = config.Modifier.IsActive,
                IsAvailable = config.IsAvailable,
                CreatedById = config.CreatedById,
                UpdatedById = config.UpdatedById,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };

        private static BranchModifierGroupConfigurationDto MapBranchModifierGroup(BranchModifierGroup config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch.Name,
                BranchNameAr = config.Branch.NameAr,
                BranchCode = config.Branch.Code,
                ModifierGroupId = config.ModifierGroupId,
                ModifierGroupName = config.ModifierGroup.Name,
                ModifierGroupNameAr = config.ModifierGroup.NameAr,
                SelectionType = (int)config.ModifierGroup.SelectionType,
                IsRequired = config.ModifierGroup.IsRequired,
                IsAvailable = config.IsAvailable,
                IsVisible = config.IsVisible,
                DisplayOrder = config.DisplayOrder,
                CreatedById = config.CreatedById,
                UpdatedById = config.UpdatedById,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };

        private static BranchProductOptionConfigurationDto MapBranchProductOption(BranchProductOption config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch.Name,
                BranchNameAr = config.Branch.NameAr,
                BranchCode = config.Branch.Code,
                ProductOptionId = config.ProductOptionId,
                ProductOptionName = config.ProductOption.Name,
                ProductOptionNameAr = config.ProductOption.NameAr,
                ProductId = config.ProductOption.ProductId,
                ProductName = config.ProductOption.Product.Name,
                ProductNameAr = config.ProductOption.Product.NameAr,
                ProductOptionIsActive = config.ProductOption.IsActive,
                IsAvailable = config.IsAvailable,
                IsVisible = config.IsVisible,
                DisplayOrder = config.DisplayOrder,
                CreatedById = config.CreatedById,
                UpdatedById = config.UpdatedById,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };

        private static BranchPaymentMethodConfigurationDto MapBranchPaymentMethod(BranchPaymentMethod config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch.Name,
                BranchNameAr = config.Branch.NameAr,
                BranchCode = config.Branch.Code,
                PaymentMethodId = config.PaymentMethodId,
                PaymentMethodName = config.PaymentMethod.NameEn,
                PaymentMethodNameAr = config.PaymentMethod.NameAr,
                PaymentMethodCode = config.PaymentMethod.Code,
                PaymentMethodIsActive = config.PaymentMethod.IsActive,
                IsEnabled = config.IsEnabled,
                CreatedById = config.CreatedById,
                UpdatedById = config.UpdatedById,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };

        private static BranchDeliveryPartnerConfigurationDto MapBranchDeliveryPartner(BranchDeliveryPartner config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch.Name,
                BranchNameAr = config.Branch.NameAr,
                BranchCode = config.Branch.Code,
                DeliveryPartnerId = config.DeliveryPartnerId,
                DeliveryPartnerName = config.DeliveryPartner.Name,
                DeliveryPartnerNameAr = config.DeliveryPartner.NameAr,
                DeliveryPartnerCode = config.DeliveryPartner.Code,
                DeliveryPartnerIsActive = config.DeliveryPartner.Status == DeliveryPartnerStatus.Active,
                IsEnabled = config.IsEnabled,
                CreatedById = config.CreatedById,
                UpdatedById = config.UpdatedById,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };

        private static BranchDeliveryZoneConfigurationDto MapBranchDeliveryZone(DeliveryZone config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch?.Name ?? string.Empty,
                BranchNameAr = config.Branch?.NameAr,
                BranchCode = config.Branch?.Code ?? string.Empty,
                NameEn = config.Name,
                NameAr = config.NameAr,
                Code = config.Code,
                DeliveryFee = config.DeliveryFee,
                DeliveryCost = config.DeliveryCost,
                PaymentMode = config.PaymentMode,
                CenterLatitude = config.CenterLatitude,
                CenterLongitude = config.CenterLongitude,
                RadiusMeters = config.RadiusMeters,
                IsActive = config.IsActive,
                DisplayOrder = config.DisplayOrder,
                Notes = config.Notes,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt ?? config.CreatedAt
            };

        private static BranchPrinterConfigurationDto MapBranchPrinter(BranchPrinter config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch.Name,
                BranchNameAr = config.Branch.NameAr,
                BranchCode = config.Branch.Code,
                PrinterId = config.PrinterId,
                PrinterName = config.Printer.Name,
                PrinterNameAr = config.Printer.NameAr,
                PrinterIsActive = config.Printer.IsActive,
                PrinterIsReceiptPrinter = config.Printer.IsReceiptPrinter,
                PrinterKitchenName = config.Printer.Kitchen?.Name,
                PrinterKitchenNameAr = config.Printer.Kitchen?.NameAr,
                IsEnabled = config.IsEnabled,
                DisplayOrder = config.DisplayOrder,
                CreatedById = config.CreatedById,
                UpdatedById = config.UpdatedById,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };

        private static BranchSettingsConfigurationDto MapBranchSettings(BranchSettings config)
            => new()
            {
                Id = config.Id,
                TenantId = config.TenantId,
                BranchId = config.BranchId,
                BranchName = config.Branch.Name,
                BranchNameAr = config.Branch.NameAr,
                BranchCode = config.Branch.Code,
                SettingKey = config.SettingKey,
                SettingValue = config.SettingValue,
                ValueType = config.ValueType,
                Description = config.Description,
                IsEnabled = config.IsEnabled,
                CreatedById = config.CreatedById,
                UpdatedById = config.UpdatedById,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };
    }
}
