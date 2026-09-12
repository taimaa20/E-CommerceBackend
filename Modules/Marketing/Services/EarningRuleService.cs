using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <inheritdoc cref="IEarningRuleService"/>
    public class EarningRuleService : IEarningRuleService
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenant;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMarketingAuditLogger _audit;

        public EarningRuleService(
            PosDbContext context,
            ITenantResolver tenant,
            ICurrentUserAccessor currentUser,
            IMarketingAuditLogger audit)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public async Task<List<EarningRuleDto>> GetCurrentAsync(bool includeInactive, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var rules = await _context.EarningRules
                .Where(r => r.TenantId == tenantId && r.IsCurrent && (includeInactive || r.IsActive))
                .OrderBy(r => r.Priority).ThenBy(r => r.Name)
                .ToListAsync(ct);
            return rules.Select(MapToDto).ToList();
        }

        public async Task<List<EarningRuleDto>> GetVersionsAsync(Guid ruleGroupId, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var rules = await _context.EarningRules.IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && r.RuleGroupId == ruleGroupId)
                .OrderByDescending(r => r.RuleVersion)
                .ToListAsync(ct);
            if (rules.Count == 0) throw new NotFoundException("Earning rule was not found.");
            return rules.Select(MapToDto).ToList();
        }

        public async Task<EarningRuleDto> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var rule = await _context.EarningRules
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Earning rule was not found.");
            return MapToDto(rule);
        }

        public async Task<EarningRuleDto> CreateAsync(EarningRuleCreateDto dto, CancellationToken ct)
        {
            ValidateRule(dto);
            var tenantId = _tenant.GetTenantId();

            var rule = BuildVersion(dto, tenantId, Guid.NewGuid(), version: 1);
            _context.EarningRules.Add(rule);

            _audit.Track(tenantId, dto.BranchId, MarketingAuditAction.Created,
                nameof(EarningRule), rule.Id, null, $"group={rule.RuleGroupId};v1");

            await _context.SaveChangesAsync(ct);
            return MapToDto(rule);
        }

        public async Task<EarningRuleDto> UpdateAsync(Guid ruleGroupId, EarningRuleCreateDto dto, CancellationToken ct)
        {
            ValidateRule(dto);
            var tenantId = _tenant.GetTenantId();

            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var current = await _context.EarningRules
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.RuleGroupId == ruleGroupId && r.IsCurrent, ct)
                ?? throw new NotFoundException("Earning rule was not found.");

            // Retire the current version first (immediate unique index forbids two current rows).
            current.IsCurrent = false;
            current.EffectiveTo = DateTime.UtcNow;
            current.UpdatedByUserId = _currentUser.UserIdOrNull;
            await _context.SaveChangesAsync(ct);

            var next = BuildVersion(dto, tenantId, ruleGroupId, current.RuleVersion + 1);
            _context.EarningRules.Add(next);

            _audit.Track(tenantId, dto.BranchId, MarketingAuditAction.RuleVersioned,
                nameof(EarningRule), next.Id, null, $"group={ruleGroupId};v{next.RuleVersion}");

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return MapToDto(next);
        }

        public async Task DeactivateAsync(Guid ruleGroupId, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var current = await _context.EarningRules
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.RuleGroupId == ruleGroupId && r.IsCurrent, ct)
                ?? throw new NotFoundException("Earning rule was not found.");

            current.IsActive = false;
            current.UpdatedByUserId = _currentUser.UserIdOrNull;

            _audit.Track(tenantId, current.BranchId, MarketingAuditAction.Deleted,
                nameof(EarningRule), current.Id, null, $"group={ruleGroupId}");

            await _context.SaveChangesAsync(ct);
        }

        private EarningRule BuildVersion(EarningRuleCreateDto dto, Guid tenantId, Guid ruleGroupId, int version) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = dto.BranchId,
            RuleGroupId = ruleGroupId,
            RuleVersion = version,
            IsCurrent = true,
            EffectiveFrom = DateTime.UtcNow,
            Name = dto.Name,
            NameAr = dto.NameAr,
            RuleType = dto.RuleType,
            PointsValue = dto.PointsValue,
            PointsPerCurrencyUnit = dto.PointsPerCurrencyUnit,
            Multiplier = dto.Multiplier,
            TargetCategoryId = dto.TargetCategoryId,
            TargetProductId = dto.TargetProductId,
            ChannelScope = dto.ChannelScope,
            ApprovalMode = dto.ApprovalMode,
            ApprovalDelayDays = dto.ApprovalDelayDays,
            ExpirationMode = dto.ExpirationMode,
            ExpirationValue = dto.ExpirationValue,
            Priority = dto.Priority,
            IsActive = dto.IsActive,
            CreatedByUserId = _currentUser.UserIdOrNull
        };

        private static void ValidateRule(EarningRuleCreateDto dto)
        {
            switch (dto.RuleType)
            {
                case EarningRuleType.PerAmount when dto.PointsPerCurrencyUnit is not > 0:
                    throw new ValidationException("PerAmount rules require a positive PointsPerCurrencyUnit.");
                case EarningRuleType.FixedPerOrder or EarningRuleType.FirstOrder or EarningRuleType.Birthday
                    when dto.PointsValue is not > 0:
                    throw new ValidationException("This rule type requires a positive PointsValue.");
                case EarningRuleType.PerCategory when dto.TargetCategoryId is null:
                    throw new ValidationException("PerCategory rules require a TargetCategoryId.");
                case EarningRuleType.PerProduct when dto.TargetProductId is null:
                    throw new ValidationException("PerProduct rules require a TargetProductId.");
                case EarningRuleType.ChannelBonus when dto.ChannelScope is null:
                    throw new ValidationException("ChannelBonus rules require a ChannelScope.");
            }

            if (dto.ApprovalMode == PointsApprovalMode.Delayed && dto.ApprovalDelayDays is not > 0)
                throw new ValidationException("Delayed approval requires ApprovalDelayDays > 0.");
            if (dto.ExpirationMode != PointsExpirationMode.Never && dto.ExpirationValue is not > 0)
                throw new ValidationException("A non-Never expiration requires ExpirationValue > 0.");
        }

        private static EarningRuleDto MapToDto(EarningRule r) => new()
        {
            Id = r.Id,
            RuleGroupId = r.RuleGroupId,
            RuleVersion = r.RuleVersion,
            IsCurrent = r.IsCurrent,
            IsActive = r.IsActive,
            BranchId = r.BranchId,
            Name = r.Name,
            NameAr = r.NameAr,
            RuleType = r.RuleType,
            PointsValue = r.PointsValue,
            PointsPerCurrencyUnit = r.PointsPerCurrencyUnit,
            Multiplier = r.Multiplier,
            TargetCategoryId = r.TargetCategoryId,
            TargetProductId = r.TargetProductId,
            ChannelScope = r.ChannelScope,
            ApprovalMode = r.ApprovalMode,
            ApprovalDelayDays = r.ApprovalDelayDays,
            ExpirationMode = r.ExpirationMode,
            ExpirationValue = r.ExpirationValue,
            Priority = r.Priority,
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo,
            CreatedAt = r.CreatedAt
        };
    }
}
