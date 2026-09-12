using System.Text.Json;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Modules.Dashboard.Services.Caching;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services.Caching;

namespace RestaurantPos.Api.Services
{
    public class DeliveryPartnerService : IDeliveryPartnerService
    {
        private readonly IDeliveryPartnerRepository _repository;
        private readonly ICacheService _cache;
        private readonly IDashboardCacheService _dashboardCache;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IConfigAuditService _audit;
        private readonly IBranchContext _branchContext;
        private readonly IBranchConfigurationService _branchConfigurationService;
        private readonly ILogger<DeliveryPartnerService> _logger;

        private string ActiveCacheKey => $"deliverypartners:active:{_tenantResolver.GetTenantId()}";

        public DeliveryPartnerService(
            IDeliveryPartnerRepository repository,
            ICacheService cache,
            IDashboardCacheService dashboardCache,
            ITenantResolver tenantResolver,
            ICurrentUserAccessor currentUser,
            IConfigAuditService audit,
            IBranchContext branchContext,
            IBranchConfigurationService branchConfigurationService,
            ILogger<DeliveryPartnerService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _dashboardCache = dashboardCache ?? throw new ArgumentNullException(nameof(dashboardCache));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<DeliveryPartnerDto>> GetActiveAsync(CancellationToken ct = default)
        {
            var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
            var enabledIds = await _branchConfigurationService.GetEnabledDeliveryPartnerIdsAsync(branchId, ct);
            var partners = await _cache.GetOrCreateAsync(
                $"{ActiveCacheKey}:{branchId:N}",
                async () => await _repository.GetActiveAsync(ct),
                slidingExpiration: TimeSpan.FromMinutes(15),
                absoluteExpiration: TimeSpan.FromHours(1),
                ct);

            return (partners ?? new List<DeliveryPartnerDto>())
                .Where(partner => enabledIds.Contains(partner.Id))
                .ToList();
        }

        public async Task<DeliveryPartnerPagedResultDto> GetPagedAsync(
            string? search,
            DeliveryPartnerStatus? status,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;
            var (items, totalCount) = await _repository.GetPagedAsync(search, status, page, pageSize, ct);
            return new DeliveryPartnerPagedResultDto { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
        }

        public async Task<DeliveryPartnerDto> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var partner = await _repository.GetByIdAsync(id, ct)
                ?? throw new NotFoundException($"Delivery partner {id} was not found.");
            return MapToDto(partner);
        }

        public async Task<DeliveryPartnerDto> CreateAsync(DeliveryPartnerUpsertDto dto, CancellationToken ct = default)
        {
            ValidatePartner(dto);
            await EnsureUniqueCodeAsync(dto.Code, null, ct);

            var partner = BuildPartner(dto);
            var created = await _repository.AddAsync(partner, ct);
            await _branchConfigurationService.EnsureMainBranchDeliveryPartnersAsync(
                new[] { new MainBranchConfigurationSeed(created.TenantId, created.Id) },
                ct);
            await AuditCostSharingAsync(null, created, ct);
            await AddLogAsync(created.Id, null, "PartnerCreated", new { created.Code, created.Name }, ct);
            await InvalidateActiveAsync(ct);

            _logger.LogInformation("Delivery partner {PartnerId} created with code {Code}", created.Id, created.Code);
            return MapToDto(created);
        }

        public async Task<DeliveryPartnerDto> UpdateAsync(Guid id, DeliveryPartnerUpsertDto dto, CancellationToken ct = default)
        {
            ValidatePartner(dto);
            var partner = await _repository.GetByIdAsync(id, ct)
                ?? throw new NotFoundException($"Delivery partner {id} was not found.");
            var previousCostSharing = CostSharingSnapshot(partner);
            await EnsureUniqueCodeAsync(dto.Code, id, ct);

            ApplyPartnerUpdate(partner, dto);
            var updated = await _repository.UpdateAsync(partner, ct);
            await AuditCostSharingAsync(previousCostSharing, updated, ct);
            await AddLogAsync(updated.Id, null, "PartnerUpdated", new { updated.Code, updated.Status }, ct);
            await InvalidateActiveAsync(ct);

            _logger.LogInformation("Delivery partner {PartnerId} updated", updated.Id);
            return MapToDto(updated);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var partner = await _repository.GetByIdAsync(id, ct)
                ?? throw new NotFoundException($"Delivery partner {id} was not found.");

            await _repository.DeleteAsync(partner, ct);
            await AddLogAsync(id, null, "PartnerDeleted", new { partner.Code }, ct);
            await InvalidateActiveAsync(ct);
        }

        public async Task<List<DeliveryPartnerProductMappingDto>> GetProductMappingsAsync(
            Guid productId,
            bool activePartnersOnly,
            CancellationToken ct = default)
        {
            if (!await _repository.ProductExistsAsync(productId, ct))
            {
                throw new NotFoundException($"Product {productId} was not found.");
            }

            var rows = await _repository.GetProductMappingsAsync(productId, activePartnersOnly, ct);
            return rows.Select(ApplyEffectivePrice).ToList();
        }

        public async Task<DeliveryPartnerProductPagedResultDto> GetPartnerProductsAsync(
            Guid partnerId,
            string? search,
            bool? enabledOnly,
            Guid? categoryId,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;
            await EnsurePartnerExistsAsync(partnerId, ct);

            var (items, totalCount) = await _repository.GetPartnerProductMappingsAsync(partnerId, search, enabledOnly, categoryId, page, pageSize, ct);
            return new DeliveryPartnerProductPagedResultDto
            {
                Items = items.Select(ApplyEffectivePrice).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<DeliveryPartnerProductMappingDto> UpsertProductMappingAsync(
            Guid productId,
            Guid partnerId,
            DeliveryPartnerProductMappingUpsertDto dto,
            CancellationToken ct = default)
        {
            ValidateMapping(dto);
            await EnsureProductAndPartnerExistAsync(productId, partnerId, ct);
            await EnsureMappingPriceIsValidAsync(productId, partnerId, dto, ct);

            var mapping = await _repository.GetMappingAsync(productId, partnerId, ct) ?? NewMapping(productId, partnerId);
            ApplyMappingUpdate(mapping, dto);
            await _repository.SaveMappingAsync(mapping, ct);
            await AddLogAsync(partnerId, productId, "ProductMappingUpdated", new { dto.IsEnabled, dto.PartnerProductId }, ct);
            // Mapping changes alter both the partner's mapping counters (cached in
            // `partners:active`) and the per-partner cashier product list (cached in
            // `products:cashier:*`). Without this invalidation the cashier popup keeps
            // returning the pre-mapping snapshot — symptom: "0/0" on the partner card
            // and an empty grid in the Delivery Order dialog after the admin maps products.
            await InvalidateActiveAsync(ct);

            var rows = await _repository.GetProductMappingsAsync(productId, activePartnersOnly: false, ct);
            return rows.Select(ApplyEffectivePrice).First(row => row.PartnerId == partnerId);
        }

        private async Task EnsureUniqueCodeAsync(string code, Guid? excludeId, CancellationToken ct)
        {
            if (await _repository.CodeExistsAsync(code, excludeId, ct))
            {
                throw new ConflictException($"A delivery partner with code '{code.Trim()}' already exists.");
            }
        }

        private async Task EnsurePartnerExistsAsync(Guid partnerId, CancellationToken ct)
        {
            if (!await _repository.PartnerExistsAsync(partnerId, ct))
            {
                throw new NotFoundException($"Delivery partner {partnerId} was not found.");
            }
        }

        private async Task EnsureProductAndPartnerExistAsync(Guid productId, Guid partnerId, CancellationToken ct)
        {
            if (!await _repository.ProductExistsAsync(productId, ct))
            {
                throw new NotFoundException($"Product {productId} was not found.");
            }

            await EnsurePartnerExistsAsync(partnerId, ct);
        }

        private DeliveryPartner BuildPartner(DeliveryPartnerUpsertDto dto)
        {
            var now = DateTime.UtcNow;
            var partner = new DeliveryPartner
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                CreatedAt = now
            };
            ApplyPartnerUpdate(partner, dto);
            return partner;
        }

        private static void ApplyPartnerUpdate(DeliveryPartner partner, DeliveryPartnerUpsertDto dto)
        {
            partner.Name = dto.Name.Trim();
            partner.NameAr = TrimToNull(dto.NameAr);
            partner.Code = dto.Code.Trim();
            partner.Description = TrimToNull(dto.Description);
            partner.DescriptionAr = TrimToNull(dto.DescriptionAr);
            partner.LogoUrl = TrimToNull(dto.LogoUrl);
            partner.Status = dto.Status;
            partner.DefaultPricingRuleType = dto.DefaultPricingRuleType;
            partner.DefaultPricingRuleValue = dto.DefaultPricingRuleValue;
            partner.DeliveryCostRuleType = dto.DeliveryCostRuleType;
            partner.DefaultDeliveryCostValue = dto.DefaultDeliveryCostValue;
            ApplyCostSharing(dto, partner);
            partner.IntegrationEnabled = dto.IntegrationEnabled;
            partner.IntegrationSettingsJson = TrimToNull(dto.IntegrationSettingsJson);
            partner.CredentialsJson = TrimToNull(dto.CredentialsJson) ?? partner.CredentialsJson;
            partner.SortOrder = dto.SortOrder;
        }

        private DeliveryPartnerProduct NewMapping(Guid productId, Guid partnerId) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantResolver.GetTenantId(),
            ProductId = productId,
            DeliveryPartnerId = partnerId
        };

        private static void ApplyMappingUpdate(DeliveryPartnerProduct mapping, DeliveryPartnerProductMappingUpsertDto dto)
        {
            mapping.PartnerProductId = TrimToNull(dto.PartnerProductId);
            mapping.IsEnabled = dto.IsEnabled;
            mapping.IsAvailable = dto.IsAvailable;
            mapping.PricingRuleType = dto.PricingRuleType;
            mapping.PricingRuleValue = dto.PricingRuleValue;
            mapping.CustomPrice = dto.CustomPrice;
            mapping.AvailableStartDate = dto.AvailableStartDate;
            mapping.AvailableEndDate = dto.AvailableEndDate;
            mapping.AvailableFrom = dto.AvailableFrom;
            mapping.AvailableTo = dto.AvailableTo;
        }

        private static DeliveryPartnerDto MapToDto(DeliveryPartner p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            NameAr = p.NameAr,
            Code = p.Code,
            Description = p.Description,
            DescriptionAr = p.DescriptionAr,
            LogoUrl = p.LogoUrl,
            Status = p.Status,
            DefaultPricingRuleType = p.DefaultPricingRuleType,
            DefaultPricingRuleValue = p.DefaultPricingRuleValue,
            DeliveryCostRuleType = p.DeliveryCostRuleType,
            DefaultDeliveryCostValue = p.DefaultDeliveryCostValue,
            CostSharingMode = p.CostSharingMode,
            CostSharingScope = p.CostSharingScope,
            CostSharingCommissionPercentage = p.CostSharingCommissionPercentage,
            CostSharingRestaurantPercentage = p.CostSharingRestaurantPercentage,
            CostSharingCounterpartyPercentage = p.CostSharingCounterpartyPercentage,
            IntegrationEnabled = p.IntegrationEnabled,
            IntegrationSettingsJson = p.IntegrationSettingsJson,
            HasCredentials = !string.IsNullOrWhiteSpace(p.CredentialsJson),
            SortOrder = p.SortOrder,
            ProductMappingCount = p.ProductMappings.Count(m => m.Product.IsActive),
            EnabledProductCount = p.ProductMappings.Count(m => m.IsEnabled && m.IsAvailable && m.Product.IsActive),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };

        private static DeliveryPartnerProductMappingDto ApplyEffectivePrice(DeliveryPartnerProductMappingDto row)
        {
            var rule = row.PricingRuleType ?? row.PartnerDefaultPricingRuleType;
            var value = row.PricingRuleType.HasValue ? row.PricingRuleValue ?? 0m : row.PartnerDefaultPricingRuleValue;
            row.EffectivePricingRuleType = rule;
            row.EffectivePricingRuleValue = rule == DeliveryPartnerPricingRuleType.CustomPrice ? row.CustomPrice ?? 0m : value;
            row.EffectivePrice = DeliveryPartnerPricingHelper.CalculatePrice(row.ProductBasePrice, rule, value, row.CustomPrice);
            row.PriceSource = row.PricingRuleType.HasValue ? "ProductOverride" : "PartnerDefault";
            return row;
        }

        private static void ValidatePartner(DeliveryPartnerUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("Delivery partner name is required.");
            if (string.IsNullOrWhiteSpace(dto.NameAr)) throw new ValidationException("Delivery partner Arabic name is required.");
            if (string.IsNullOrWhiteSpace(dto.Code)) throw new ValidationException("Delivery partner code is required.");
            if (dto.DefaultPricingRuleType == DeliveryPartnerPricingRuleType.CustomPrice)
            {
                throw new ValidationException("Partner default pricing cannot be a custom product price.");
            }

            if (dto.DefaultPricingRuleType == DeliveryPartnerPricingRuleType.PercentageDiscount && dto.DefaultPricingRuleValue > 100)
            {
                throw new ValidationException("Partner default percentage discount cannot exceed 100%.");
            }

            if (dto.CostSharingCommissionPercentage is < 0m or > 100m)
                throw new ValidationException("Cost sharing commission percentage must be between 0 and 100.");

            if (dto.CostSharingMode == CostSharingMode.Shared &&
                !CostSharingHelper.SharedPercentagesTotalOneHundred(
                    dto.CostSharingRestaurantPercentage,
                    dto.CostSharingCounterpartyPercentage))
            {
                throw new ValidationException("Cost sharing percentages must total 100%.");
            }
        }

        private static void ApplyCostSharing(DeliveryPartnerUpsertDto dto, DeliveryPartner partner)
        {
            var percentages = CostSharingHelper.NormalizePercentages(
                dto.CostSharingMode,
                dto.CostSharingRestaurantPercentage,
                dto.CostSharingCounterpartyPercentage);

            partner.CostSharingMode = dto.CostSharingMode;
            partner.CostSharingScope = dto.CostSharingScope;
            partner.CostSharingCommissionPercentage =
                CostSharingHelper.NormalizePercentage(dto.CostSharingCommissionPercentage);
            partner.CostSharingRestaurantPercentage = percentages.RestaurantPercentage;
            partner.CostSharingCounterpartyPercentage = percentages.CounterpartyPercentage;
        }

        private async Task AuditCostSharingAsync(object? previousValue, DeliveryPartner partner, CancellationToken ct)
        {
            var nextValue = CostSharingSnapshot(partner);
            if (previousValue != null && previousValue.Equals(nextValue))
                return;

            await _audit.LogAsync(
                partner.TenantId,
                ConfigAuditEventType.CostSharingConfigChanged,
                previousValue,
                nextValue,
                targetId: partner.Id,
                reason: "DeliveryPartner",
                ct: ct);
        }

        private static object CostSharingSnapshot(DeliveryPartner partner) => new
        {
            TargetType = CostSharingTargetType.DeliveryPartner,
            TargetId = partner.Id,
            TargetName = partner.Name,
            TargetCode = partner.Code,
            Mode = partner.CostSharingMode,
            RestaurantPercentage = partner.CostSharingRestaurantPercentage,
            CounterpartyPercentage = partner.CostSharingCounterpartyPercentage,
            partner.CostSharingScope,
            partner.CostSharingCommissionPercentage
        };

        private static void ValidateMapping(DeliveryPartnerProductMappingUpsertDto dto)
        {
            if (dto.PricingRuleType == DeliveryPartnerPricingRuleType.CustomPrice && (!dto.CustomPrice.HasValue || dto.CustomPrice <= 0))
            {
                throw new ValidationException("Custom partner price must be greater than zero.");
            }

            if (dto.PricingRuleType == DeliveryPartnerPricingRuleType.PercentageDiscount && dto.PricingRuleValue > 100)
            {
                throw new ValidationException("Percentage discount cannot exceed 100%.");
            }
        }

        private async Task EnsureMappingPriceIsValidAsync(
            Guid productId,
            Guid partnerId,
            DeliveryPartnerProductMappingUpsertDto dto,
            CancellationToken ct)
        {
            var row = (await _repository.GetProductMappingsAsync(productId, activePartnersOnly: false, ct))
                .First(r => r.PartnerId == partnerId);
            row.PricingRuleType = dto.PricingRuleType;
            row.PricingRuleValue = dto.PricingRuleValue;
            row.CustomPrice = dto.CustomPrice;

            if (ApplyEffectivePrice(row).EffectivePrice < 0)
            {
                throw new ValidationException("Partner price cannot be negative.");
            }
        }

        private async Task AddLogAsync(Guid partnerId, Guid? productId, string action, object details, CancellationToken ct)
        {
            await _repository.AddActivityLogAsync(new DeliveryPartnerActivityLog
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                DeliveryPartnerId = partnerId,
                ProductId = productId,
                ActionType = action,
                DetailsJson = JsonSerializer.Serialize(details),
                PerformedById = _currentUser.UserIdOrNull
            }, ct);
        }

        private async Task InvalidateActiveAsync(CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            await _cache.RemoveAsync(ActiveCacheKey, ct);
            await _cache.RemoveByPatternAsync($"{ActiveCacheKey}:*", ct);
            await _cache.RemoveByPatternAsync(CacheKeys.ProductsPattern(tenantId), ct);
            await _cache.RemoveByPatternAsync($"pos:products:cashier:*:{tenantId}:*", ct);

            // Cashier closing + dashboards group sales by partner name — flush so any
            // rename / enable / disable is reflected on the next dashboard read.
            try
            {
                await _dashboardCache.InvalidateScopeAsync(tenantId, DashboardScope.Operations, ct);
                await _dashboardCache.InvalidateScopeAsync(tenantId, DashboardScope.Financial, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dashboard cache invalidation skipped after delivery-partner change.");
            }
        }

        private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
