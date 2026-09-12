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
    /// <inheritdoc cref="ITierAdminService"/>
    public class TierAdminService : ITierAdminService
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenant;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMarketingAuditLogger _audit;

        public TierAdminService(
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

        public async Task<List<LoyaltyTierDto>> GetAllAsync(bool includeInactive, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var tiers = await _context.LoyaltyTiers
                .Include(t => t.Benefits)
                .Where(t => t.TenantId == tenantId && (includeInactive || t.IsActive))
                .OrderBy(t => t.SortOrder).ThenBy(t => t.MinPoints)
                .ToListAsync(ct);
            return tiers.Select(MapToDto).ToList();
        }

        public async Task<LoyaltyTierDto> GetByIdAsync(Guid id, CancellationToken ct)
            => MapToDto(await LoadAsync(id, ct));

        public async Task<LoyaltyTierDto> CreateAsync(LoyaltyTierUpsertDto dto, CancellationToken ct)
        {
            Validate(dto);
            var tenantId = _tenant.GetTenantId();

            var tier = new LoyaltyTier
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = dto.Name,
                NameAr = dto.NameAr,
                LegacyTierEnum = dto.LegacyTierEnum,
                MinPoints = dto.MinPoints,
                MinSpend = dto.MinSpend,
                EarnMultiplier = dto.EarnMultiplier,
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive,
                Benefits = dto.Benefits.Select(b => BuildBenefit(b, tenantId)).ToList(),
                CreatedByUserId = _currentUser.UserIdOrNull
            };
            _context.LoyaltyTiers.Add(tier);

            _audit.Track(tenantId, null, MarketingAuditAction.Created,
                nameof(LoyaltyTier), tier.Id, null, $"name={tier.Name};multiplier={tier.EarnMultiplier}");

            await _context.SaveChangesAsync(ct);
            return MapToDto(tier);
        }

        public async Task<LoyaltyTierDto> UpdateAsync(Guid id, LoyaltyTierUpsertDto dto, CancellationToken ct)
        {
            Validate(dto);
            var tenantId = _tenant.GetTenantId();

            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var tier = await LoadAsync(id, ct);

            tier.Name = dto.Name;
            tier.NameAr = dto.NameAr;
            tier.LegacyTierEnum = dto.LegacyTierEnum;
            tier.MinPoints = dto.MinPoints;
            tier.MinSpend = dto.MinSpend;
            tier.EarnMultiplier = dto.EarnMultiplier;
            tier.SortOrder = dto.SortOrder;
            tier.IsActive = dto.IsActive;
            tier.UpdatedByUserId = _currentUser.UserIdOrNull;

            // Replace the benefit set wholesale — benefits are owned, value-style children of the tier.
            _context.TierBenefits.RemoveRange(tier.Benefits);
            foreach (var b in dto.Benefits)
                _context.TierBenefits.Add(BuildBenefit(b, tenantId, tier.Id));

            _audit.Track(tenantId, tier.BranchId, MarketingAuditAction.Updated,
                nameof(LoyaltyTier), tier.Id, null, $"name={tier.Name};multiplier={tier.EarnMultiplier};active={tier.IsActive}");

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return MapToDto(await LoadAsync(id, ct));
        }

        public async Task DeactivateAsync(Guid id, CancellationToken ct)
        {
            var tier = await LoadAsync(id, ct);
            tier.IsActive = false;
            tier.UpdatedByUserId = _currentUser.UserIdOrNull;

            _audit.Track(tier.TenantId, tier.BranchId, MarketingAuditAction.Deleted,
                nameof(LoyaltyTier), tier.Id, null, $"name={tier.Name}");

            await _context.SaveChangesAsync(ct);
        }

        private async Task<LoyaltyTier> LoadAsync(Guid id, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            return await _context.LoyaltyTiers
                .Include(t => t.Benefits)
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Loyalty tier was not found.");
        }

        private TierBenefit BuildBenefit(TierBenefitUpsertDto dto, Guid tenantId, Guid? tierId = null) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TierId = tierId ?? Guid.Empty,
            BenefitType = dto.BenefitType,
            Value = dto.Value,
            RefId = dto.RefId,
            Description = dto.Description,
            DescriptionAr = dto.DescriptionAr,
            CreatedByUserId = _currentUser.UserIdOrNull
        };

        private static void Validate(LoyaltyTierUpsertDto dto)
        {
            if (dto.EarnMultiplier < 0)
                throw new ValidationException("EarnMultiplier cannot be negative.");
            if (dto.MinPoints < 0 || dto.MinSpend < 0)
                throw new ValidationException("Tier thresholds cannot be negative.");
        }

        private static LoyaltyTierDto MapToDto(LoyaltyTier t) => new()
        {
            Id = t.Id,
            Name = t.Name,
            NameAr = t.NameAr,
            LegacyTierEnum = t.LegacyTierEnum,
            MinPoints = t.MinPoints,
            MinSpend = t.MinSpend,
            EarnMultiplier = t.EarnMultiplier,
            SortOrder = t.SortOrder,
            IsActive = t.IsActive,
            Benefits = t.Benefits
                .Select(b => new TierBenefitDto
                {
                    Id = b.Id,
                    BenefitType = b.BenefitType,
                    Value = b.Value,
                    RefId = b.RefId,
                    Description = b.Description,
                    DescriptionAr = b.DescriptionAr,
                    Enforced = TierBenefitPolicy.IsEnforced(b.BenefitType)
                })
                .ToList()
        };
    }
}
