using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public readonly record struct MainBranchConfigurationSeed(Guid TenantId, Guid EntityId, int DisplayOrder = 0);

    public interface IBranchConfigurationService
    {
        Task<BranchConfigurationOptionsDto> GetOptionsAsync(CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchProductConfigurationDto>> GetBranchProductsAsync(Guid? branchId, Guid? productId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchProductConfigurationDto> CreateBranchProductAsync(BranchProductConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchProductConfigurationDto> UpdateBranchProductAsync(Guid id, BranchProductConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchProductAsync(Guid id, CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchCategoryConfigurationDto>> GetBranchCategoriesAsync(Guid? branchId, Guid? categoryId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchCategoryConfigurationDto> CreateBranchCategoryAsync(BranchCategoryConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchCategoryConfigurationDto> UpdateBranchCategoryAsync(Guid id, BranchCategoryConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchCategoryAsync(Guid id, CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchSubcategoryConfigurationDto>> GetBranchSubcategoriesAsync(Guid? branchId, Guid? subcategoryId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchSubcategoryConfigurationDto> CreateBranchSubcategoryAsync(BranchSubcategoryConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchSubcategoryConfigurationDto> UpdateBranchSubcategoryAsync(Guid id, BranchSubcategoryConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchSubcategoryAsync(Guid id, CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchModifierConfigurationDto>> GetBranchModifiersAsync(Guid? branchId, Guid? modifierId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchModifierConfigurationDto> CreateBranchModifierAsync(BranchModifierConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchModifierConfigurationDto> UpdateBranchModifierAsync(Guid id, BranchModifierConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchModifierAsync(Guid id, CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchModifierGroupConfigurationDto>> GetBranchModifierGroupsAsync(Guid? branchId, Guid? modifierGroupId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchModifierGroupConfigurationDto> CreateBranchModifierGroupAsync(BranchModifierGroupConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchModifierGroupConfigurationDto> UpdateBranchModifierGroupAsync(Guid id, BranchModifierGroupConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchModifierGroupAsync(Guid id, CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchProductOptionConfigurationDto>> GetBranchProductOptionsAsync(Guid? branchId, Guid? productOptionId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchProductOptionConfigurationDto> CreateBranchProductOptionAsync(BranchProductOptionConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchProductOptionConfigurationDto> UpdateBranchProductOptionAsync(Guid id, BranchProductOptionConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchProductOptionAsync(Guid id, CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchPaymentMethodConfigurationDto>> GetBranchPaymentMethodsAsync(Guid? branchId, Guid? paymentMethodId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchPaymentMethodConfigurationDto> CreateBranchPaymentMethodAsync(BranchPaymentMethodConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchPaymentMethodConfigurationDto> UpdateBranchPaymentMethodAsync(Guid id, BranchPaymentMethodConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchPaymentMethodAsync(Guid id, CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchDeliveryPartnerConfigurationDto>> GetBranchDeliveryPartnersAsync(Guid? branchId, Guid? deliveryPartnerId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchDeliveryPartnerConfigurationDto> CreateBranchDeliveryPartnerAsync(BranchDeliveryPartnerConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchDeliveryPartnerConfigurationDto> UpdateBranchDeliveryPartnerAsync(Guid id, BranchDeliveryPartnerConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchDeliveryPartnerAsync(Guid id, CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchDeliveryZoneConfigurationDto>> GetBranchDeliveryZonesAsync(Guid? branchId, Guid? deliveryZoneId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchDeliveryZoneConfigurationDto> CreateBranchDeliveryZoneAsync(BranchDeliveryZoneConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchDeliveryZoneConfigurationDto> UpdateBranchDeliveryZoneAsync(Guid id, BranchDeliveryZoneConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchDeliveryZoneAsync(Guid id, CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchPrinterConfigurationDto>> GetBranchPrintersAsync(Guid? branchId, Guid? printerId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchPrinterConfigurationDto> CreateBranchPrinterAsync(BranchPrinterConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchPrinterConfigurationDto> UpdateBranchPrinterAsync(Guid id, BranchPrinterConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchPrinterAsync(Guid id, CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchOfferConfigurationDto>> GetBranchOffersAsync(Guid? branchId, int? offerId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchOfferConfigurationDto> CreateBranchOfferAsync(BranchOfferConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchOfferConfigurationDto> UpdateBranchOfferAsync(Guid id, BranchOfferConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchOfferAsync(Guid id, CancellationToken ct = default);

        Task<BranchConfigurationPagedResultDto<BranchSettingsConfigurationDto>> GetBranchSettingsAsync(Guid? branchId, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<BranchSettingsConfigurationDto> CreateBranchSettingsAsync(BranchSettingsConfigurationUpsertDto dto, CancellationToken ct = default);
        Task<BranchSettingsConfigurationDto> UpdateBranchSettingsAsync(Guid id, BranchSettingsConfigurationUpsertDto dto, CancellationToken ct = default);
        Task DeleteBranchSettingsAsync(Guid id, CancellationToken ct = default);

        Task<IReadOnlySet<Guid>> GetAvailableProductIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<IReadOnlySet<Guid>> GetVisibleCategoryIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<IReadOnlySet<Guid>> GetVisibleSubcategoryIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<IReadOnlySet<Guid>> GetAvailableModifierIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<IReadOnlySet<Guid>> GetAvailableModifierGroupIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<IReadOnlySet<Guid>> GetAvailableProductOptionIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<IReadOnlySet<Guid>> GetEnabledPaymentMethodIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<IReadOnlySet<Guid>> GetEnabledDeliveryPartnerIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<IReadOnlySet<Guid>> GetEnabledPrinterIdsAsync(Guid branchId, CancellationToken ct = default);
        Task<IReadOnlySet<int>> GetEnabledOfferIdsAsync(Guid branchId, CancellationToken ct = default);

        Task EnsureMainBranchProductsAsync(IReadOnlyCollection<MainBranchConfigurationSeed> products, CancellationToken ct = default);
        Task EnsureMainBranchCategoriesAsync(IReadOnlyCollection<MainBranchConfigurationSeed> categories, CancellationToken ct = default);
        Task EnsureMainBranchSubcategoriesAsync(IReadOnlyCollection<MainBranchConfigurationSeed> subcategories, CancellationToken ct = default);
        Task EnsureMainBranchModifierGroupsAsync(IReadOnlyCollection<MainBranchConfigurationSeed> modifierGroups, CancellationToken ct = default);
        Task EnsureMainBranchModifiersAsync(IReadOnlyCollection<MainBranchConfigurationSeed> modifiers, CancellationToken ct = default);
        Task EnsureMainBranchProductOptionsAsync(IReadOnlyCollection<MainBranchConfigurationSeed> productOptions, CancellationToken ct = default);
        Task EnsureMainBranchPaymentMethodsAsync(IReadOnlyCollection<MainBranchConfigurationSeed> paymentMethods, CancellationToken ct = default);
        Task EnsureMainBranchDeliveryPartnersAsync(IReadOnlyCollection<MainBranchConfigurationSeed> deliveryPartners, CancellationToken ct = default);
        Task EnsureMainBranchPrintersAsync(IReadOnlyCollection<MainBranchConfigurationSeed> printers, CancellationToken ct = default);
        Task EnsureMainBranchOfferAsync(Guid tenantId, int offerId, CancellationToken ct = default);

        Task RemoveProductBranchConfigurationsAsync(Guid productId, CancellationToken ct = default);
        Task RemoveProductOptionBranchConfigurationsAsync(Guid productOptionId, CancellationToken ct = default);
        Task RemoveCategoryBranchConfigurationsAsync(Guid categoryId, CancellationToken ct = default);
        Task RemoveSubcategoryBranchConfigurationsAsync(Guid subcategoryId, CancellationToken ct = default);
        Task RemoveModifierBranchConfigurationsAsync(IReadOnlyCollection<Guid> modifierIds, CancellationToken ct = default);
        Task RemoveModifierGroupBranchConfigurationsAsync(IReadOnlyCollection<Guid> modifierGroupIds, CancellationToken ct = default);
        Task RemoveOfferBranchConfigurationsAsync(int offerId, CancellationToken ct = default);
    }
}
