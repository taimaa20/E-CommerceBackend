using RestaurantPos.Api.Models;
using CategoryEntity = RestaurantPos.Api.Models.Category;

namespace RestaurantPos.Api.Repositories
{
    public interface IBranchConfigurationRepository
    {
        Task<List<Branch>> GetBranchOptionsAsync(CancellationToken ct = default);
        Task<Dictionary<Guid, Guid>> GetMainBranchIdsByTenantAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken ct = default);
        Task<List<Product>> GetProductOptionsAsync(CancellationToken ct = default);
        Task<List<CategoryEntity>> GetCategoryOptionsAsync(CancellationToken ct = default);
        Task<List<Subcategory>> GetSubcategoryOptionsAsync(CancellationToken ct = default);
        Task<List<Modifier>> GetModifierOptionsAsync(CancellationToken ct = default);
        Task<List<ModifierGroup>> GetModifierGroupOptionsAsync(CancellationToken ct = default);
        Task<List<ProductOption>> GetProductOptionOptionsAsync(CancellationToken ct = default);
        Task<List<PaymentMethod>> GetPaymentMethodOptionsAsync(CancellationToken ct = default);
        Task<List<DeliveryPartner>> GetDeliveryPartnerOptionsAsync(CancellationToken ct = default);
        Task<List<Printer>> GetPrinterOptionsAsync(CancellationToken ct = default);
        Task<List<Offer>> GetOfferOptionsAsync(CancellationToken ct = default);

        Task<bool> BranchExistsAsync(Guid branchId, CancellationToken ct = default);
        Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct = default);
        Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken ct = default);
        Task<bool> SubcategoryExistsAsync(Guid subcategoryId, CancellationToken ct = default);
        Task<bool> ModifierExistsAsync(Guid modifierId, CancellationToken ct = default);
        Task<bool> ModifierGroupExistsAsync(Guid modifierGroupId, CancellationToken ct = default);
        Task<bool> ProductOptionExistsAsync(Guid productOptionId, CancellationToken ct = default);
        Task<bool> PaymentMethodExistsAsync(Guid paymentMethodId, CancellationToken ct = default);
        Task<bool> DeliveryPartnerExistsAsync(Guid deliveryPartnerId, CancellationToken ct = default);
        Task<bool> PrinterExistsAsync(Guid printerId, CancellationToken ct = default);
        Task<bool> OfferExistsAsync(int offerId, CancellationToken ct = default);

        Task<(List<BranchProduct> Items, int TotalCount)> GetBranchProductsAsync(Guid? branchId, Guid? productId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchProduct?> GetBranchProductTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchProductExistsAsync(Guid branchId, Guid productId, Guid? excludeId, CancellationToken ct = default);

        Task<(List<BranchCategory> Items, int TotalCount)> GetBranchCategoriesAsync(Guid? branchId, Guid? categoryId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchCategory?> GetBranchCategoryTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchCategoryExistsAsync(Guid branchId, Guid categoryId, Guid? excludeId, CancellationToken ct = default);

        Task<(List<BranchSubcategory> Items, int TotalCount)> GetBranchSubcategoriesAsync(Guid? branchId, Guid? subcategoryId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchSubcategory?> GetBranchSubcategoryTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchSubcategoryExistsAsync(Guid branchId, Guid subcategoryId, Guid? excludeId, CancellationToken ct = default);

        Task<(List<BranchModifier> Items, int TotalCount)> GetBranchModifiersAsync(Guid? branchId, Guid? modifierId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchModifier?> GetBranchModifierTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchModifierExistsAsync(Guid branchId, Guid modifierId, Guid? excludeId, CancellationToken ct = default);

        Task<(List<BranchModifierGroup> Items, int TotalCount)> GetBranchModifierGroupsAsync(Guid? branchId, Guid? modifierGroupId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchModifierGroup?> GetBranchModifierGroupTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchModifierGroupExistsAsync(Guid branchId, Guid modifierGroupId, Guid? excludeId, CancellationToken ct = default);

        Task<(List<BranchProductOption> Items, int TotalCount)> GetBranchProductOptionsAsync(Guid? branchId, Guid? productOptionId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchProductOption?> GetBranchProductOptionTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchProductOptionExistsAsync(Guid branchId, Guid productOptionId, Guid? excludeId, CancellationToken ct = default);

        Task<(List<BranchPaymentMethod> Items, int TotalCount)> GetBranchPaymentMethodsAsync(Guid? branchId, Guid? paymentMethodId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchPaymentMethod?> GetBranchPaymentMethodTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchPaymentMethodExistsAsync(Guid branchId, Guid paymentMethodId, Guid? excludeId, CancellationToken ct = default);

        Task<(List<BranchDeliveryPartner> Items, int TotalCount)> GetBranchDeliveryPartnersAsync(Guid? branchId, Guid? deliveryPartnerId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchDeliveryPartner?> GetBranchDeliveryPartnerTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchDeliveryPartnerExistsAsync(Guid branchId, Guid deliveryPartnerId, Guid? excludeId, CancellationToken ct = default);

        Task<(List<DeliveryZone> Items, int TotalCount)> GetBranchDeliveryZonesAsync(Guid? branchId, Guid? deliveryZoneId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<DeliveryZone?> GetBranchDeliveryZoneTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchDeliveryZoneCodeExistsAsync(Guid branchId, string code, Guid? excludeId, CancellationToken ct = default);

        Task<(List<BranchPrinter> Items, int TotalCount)> GetBranchPrintersAsync(Guid? branchId, Guid? printerId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchPrinter?> GetBranchPrinterTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchPrinterExistsAsync(Guid branchId, Guid printerId, Guid? excludeId, CancellationToken ct = default);

        Task<(List<BranchOffer> Items, int TotalCount)> GetBranchOffersAsync(Guid? branchId, int? offerId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchOffer?> GetBranchOfferTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchOfferExistsAsync(Guid branchId, int offerId, Guid? excludeId, CancellationToken ct = default);

        Task<(List<BranchSettings> Items, int TotalCount)> GetBranchSettingsAsync(Guid? branchId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchSettings?> GetBranchSettingsTrackedAsync(Guid id, CancellationToken ct = default);
        Task<bool> BranchSettingsExistsAsync(Guid branchId, string settingKey, Guid? excludeId, CancellationToken ct = default);

        Task<List<Guid>> GetAvailableProductIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<List<Guid>> GetVisibleCategoryIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<List<Guid>> GetVisibleSubcategoryIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<List<Guid>> GetAvailableModifierIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<List<Guid>> GetAvailableModifierGroupIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<List<Guid>> GetAvailableProductOptionIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<List<Guid>> GetEnabledPaymentMethodIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<List<Guid>> GetEnabledDeliveryPartnerIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<List<Guid>> GetEnabledPrinterIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<List<int>> GetEnabledOfferIdsAsync(Guid branchId, CancellationToken ct = default);

        Task<List<(Guid BranchId, Guid EntityId)>> GetBranchProductKeysAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
        Task<List<(Guid BranchId, Guid EntityId)>> GetBranchCategoryKeysAsync(IReadOnlyCollection<Guid> categoryIds, CancellationToken ct = default);
        Task<List<(Guid BranchId, Guid EntityId)>> GetBranchSubcategoryKeysAsync(IReadOnlyCollection<Guid> subcategoryIds, CancellationToken ct = default);
        Task<List<(Guid BranchId, Guid EntityId)>> GetBranchModifierKeysAsync(IReadOnlyCollection<Guid> modifierIds, CancellationToken ct = default);
        Task<List<(Guid BranchId, Guid EntityId)>> GetBranchModifierGroupKeysAsync(IReadOnlyCollection<Guid> modifierGroupIds, CancellationToken ct = default);
        Task<List<(Guid BranchId, Guid EntityId)>> GetBranchProductOptionKeysAsync(IReadOnlyCollection<Guid> productOptionIds, CancellationToken ct = default);
        Task<List<(Guid BranchId, Guid EntityId)>> GetBranchPaymentMethodKeysAsync(IReadOnlyCollection<Guid> paymentMethodIds, CancellationToken ct = default);
        Task<List<(Guid BranchId, Guid EntityId)>> GetBranchDeliveryPartnerKeysAsync(IReadOnlyCollection<Guid> deliveryPartnerIds, CancellationToken ct = default);
        Task<List<(Guid BranchId, Guid EntityId)>> GetBranchPrinterKeysAsync(IReadOnlyCollection<Guid> printerIds, CancellationToken ct = default);
        Task<List<(Guid BranchId, int OfferId)>> GetBranchOfferKeysAsync(IReadOnlyCollection<int> offerIds, CancellationToken ct = default);

        Task AddAsync<TEntity>(TEntity entity, CancellationToken ct = default) where TEntity : BaseEntity;
        Task AddRangeAsync<TEntity>(IReadOnlyCollection<TEntity> entities, CancellationToken ct = default) where TEntity : BaseEntity;
        Task SaveChangesAsync(CancellationToken ct = default);
        void Detach<TEntity>(TEntity entity) where TEntity : BaseEntity;
        Task DeleteAsync<TEntity>(TEntity entity, CancellationToken ct = default) where TEntity : BaseEntity;
        Task RemoveBranchProductsByProductIdAsync(Guid productId, CancellationToken ct = default);
        Task RemoveBranchProductOptionsByProductIdAsync(Guid productId, CancellationToken ct = default);
        Task RemoveBranchProductOptionsByProductOptionIdAsync(Guid productOptionId, CancellationToken ct = default);
        Task RemoveBranchCategoriesByCategoryIdAsync(Guid categoryId, CancellationToken ct = default);
        Task RemoveBranchSubcategoriesBySubcategoryIdAsync(Guid subcategoryId, CancellationToken ct = default);
        Task RemoveBranchModifiersByModifierIdsAsync(IReadOnlyCollection<Guid> modifierIds, CancellationToken ct = default);
        Task RemoveBranchModifierGroupsByModifierGroupIdsAsync(IReadOnlyCollection<Guid> modifierGroupIds, CancellationToken ct = default);
        Task RemoveBranchOffersByOfferIdAsync(int offerId, CancellationToken ct = default);
    }
}
