using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Modules.Dashboard.Services.Caching;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Services.Caching;

namespace RestaurantPos.Api.Services
{
    public class PaymentMethodService : IPaymentMethodService
    {
        private readonly IPaymentMethodRepository _repository;
        private readonly ICacheService _cache;
        private readonly IDashboardCacheService _dashboardCache;
        private readonly ITenantResolver _tenantResolver;
        private readonly IConfigAuditService _audit;
        private readonly IBranchContext _branchContext;
        private readonly IBranchConfigurationService _branchConfigurationService;
        private readonly ILogger<PaymentMethodService> _logger;

        private Guid TenantId => _tenantResolver.GetTenantId();

        public PaymentMethodService(
            IPaymentMethodRepository repository,
            ICacheService cache,
            IDashboardCacheService dashboardCache,
            ITenantResolver tenantResolver,
            IConfigAuditService audit,
            IBranchContext branchContext,
            IBranchConfigurationService branchConfigurationService,
            ILogger<PaymentMethodService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _dashboardCache = dashboardCache ?? throw new ArgumentNullException(nameof(dashboardCache));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<PaymentMethodDto>> GetActiveAsync(CancellationToken ct = default)
        {
            var tenantId = TenantId;
            var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
            var enabledIds = await _branchConfigurationService.GetEnabledPaymentMethodIdsAsync(branchId, ct);
            var cached = await _cache.GetOrCreateAsync(
                $"{CacheKeys.PaymentMethodsActive(tenantId)}:{branchId:N}",
                () => _repository.GetActiveAsync(ct),
                TimeSpan.FromMinutes(15),
                TimeSpan.FromHours(1),
                ct);

            return (cached ?? new List<PaymentMethod>())
                .Where(method => enabledIds.Contains(method.Id))
                .Select(MapToDto)
                .ToList();
        }

        public async Task<List<PaymentMethodDto>> GetAllAsync(CancellationToken ct = default)
        {
            var tenantId = TenantId;
            var cached = await _cache.GetOrCreateAsync(
                CacheKeys.PaymentMethodsAll(tenantId),
                async () => (await _repository.GetAllAsync(ct)).Select(MapToDto).ToList(),
                TimeSpan.FromMinutes(15),
                TimeSpan.FromHours(1),
                ct);

            return cached ?? new List<PaymentMethodDto>();
        }

        public async Task<PaymentMethodDto> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await _repository.GetByIdAsync(id, ct)
                ?? throw new NotFoundException($"Payment method {id} was not found.");
            return MapToDto(entity);
        }

        public async Task<PaymentMethodDto> CreateAsync(PaymentMethodCreateDto dto, CancellationToken ct = default)
        {
            Validate(dto.NameEn, dto.Code, dto.IsActive, dto.IsDefault);
            ValidateCostSharing(dto.CostSharingMode, dto.CostSharingCommissionPercentage,
                dto.CostSharingRestaurantPercentage, dto.CostSharingCounterpartyPercentage);
            var code = NormalizeCode(dto.Code);

            if (await _repository.CodeExistsAsync(code, null, ct))
                throw new ConflictException($"A payment method with code '{code}' already exists.");

            if (dto.IsDefault)
                await _repository.ClearDefaultAsync(null, ct);

            var created = await _repository.AddAsync(CreateEntity(dto, code), ct);
            await _branchConfigurationService.EnsureMainBranchPaymentMethodsAsync(
                new[] { new MainBranchConfigurationSeed(created.TenantId, created.Id) },
                ct);
            await AuditCostSharingAsync(null, created, ct);
            await InvalidateCachesAsync(ct);
            _logger.LogInformation("Payment method {PaymentMethodId} created with code {Code}", created.Id, created.Code);
            return MapToDto(created);
        }

        public async Task<PaymentMethodDto> UpdateAsync(Guid id, PaymentMethodUpdateDto dto, CancellationToken ct = default)
        {
            Validate(dto.NameEn, dto.Code, dto.IsActive, dto.IsDefault);
            ValidateCostSharing(dto.CostSharingMode, dto.CostSharingCommissionPercentage,
                dto.CostSharingRestaurantPercentage, dto.CostSharingCounterpartyPercentage);
            var code = NormalizeCode(dto.Code);
            var entity = await _repository.GetByIdAsync(id, ct)
                ?? throw new NotFoundException($"Payment method {id} was not found.");
            var previousCostSharing = CostSharingSnapshot(entity);

            if (await _repository.CodeExistsAsync(code, id, ct))
                throw new ConflictException($"A payment method with code '{code}' already exists.");

            if (dto.IsDefault)
                await _repository.ClearDefaultAsync(id, ct);

            Apply(dto, entity, code);
            var updated = await _repository.UpdateAsync(entity, ct);
            await AuditCostSharingAsync(previousCostSharing, updated, ct);
            await InvalidateCachesAsync(ct);
            _logger.LogInformation("Payment method {PaymentMethodId} updated", updated.Id);
            return MapToDto(updated);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await _repository.GetByIdAsync(id, ct)
                ?? throw new NotFoundException($"Payment method {id} was not found.");

            await _repository.DeleteAsync(entity, ct);
            await InvalidateCachesAsync(ct);
            _logger.LogInformation("Payment method {PaymentMethodId} soft-deleted", id);
        }

        private PaymentMethod CreateEntity(PaymentMethodCreateDto dto, string code)
        {
            var method = new PaymentMethod
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                NameEn = dto.NameEn.Trim(),
                NameAr = NormalizeOptional(dto.NameAr),
                Code = code,
                Icon = NormalizeOptional(dto.Icon),
                DisplayOrder = dto.DisplayOrder,
                IsActive = dto.IsActive,
                IsDefault = dto.IsDefault,
                RequiresReferenceNumber = dto.RequiresReferenceNumber
            };
            ApplyCostSharing(dto.CostSharingMode, dto.CostSharingScope, dto.CostSharingCommissionPercentage,
                dto.CostSharingRestaurantPercentage, dto.CostSharingCounterpartyPercentage, method);
            return method;
        }

        private static void Apply(PaymentMethodUpdateDto dto, PaymentMethod entity, string code)
        {
            entity.NameEn = dto.NameEn.Trim();
            entity.NameAr = NormalizeOptional(dto.NameAr);
            entity.Code = code;
            entity.Icon = NormalizeOptional(dto.Icon);
            entity.DisplayOrder = dto.DisplayOrder;
            entity.IsActive = dto.IsActive;
            entity.IsDefault = dto.IsDefault;
            entity.RequiresReferenceNumber = dto.RequiresReferenceNumber;
            ApplyCostSharing(dto.CostSharingMode, dto.CostSharingScope, dto.CostSharingCommissionPercentage,
                dto.CostSharingRestaurantPercentage, dto.CostSharingCounterpartyPercentage, entity);
        }

        private async Task InvalidateCachesAsync(CancellationToken ct)
        {
            var tenantId = TenantId;
            await _cache.RemoveAsync(CacheKeys.PaymentMethodsActive(tenantId), ct);
            await _cache.RemoveAsync(CacheKeys.PaymentMethodsAll(tenantId), ct);
            await _cache.RemoveByPatternAsync($"{CacheKeys.PaymentMethodsActive(tenantId)}:*", ct);

            // Dashboard payment-split + filter-options reflect the registry — flush so
            // newly added / renamed / deactivated methods appear immediately.
            try
            {
                await _dashboardCache.InvalidateScopeAsync(tenantId, DashboardScope.Operations, ct);
                await _dashboardCache.InvalidateScopeAsync(tenantId, DashboardScope.Financial, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dashboard cache invalidation skipped after payment-method change.");
            }
        }

        private static void Validate(string nameEn, string code, bool isActive, bool isDefault)
        {
            if (string.IsNullOrWhiteSpace(nameEn))
                throw new ValidationException("Payment method English name is required.");

            if (string.IsNullOrWhiteSpace(code))
                throw new ValidationException("Payment method code is required.");

            if (isDefault && !isActive)
                throw new ValidationException("The default payment method must be active.");
        }

        private static void ValidateCostSharing(
            CostSharingMode mode,
            decimal commissionPercentage,
            decimal restaurantPercentage,
            decimal counterpartyPercentage)
        {
            if (commissionPercentage is < 0m or > 100m)
                throw new ValidationException("Cost sharing commission percentage must be between 0 and 100.");

            if (mode == CostSharingMode.Shared &&
                !CostSharingHelper.SharedPercentagesTotalOneHundred(restaurantPercentage, counterpartyPercentage))
            {
                throw new ValidationException("Cost sharing percentages must total 100%.");
            }
        }

        private static void ApplyCostSharing(
            CostSharingMode mode,
            CostSharingScope scope,
            decimal commissionPercentage,
            decimal restaurantPercentage,
            decimal counterpartyPercentage,
            PaymentMethod method)
        {
            var percentages = CostSharingHelper.NormalizePercentages(mode, restaurantPercentage, counterpartyPercentage);
            method.CostSharingMode = mode;
            method.CostSharingScope = scope;
            method.CostSharingCommissionPercentage = CostSharingHelper.NormalizePercentage(commissionPercentage);
            method.CostSharingRestaurantPercentage = percentages.RestaurantPercentage;
            method.CostSharingCounterpartyPercentage = percentages.CounterpartyPercentage;
        }

        private async Task AuditCostSharingAsync(object? previousValue, PaymentMethod method, CancellationToken ct)
        {
            var nextValue = CostSharingSnapshot(method);
            if (previousValue != null && previousValue.Equals(nextValue))
                return;

            await _audit.LogAsync(
                method.TenantId,
                ConfigAuditEventType.CostSharingConfigChanged,
                previousValue,
                nextValue,
                targetId: method.Id,
                reason: "PaymentMethod",
                ct: ct);
        }

        private static object CostSharingSnapshot(PaymentMethod method) => new
        {
            TargetType = CostSharingTargetType.PaymentMethod,
            TargetId = method.Id,
            TargetName = method.NameEn,
            TargetCode = method.Code,
            Mode = method.CostSharingMode,
            RestaurantPercentage = method.CostSharingRestaurantPercentage,
            CounterpartyPercentage = method.CostSharingCounterpartyPercentage,
            method.CostSharingScope,
            method.CostSharingCommissionPercentage
        };

        private static string NormalizeCode(string value)
            => value.Trim().ToUpperInvariant().Replace(' ', '_');

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static PaymentMethodDto MapToDto(PaymentMethod method) => new()
        {
            Id = method.Id,
            NameEn = method.NameEn,
            NameAr = method.NameAr,
            Code = method.Code,
            Icon = method.Icon,
            DisplayOrder = method.DisplayOrder,
            IsActive = method.IsActive,
            IsDefault = method.IsDefault,
            RequiresReferenceNumber = method.RequiresReferenceNumber,
            CostSharingMode = method.CostSharingMode,
            CostSharingScope = method.CostSharingScope,
            CostSharingCommissionPercentage = method.CostSharingCommissionPercentage,
            CostSharingRestaurantPercentage = method.CostSharingRestaurantPercentage,
            CostSharingCounterpartyPercentage = method.CostSharingCounterpartyPercentage,
            CreatedAt = method.CreatedAt,
            UpdatedAt = method.UpdatedAt
        };
    }
}
