using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using CategoryEntity = RestaurantPos.Api.Models.Category;

namespace RestaurantPos.Api.Repositories
{
    public class BranchConfigurationRepository : IBranchConfigurationRepository
    {
        private readonly PosDbContext _context;

        public BranchConfigurationRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<List<Branch>> GetBranchOptionsAsync(CancellationToken ct = default)
            => _context.Branches.AsNoTracking()
                .OrderByDescending(b => b.IsMainBranch)
                .ThenBy(b => b.Name)
                .ToListAsync(ct);

        public Task<Dictionary<Guid, Guid>> GetMainBranchIdsByTenantAsync(
            IReadOnlyCollection<Guid> tenantIds,
            CancellationToken ct = default)
        {
            if (tenantIds.Count == 0)
                return Task.FromResult(new Dictionary<Guid, Guid>());

            return _context.Branches.AsNoTracking()
                .Where(b => tenantIds.Contains(b.TenantId) && b.IsMainBranch)
                .Select(b => new { b.TenantId, b.Id })
                .ToDictionaryAsync(b => b.TenantId, b => b.Id, ct);
        }

        public Task<List<Product>> GetProductOptionsAsync(CancellationToken ct = default)
            => _context.Products.AsNoTracking()
                .Include(p => p.Category)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);

        public Task<List<CategoryEntity>> GetCategoryOptionsAsync(CancellationToken ct = default)
            => _context.Categories.AsNoTracking()
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync(ct);

        public Task<List<Subcategory>> GetSubcategoryOptionsAsync(CancellationToken ct = default)
            => _context.Subcategories.AsNoTracking()
                .Include(s => s.Category)
                .OrderBy(s => s.Category.SortOrder)
                .ThenBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .ToListAsync(ct);

        public Task<List<Modifier>> GetModifierOptionsAsync(CancellationToken ct = default)
            => _context.Modifiers.AsNoTracking()
                .Include(m => m.ModifierGroup)
                .OrderBy(m => m.ModifierGroup.Name)
                .ThenBy(m => m.Name)
                .ToListAsync(ct);

        public Task<List<ModifierGroup>> GetModifierGroupOptionsAsync(CancellationToken ct = default)
            => _context.ModifierGroups.AsNoTracking()
                .OrderBy(g => g.Name)
                .ToListAsync(ct);

        public Task<List<ProductOption>> GetProductOptionOptionsAsync(CancellationToken ct = default)
            => _context.ProductOptions.AsNoTracking()
                .Include(o => o.Product)
                .OrderBy(o => o.Product.Name)
                .ThenBy(o => o.SortOrder)
                .ThenBy(o => o.Name)
                .ToListAsync(ct);

        public Task<List<PaymentMethod>> GetPaymentMethodOptionsAsync(CancellationToken ct = default)
            => _context.PaymentMethods.AsNoTracking()
                .OrderBy(m => m.DisplayOrder)
                .ThenBy(m => m.NameEn)
                .ToListAsync(ct);

        public Task<List<DeliveryPartner>> GetDeliveryPartnerOptionsAsync(CancellationToken ct = default)
            => _context.DeliveryPartners.AsNoTracking()
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Name)
                .ToListAsync(ct);

        public Task<List<Printer>> GetPrinterOptionsAsync(CancellationToken ct = default)
            => _context.Printers.AsNoTracking()
                .Include(p => p.Kitchen)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);

        public Task<List<Offer>> GetOfferOptionsAsync(CancellationToken ct = default)
            => _context.Offers.AsNoTracking()
                .OrderBy(o => o.Name)
                .ToListAsync(ct);

        public Task<bool> BranchExistsAsync(Guid branchId, CancellationToken ct = default)
            => _context.Branches.AsNoTracking().AnyAsync(b => b.Id == branchId, ct);

        public Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct = default)
            => _context.Products.AsNoTracking().AnyAsync(p => p.Id == productId, ct);

        public Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken ct = default)
            => _context.Categories.AsNoTracking().AnyAsync(c => c.Id == categoryId, ct);

        public Task<bool> SubcategoryExistsAsync(Guid subcategoryId, CancellationToken ct = default)
            => _context.Subcategories.AsNoTracking().AnyAsync(s => s.Id == subcategoryId, ct);

        public Task<bool> ModifierExistsAsync(Guid modifierId, CancellationToken ct = default)
            => _context.Modifiers.AsNoTracking().AnyAsync(m => m.Id == modifierId, ct);

        public Task<bool> ModifierGroupExistsAsync(Guid modifierGroupId, CancellationToken ct = default)
            => _context.ModifierGroups.AsNoTracking().AnyAsync(g => g.Id == modifierGroupId, ct);

        public Task<bool> ProductOptionExistsAsync(Guid productOptionId, CancellationToken ct = default)
            => _context.ProductOptions.AsNoTracking().AnyAsync(o => o.Id == productOptionId, ct);

        public Task<bool> PaymentMethodExistsAsync(Guid paymentMethodId, CancellationToken ct = default)
            => _context.PaymentMethods.AsNoTracking().AnyAsync(m => m.Id == paymentMethodId, ct);

        public Task<bool> DeliveryPartnerExistsAsync(Guid deliveryPartnerId, CancellationToken ct = default)
            => _context.DeliveryPartners.AsNoTracking().AnyAsync(p => p.Id == deliveryPartnerId, ct);

        public Task<bool> PrinterExistsAsync(Guid printerId, CancellationToken ct = default)
            => _context.Printers.AsNoTracking().AnyAsync(p => p.Id == printerId, ct);

        public Task<bool> OfferExistsAsync(int offerId, CancellationToken ct = default)
            => _context.Offers.AsNoTracking().AnyAsync(o => o.Id == offerId, ct);

        public async Task<(List<BranchProduct> Items, int TotalCount)> GetBranchProductsAsync(
            Guid? branchId,
            Guid? productId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.BranchProducts.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.Product)
                    .ThenInclude(p => p.Category)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            if (productId.HasValue) query = query.Where(c => c.ProductId == productId.Value);
            query = ApplyProductSearch(query, search);
            return await PageAsync(query.OrderBy(c => c.Branch.Name).ThenBy(c => c.DisplayOrder).ThenBy(c => c.Product.Name), page, pageSize, ct);
        }

        public Task<BranchProduct?> GetBranchProductTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.BranchProducts
                .Include(c => c.Branch)
                .Include(c => c.Product)
                    .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchProductExistsAsync(Guid branchId, Guid productId, Guid? excludeId, CancellationToken ct = default)
            => _context.BranchProducts.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.ProductId == productId && (!excludeId.HasValue || c.Id != excludeId), ct);

        public async Task<(List<BranchCategory> Items, int TotalCount)> GetBranchCategoriesAsync(
            Guid? branchId,
            Guid? categoryId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.BranchCategories.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.Category)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            if (categoryId.HasValue) query = query.Where(c => c.CategoryId == categoryId.Value);
            query = ApplyCategorySearch(query, search);
            return await PageAsync(query.OrderBy(c => c.Branch.Name).ThenBy(c => c.DisplayOrder).ThenBy(c => c.Category.Name), page, pageSize, ct);
        }

        public Task<BranchCategory?> GetBranchCategoryTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.BranchCategories
                .Include(c => c.Branch)
                .Include(c => c.Category)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchCategoryExistsAsync(Guid branchId, Guid categoryId, Guid? excludeId, CancellationToken ct = default)
            => _context.BranchCategories.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.CategoryId == categoryId && (!excludeId.HasValue || c.Id != excludeId), ct);

        public async Task<(List<BranchSubcategory> Items, int TotalCount)> GetBranchSubcategoriesAsync(
            Guid? branchId,
            Guid? subcategoryId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.BranchSubcategories.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.Subcategory)
                    .ThenInclude(s => s.Category)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            if (subcategoryId.HasValue) query = query.Where(c => c.SubcategoryId == subcategoryId.Value);
            query = ApplySubcategorySearch(query, search);
            return await PageAsync(
                query.OrderBy(c => c.Branch.Name).ThenBy(c => c.DisplayOrder).ThenBy(c => c.Subcategory.Name),
                page,
                pageSize,
                ct);
        }

        public Task<BranchSubcategory?> GetBranchSubcategoryTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.BranchSubcategories
                .Include(c => c.Branch)
                .Include(c => c.Subcategory)
                    .ThenInclude(s => s.Category)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchSubcategoryExistsAsync(Guid branchId, Guid subcategoryId, Guid? excludeId, CancellationToken ct = default)
            => _context.BranchSubcategories.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.SubcategoryId == subcategoryId && (!excludeId.HasValue || c.Id != excludeId), ct);

        public async Task<(List<BranchModifier> Items, int TotalCount)> GetBranchModifiersAsync(
            Guid? branchId,
            Guid? modifierId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.BranchModifiers.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.Modifier)
                    .ThenInclude(m => m.ModifierGroup)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            if (modifierId.HasValue) query = query.Where(c => c.ModifierId == modifierId.Value);
            query = ApplyModifierSearch(query, search);
            return await PageAsync(query.OrderBy(c => c.Branch.Name).ThenBy(c => c.Modifier.ModifierGroup.Name).ThenBy(c => c.Modifier.Name), page, pageSize, ct);
        }

        public Task<BranchModifier?> GetBranchModifierTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.BranchModifiers
                .Include(c => c.Branch)
                .Include(c => c.Modifier)
                    .ThenInclude(m => m.ModifierGroup)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchModifierExistsAsync(Guid branchId, Guid modifierId, Guid? excludeId, CancellationToken ct = default)
            => _context.BranchModifiers.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.ModifierId == modifierId && (!excludeId.HasValue || c.Id != excludeId), ct);

        public async Task<(List<BranchModifierGroup> Items, int TotalCount)> GetBranchModifierGroupsAsync(
            Guid? branchId,
            Guid? modifierGroupId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.BranchModifierGroups.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.ModifierGroup)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            if (modifierGroupId.HasValue) query = query.Where(c => c.ModifierGroupId == modifierGroupId.Value);
            query = ApplyModifierGroupSearch(query, search);
            return await PageAsync(
                query.OrderBy(c => c.Branch.Name).ThenBy(c => c.DisplayOrder).ThenBy(c => c.ModifierGroup.Name),
                page,
                pageSize,
                ct);
        }

        public Task<BranchModifierGroup?> GetBranchModifierGroupTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.BranchModifierGroups
                .Include(c => c.Branch)
                .Include(c => c.ModifierGroup)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchModifierGroupExistsAsync(Guid branchId, Guid modifierGroupId, Guid? excludeId, CancellationToken ct = default)
            => _context.BranchModifierGroups.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.ModifierGroupId == modifierGroupId && (!excludeId.HasValue || c.Id != excludeId), ct);

        public async Task<(List<BranchProductOption> Items, int TotalCount)> GetBranchProductOptionsAsync(
            Guid? branchId,
            Guid? productOptionId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.BranchProductOptions.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.ProductOption)
                    .ThenInclude(o => o.Product)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            if (productOptionId.HasValue) query = query.Where(c => c.ProductOptionId == productOptionId.Value);
            query = ApplyProductOptionSearch(query, search);
            return await PageAsync(
                query.OrderBy(c => c.Branch.Name).ThenBy(c => c.DisplayOrder).ThenBy(c => c.ProductOption.Name),
                page,
                pageSize,
                ct);
        }

        public Task<BranchProductOption?> GetBranchProductOptionTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.BranchProductOptions
                .Include(c => c.Branch)
                .Include(c => c.ProductOption)
                    .ThenInclude(o => o.Product)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchProductOptionExistsAsync(Guid branchId, Guid productOptionId, Guid? excludeId, CancellationToken ct = default)
            => _context.BranchProductOptions.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.ProductOptionId == productOptionId && (!excludeId.HasValue || c.Id != excludeId), ct);

        public async Task<(List<BranchPaymentMethod> Items, int TotalCount)> GetBranchPaymentMethodsAsync(
            Guid? branchId,
            Guid? paymentMethodId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.BranchPaymentMethods.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.PaymentMethod)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            if (paymentMethodId.HasValue) query = query.Where(c => c.PaymentMethodId == paymentMethodId.Value);
            query = ApplyPaymentMethodSearch(query, search);
            return await PageAsync(query.OrderBy(c => c.Branch.Name).ThenBy(c => c.PaymentMethod.DisplayOrder).ThenBy(c => c.PaymentMethod.NameEn), page, pageSize, ct);
        }

        public Task<BranchPaymentMethod?> GetBranchPaymentMethodTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.BranchPaymentMethods
                .Include(c => c.Branch)
                .Include(c => c.PaymentMethod)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchPaymentMethodExistsAsync(Guid branchId, Guid paymentMethodId, Guid? excludeId, CancellationToken ct = default)
            => _context.BranchPaymentMethods.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.PaymentMethodId == paymentMethodId && (!excludeId.HasValue || c.Id != excludeId), ct);

        public async Task<(List<BranchDeliveryPartner> Items, int TotalCount)> GetBranchDeliveryPartnersAsync(
            Guid? branchId,
            Guid? deliveryPartnerId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.BranchDeliveryPartners.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.DeliveryPartner)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            if (deliveryPartnerId.HasValue) query = query.Where(c => c.DeliveryPartnerId == deliveryPartnerId.Value);
            query = ApplyDeliveryPartnerSearch(query, search);
            return await PageAsync(query.OrderBy(c => c.Branch.Name).ThenBy(c => c.DeliveryPartner.SortOrder).ThenBy(c => c.DeliveryPartner.Name), page, pageSize, ct);
        }

        public Task<BranchDeliveryPartner?> GetBranchDeliveryPartnerTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.BranchDeliveryPartners
                .Include(c => c.Branch)
                .Include(c => c.DeliveryPartner)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchDeliveryPartnerExistsAsync(Guid branchId, Guid deliveryPartnerId, Guid? excludeId, CancellationToken ct = default)
            => _context.BranchDeliveryPartners.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.DeliveryPartnerId == deliveryPartnerId && (!excludeId.HasValue || c.Id != excludeId), ct);

        public async Task<(List<DeliveryZone> Items, int TotalCount)> GetBranchDeliveryZonesAsync(
            Guid? branchId,
            Guid? deliveryZoneId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.DeliveryZones.AsNoTracking()
                .Include(c => c.Branch)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            if (deliveryZoneId.HasValue) query = query.Where(c => c.Id == deliveryZoneId.Value);
            query = ApplyDeliveryZoneSearch(query, search);
            return await PageAsync(query.OrderBy(c => c.Branch!.Name).ThenBy(c => c.DisplayOrder).ThenBy(c => c.Name), page, pageSize, ct);
        }

        public Task<DeliveryZone?> GetBranchDeliveryZoneTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.DeliveryZones
                .Include(c => c.Branch)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchDeliveryZoneCodeExistsAsync(Guid branchId, string code, Guid? excludeId, CancellationToken ct = default)
        {
            var normalized = code.Trim().ToLower();
            return _context.DeliveryZones.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.Code.ToLower() == normalized && (!excludeId.HasValue || c.Id != excludeId), ct);
        }

        public async Task<(List<BranchPrinter> Items, int TotalCount)> GetBranchPrintersAsync(
            Guid? branchId,
            Guid? printerId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.BranchPrinters.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.Printer)
                    .ThenInclude(p => p.Kitchen)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            if (printerId.HasValue) query = query.Where(c => c.PrinterId == printerId.Value);
            query = ApplyPrinterSearch(query, search);
            return await PageAsync(query.OrderBy(c => c.Branch.Name).ThenBy(c => c.DisplayOrder).ThenBy(c => c.Printer.Name), page, pageSize, ct);
        }

        public Task<BranchPrinter?> GetBranchPrinterTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.BranchPrinters
                .Include(c => c.Branch)
                .Include(c => c.Printer)
                    .ThenInclude(p => p.Kitchen)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchPrinterExistsAsync(Guid branchId, Guid printerId, Guid? excludeId, CancellationToken ct = default)
            => _context.BranchPrinters.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.PrinterId == printerId && (!excludeId.HasValue || c.Id != excludeId), ct);

        public async Task<(List<BranchOffer> Items, int TotalCount)> GetBranchOffersAsync(
            Guid? branchId,
            int? offerId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.BranchOffers.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.Offer)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            if (offerId.HasValue) query = query.Where(c => c.OfferId == offerId.Value);
            query = ApplyOfferSearch(query, search);
            return await PageAsync(query.OrderBy(c => c.Branch.Name).ThenBy(c => c.Offer.Name), page, pageSize, ct);
        }

        public Task<BranchOffer?> GetBranchOfferTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.BranchOffers
                .Include(c => c.Branch)
                .Include(c => c.Offer)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchOfferExistsAsync(Guid branchId, int offerId, Guid? excludeId, CancellationToken ct = default)
            => _context.BranchOffers.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.OfferId == offerId && (!excludeId.HasValue || c.Id != excludeId), ct);

        public async Task<(List<BranchSettings> Items, int TotalCount)> GetBranchSettingsAsync(
            Guid? branchId,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.BranchSettings.AsNoTracking()
                .Include(c => c.Branch)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(c => c.BranchId == branchId.Value);
            query = ApplySettingsSearch(query, search);
            return await PageAsync(query.OrderBy(c => c.Branch.Name).ThenBy(c => c.SettingKey), page, pageSize, ct);
        }

        public Task<BranchSettings?> GetBranchSettingsTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.BranchSettings
                .Include(c => c.Branch)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> BranchSettingsExistsAsync(Guid branchId, string settingKey, Guid? excludeId, CancellationToken ct = default)
            => _context.BranchSettings.AsNoTracking()
                .AnyAsync(c => c.BranchId == branchId && c.SettingKey == settingKey && (!excludeId.HasValue || c.Id != excludeId), ct);

        public Task<List<Guid>> GetAvailableProductIdsAsync(Guid branchId, CancellationToken ct = default)
            => _context.BranchProducts.AsNoTracking()
                .Where(c => c.BranchId == branchId && c.IsAvailable && c.IsVisible)
                .Select(c => c.ProductId)
                .ToListAsync(ct);

        public Task<List<Guid>> GetVisibleCategoryIdsAsync(Guid branchId, CancellationToken ct = default)
            => _context.BranchCategories.AsNoTracking()
                .Where(c => c.BranchId == branchId && c.IsVisible)
                .Select(c => c.CategoryId)
                .ToListAsync(ct);

        public Task<List<Guid>> GetVisibleSubcategoryIdsAsync(Guid branchId, CancellationToken ct = default)
            => _context.BranchSubcategories.AsNoTracking()
                .Where(c => c.BranchId == branchId && c.IsVisible)
                .Select(c => c.SubcategoryId)
                .ToListAsync(ct);

        public Task<List<Guid>> GetAvailableModifierIdsAsync(Guid branchId, CancellationToken ct = default)
            => _context.BranchModifiers.AsNoTracking()
                .Where(c => c.BranchId == branchId && c.IsAvailable)
                .Select(c => c.ModifierId)
                .ToListAsync(ct);

        public Task<List<Guid>> GetAvailableModifierGroupIdsAsync(Guid branchId, CancellationToken ct = default)
            => _context.BranchModifierGroups.AsNoTracking()
                .Where(c => c.BranchId == branchId && c.IsAvailable && c.IsVisible)
                .Select(c => c.ModifierGroupId)
                .ToListAsync(ct);

        public Task<List<Guid>> GetAvailableProductOptionIdsAsync(Guid branchId, CancellationToken ct = default)
            => _context.BranchProductOptions.AsNoTracking()
                .Where(c => c.BranchId == branchId && c.IsAvailable && c.IsVisible)
                .Select(c => c.ProductOptionId)
                .ToListAsync(ct);

        public Task<List<Guid>> GetEnabledPaymentMethodIdsAsync(Guid branchId, CancellationToken ct = default)
            => _context.BranchPaymentMethods.AsNoTracking()
                .Where(c => c.BranchId == branchId && c.IsEnabled)
                .Select(c => c.PaymentMethodId)
                .ToListAsync(ct);

        public Task<List<Guid>> GetEnabledDeliveryPartnerIdsAsync(Guid branchId, CancellationToken ct = default)
            => _context.BranchDeliveryPartners.AsNoTracking()
                .Where(c => c.BranchId == branchId && c.IsEnabled)
                .Select(c => c.DeliveryPartnerId)
                .ToListAsync(ct);

        public Task<List<Guid>> GetEnabledPrinterIdsAsync(Guid branchId, CancellationToken ct = default)
            => _context.BranchPrinters.AsNoTracking()
                .Where(c => c.BranchId == branchId && c.IsEnabled)
                .Select(c => c.PrinterId)
                .ToListAsync(ct);

        public Task<List<int>> GetEnabledOfferIdsAsync(Guid branchId, CancellationToken ct = default)
            => _context.BranchOffers.AsNoTracking()
                .Where(c => c.BranchId == branchId && c.IsEnabled)
                .Select(c => c.OfferId)
                .ToListAsync(ct);

        public async Task<List<(Guid BranchId, Guid EntityId)>> GetBranchProductKeysAsync(
            IReadOnlyCollection<Guid> productIds,
            CancellationToken ct = default)
        {
            var rows = await _context.BranchProducts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => productIds.Contains(c.ProductId))
                .Select(c => new { c.BranchId, EntityId = c.ProductId })
                .ToListAsync(ct);
            return rows.Select(c => (c.BranchId, c.EntityId)).ToList();
        }

        public async Task<List<(Guid BranchId, Guid EntityId)>> GetBranchCategoryKeysAsync(
            IReadOnlyCollection<Guid> categoryIds,
            CancellationToken ct = default)
        {
            var rows = await _context.BranchCategories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => categoryIds.Contains(c.CategoryId))
                .Select(c => new { c.BranchId, EntityId = c.CategoryId })
                .ToListAsync(ct);
            return rows.Select(c => (c.BranchId, c.EntityId)).ToList();
        }

        public async Task<List<(Guid BranchId, Guid EntityId)>> GetBranchSubcategoryKeysAsync(
            IReadOnlyCollection<Guid> subcategoryIds,
            CancellationToken ct = default)
        {
            var rows = await _context.BranchSubcategories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => subcategoryIds.Contains(c.SubcategoryId))
                .Select(c => new { c.BranchId, EntityId = c.SubcategoryId })
                .ToListAsync(ct);
            return rows.Select(c => (c.BranchId, c.EntityId)).ToList();
        }

        public async Task<List<(Guid BranchId, Guid EntityId)>> GetBranchModifierKeysAsync(
            IReadOnlyCollection<Guid> modifierIds,
            CancellationToken ct = default)
        {
            var rows = await _context.BranchModifiers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => modifierIds.Contains(c.ModifierId))
                .Select(c => new { c.BranchId, EntityId = c.ModifierId })
                .ToListAsync(ct);
            return rows.Select(c => (c.BranchId, c.EntityId)).ToList();
        }

        public async Task<List<(Guid BranchId, Guid EntityId)>> GetBranchModifierGroupKeysAsync(
            IReadOnlyCollection<Guid> modifierGroupIds,
            CancellationToken ct = default)
        {
            var rows = await _context.BranchModifierGroups
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => modifierGroupIds.Contains(c.ModifierGroupId))
                .Select(c => new { c.BranchId, EntityId = c.ModifierGroupId })
                .ToListAsync(ct);
            return rows.Select(c => (c.BranchId, c.EntityId)).ToList();
        }

        public async Task<List<(Guid BranchId, Guid EntityId)>> GetBranchProductOptionKeysAsync(
            IReadOnlyCollection<Guid> productOptionIds,
            CancellationToken ct = default)
        {
            var rows = await _context.BranchProductOptions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => productOptionIds.Contains(c.ProductOptionId))
                .Select(c => new { c.BranchId, EntityId = c.ProductOptionId })
                .ToListAsync(ct);
            return rows.Select(c => (c.BranchId, c.EntityId)).ToList();
        }

        public async Task<List<(Guid BranchId, Guid EntityId)>> GetBranchPaymentMethodKeysAsync(
            IReadOnlyCollection<Guid> paymentMethodIds,
            CancellationToken ct = default)
        {
            var rows = await _context.BranchPaymentMethods
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => paymentMethodIds.Contains(c.PaymentMethodId))
                .Select(c => new { c.BranchId, EntityId = c.PaymentMethodId })
                .ToListAsync(ct);
            return rows.Select(c => (c.BranchId, c.EntityId)).ToList();
        }

        public async Task<List<(Guid BranchId, Guid EntityId)>> GetBranchDeliveryPartnerKeysAsync(
            IReadOnlyCollection<Guid> deliveryPartnerIds,
            CancellationToken ct = default)
        {
            var rows = await _context.BranchDeliveryPartners
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => deliveryPartnerIds.Contains(c.DeliveryPartnerId))
                .Select(c => new { c.BranchId, EntityId = c.DeliveryPartnerId })
                .ToListAsync(ct);
            return rows.Select(c => (c.BranchId, c.EntityId)).ToList();
        }

        public async Task<List<(Guid BranchId, Guid EntityId)>> GetBranchPrinterKeysAsync(
            IReadOnlyCollection<Guid> printerIds,
            CancellationToken ct = default)
        {
            var rows = await _context.BranchPrinters
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => printerIds.Contains(c.PrinterId))
                .Select(c => new { c.BranchId, EntityId = c.PrinterId })
                .ToListAsync(ct);
            return rows.Select(c => (c.BranchId, c.EntityId)).ToList();
        }

        public async Task<List<(Guid BranchId, int OfferId)>> GetBranchOfferKeysAsync(
            IReadOnlyCollection<int> offerIds,
            CancellationToken ct = default)
        {
            var rows = await _context.BranchOffers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => offerIds.Contains(c.OfferId))
                .Select(c => new { c.BranchId, c.OfferId })
                .ToListAsync(ct);
            return rows.Select(c => (c.BranchId, c.OfferId)).ToList();
        }

        public async Task AddAsync<TEntity>(TEntity entity, CancellationToken ct = default) where TEntity : BaseEntity
        {
            _context.Set<TEntity>().Add(entity);
            await _context.SaveChangesAsync(ct);
        }

        public async Task AddRangeAsync<TEntity>(
            IReadOnlyCollection<TEntity> entities,
            CancellationToken ct = default)
            where TEntity : BaseEntity
        {
            if (entities.Count == 0)
                return;

            _context.Set<TEntity>().AddRange(entities);
            await _context.SaveChangesAsync(ct);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);

        public void Detach<TEntity>(TEntity entity) where TEntity : BaseEntity
            => _context.Entry(entity).State = EntityState.Detached;

        public async Task DeleteAsync<TEntity>(TEntity entity, CancellationToken ct = default) where TEntity : BaseEntity
        {
            entity.DeletedAt = DateTime.UtcNow;
            _context.Set<TEntity>().Update(entity);
            await _context.SaveChangesAsync(ct);
        }

        public async Task RemoveBranchProductsByProductIdAsync(Guid productId, CancellationToken ct = default)
        {
            var rows = await _context.BranchProducts
                .IgnoreQueryFilters()
                .Where(c => c.ProductId == productId)
                .ToListAsync(ct);
            await RemoveRowsAsync(rows, ct);
        }

        public async Task RemoveBranchProductOptionsByProductIdAsync(Guid productId, CancellationToken ct = default)
        {
            var rows = await _context.BranchProductOptions
                .IgnoreQueryFilters()
                .Where(c => c.ProductOption.ProductId == productId)
                .ToListAsync(ct);
            await RemoveRowsAsync(rows, ct);
        }

        public async Task RemoveBranchProductOptionsByProductOptionIdAsync(
            Guid productOptionId,
            CancellationToken ct = default)
        {
            var rows = await _context.BranchProductOptions
                .IgnoreQueryFilters()
                .Where(c => c.ProductOptionId == productOptionId)
                .ToListAsync(ct);
            await RemoveRowsAsync(rows, ct);
        }

        public async Task RemoveBranchCategoriesByCategoryIdAsync(Guid categoryId, CancellationToken ct = default)
        {
            var rows = await _context.BranchCategories
                .IgnoreQueryFilters()
                .Where(c => c.CategoryId == categoryId)
                .ToListAsync(ct);
            await RemoveRowsAsync(rows, ct);
        }

        public async Task RemoveBranchSubcategoriesBySubcategoryIdAsync(Guid subcategoryId, CancellationToken ct = default)
        {
            var rows = await _context.BranchSubcategories
                .IgnoreQueryFilters()
                .Where(c => c.SubcategoryId == subcategoryId)
                .ToListAsync(ct);
            await RemoveRowsAsync(rows, ct);
        }

        public async Task RemoveBranchModifiersByModifierIdsAsync(
            IReadOnlyCollection<Guid> modifierIds,
            CancellationToken ct = default)
        {
            if (modifierIds.Count == 0)
                return;

            var rows = await _context.BranchModifiers
                .IgnoreQueryFilters()
                .Where(c => modifierIds.Contains(c.ModifierId))
                .ToListAsync(ct);
            await RemoveRowsAsync(rows, ct);
        }

        public async Task RemoveBranchModifierGroupsByModifierGroupIdsAsync(
            IReadOnlyCollection<Guid> modifierGroupIds,
            CancellationToken ct = default)
        {
            if (modifierGroupIds.Count == 0)
                return;

            var rows = await _context.BranchModifierGroups
                .IgnoreQueryFilters()
                .Where(c => modifierGroupIds.Contains(c.ModifierGroupId))
                .ToListAsync(ct);
            await RemoveRowsAsync(rows, ct);
        }

        public async Task RemoveBranchOffersByOfferIdAsync(int offerId, CancellationToken ct = default)
        {
            var rows = await _context.BranchOffers
                .IgnoreQueryFilters()
                .Where(c => c.OfferId == offerId)
                .ToListAsync(ct);
            await RemoveRowsAsync(rows, ct);
        }

        private async Task RemoveRowsAsync<TEntity>(List<TEntity> rows, CancellationToken ct)
            where TEntity : BaseEntity
        {
            if (rows.Count == 0)
                return;

            _context.Set<TEntity>().RemoveRange(rows);
            await _context.SaveChangesAsync(ct);
        }

        private static async Task<(List<TEntity> Items, int TotalCount)> PageAsync<TEntity>(
            IQueryable<TEntity> query,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            var totalCount = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return (items, totalCount);
        }

        private static IQueryable<BranchProduct> ApplyProductSearch(IQueryable<BranchProduct> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch.Code, $"%{term}%") ||
                EF.Functions.ILike(c.Product.Name, $"%{term}%") ||
                (c.Product.NameAr != null && EF.Functions.ILike(c.Product.NameAr, $"%{term}%")));
        }

        private static IQueryable<BranchCategory> ApplyCategorySearch(IQueryable<BranchCategory> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch.Code, $"%{term}%") ||
                EF.Functions.ILike(c.Category.Name, $"%{term}%") ||
                (c.Category.NameAr != null && EF.Functions.ILike(c.Category.NameAr, $"%{term}%")));
        }

        private static IQueryable<BranchSubcategory> ApplySubcategorySearch(IQueryable<BranchSubcategory> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch.Code, $"%{term}%") ||
                EF.Functions.ILike(c.Subcategory.Name, $"%{term}%") ||
                (c.Subcategory.NameAr != null && EF.Functions.ILike(c.Subcategory.NameAr, $"%{term}%")) ||
                EF.Functions.ILike(c.Subcategory.Category.Name, $"%{term}%"));
        }

        private static IQueryable<BranchModifier> ApplyModifierSearch(IQueryable<BranchModifier> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch.Code, $"%{term}%") ||
                EF.Functions.ILike(c.Modifier.Name, $"%{term}%") ||
                (c.Modifier.NameAr != null && EF.Functions.ILike(c.Modifier.NameAr, $"%{term}%")) ||
                EF.Functions.ILike(c.Modifier.ModifierGroup.Name, $"%{term}%"));
        }

        private static IQueryable<BranchModifierGroup> ApplyModifierGroupSearch(IQueryable<BranchModifierGroup> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch.Code, $"%{term}%") ||
                EF.Functions.ILike(c.ModifierGroup.Name, $"%{term}%") ||
                (c.ModifierGroup.NameAr != null && EF.Functions.ILike(c.ModifierGroup.NameAr, $"%{term}%")));
        }

        private static IQueryable<BranchProductOption> ApplyProductOptionSearch(IQueryable<BranchProductOption> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch.Code, $"%{term}%") ||
                EF.Functions.ILike(c.ProductOption.Name, $"%{term}%") ||
                (c.ProductOption.NameAr != null && EF.Functions.ILike(c.ProductOption.NameAr, $"%{term}%")) ||
                EF.Functions.ILike(c.ProductOption.Product.Name, $"%{term}%"));
        }

        private static IQueryable<BranchPaymentMethod> ApplyPaymentMethodSearch(IQueryable<BranchPaymentMethod> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch.Code, $"%{term}%") ||
                EF.Functions.ILike(c.PaymentMethod.NameEn, $"%{term}%") ||
                EF.Functions.ILike(c.PaymentMethod.Code, $"%{term}%") ||
                (c.PaymentMethod.NameAr != null && EF.Functions.ILike(c.PaymentMethod.NameAr, $"%{term}%")));
        }

        private static IQueryable<BranchDeliveryPartner> ApplyDeliveryPartnerSearch(IQueryable<BranchDeliveryPartner> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch.Code, $"%{term}%") ||
                EF.Functions.ILike(c.DeliveryPartner.Name, $"%{term}%") ||
                EF.Functions.ILike(c.DeliveryPartner.Code, $"%{term}%") ||
                (c.DeliveryPartner.NameAr != null && EF.Functions.ILike(c.DeliveryPartner.NameAr, $"%{term}%")));
        }

        private static IQueryable<DeliveryZone> ApplyDeliveryZoneSearch(IQueryable<DeliveryZone> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch!.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch!.Code, $"%{term}%") ||
                EF.Functions.ILike(c.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Code, $"%{term}%") ||
                (c.NameAr != null && EF.Functions.ILike(c.NameAr, $"%{term}%")));
        }

        private static IQueryable<BranchPrinter> ApplyPrinterSearch(IQueryable<BranchPrinter> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch.Code, $"%{term}%") ||
                EF.Functions.ILike(c.Printer.Name, $"%{term}%") ||
                (c.Printer.NameAr != null && EF.Functions.ILike(c.Printer.NameAr, $"%{term}%")));
        }

        private static IQueryable<BranchOffer> ApplyOfferSearch(IQueryable<BranchOffer> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch.Code, $"%{term}%") ||
                EF.Functions.ILike(c.Offer.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Offer.NameAr, $"%{term}%"));
        }

        private static IQueryable<BranchSettings> ApplySettingsSearch(IQueryable<BranchSettings> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var term = search.Trim();
            return query.Where(c =>
                EF.Functions.ILike(c.Branch.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Branch.Code, $"%{term}%") ||
                EF.Functions.ILike(c.SettingKey, $"%{term}%") ||
                (c.Description != null && EF.Functions.ILike(c.Description, $"%{term}%")));
        }
    }
}
