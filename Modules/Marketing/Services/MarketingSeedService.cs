using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <inheritdoc cref="IMarketingSeedService"/>
    public class MarketingSeedService : IMarketingSeedService
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenant;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMarketingAuditLogger _audit;

        public MarketingSeedService(
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

        public async Task<MarketingSeedResultDto> SeedLegacyParityAsync(CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var legacy = await _context.SystemSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

            var settingsCreated = await EnsureSettingsAsync(tenantId, legacy, ct);
            var tiersCreated = await EnsureTiersAsync(tenantId, legacy, ct);
            var ruleCreated = await EnsureBaseEarningRuleAsync(tenantId, ct);

            await _context.SaveChangesAsync(ct);

            _audit.Track(tenantId, null, MarketingAuditAction.Created, "MarketingSeed", null, null,
                $"settings={settingsCreated};tiers={tiersCreated};rule={ruleCreated}");
            await _context.SaveChangesAsync(ct);

            return new MarketingSeedResultDto
            {
                SettingsCreated = settingsCreated,
                TiersCreated = tiersCreated,
                EarningRuleCreated = ruleCreated,
                Message = settingsCreated || tiersCreated > 0 || ruleCreated
                    ? "Legacy-parity marketing defaults seeded. Review, then enable delegation to activate the engine."
                    : "Marketing defaults already present — nothing to seed."
            };
        }

        private async Task<bool> EnsureSettingsAsync(Guid tenantId, SystemSettings? legacy, CancellationToken ct)
        {
            if (await _context.MarketingSettings.AnyAsync(s => s.TenantId == tenantId, ct))
                return false;

            _context.MarketingSettings.Add(new MarketingSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BaseCurrencyCode = ResolveCurrency(legacy?.Currency),
                LoyaltyEnabled = legacy?.EnableLoyalty ?? true,
                EarnDelegationEnabled = false, // never auto-activate — admin flips this explicitly
                DefaultPointValue = legacy?.LoyaltyPointValue ?? 0m,
                DefaultExpirationMode = PointsExpirationMode.Never,
                MinRedeemPoints = legacy?.LoyaltyMinimumRedeemPoints,
                MaxRedeemPoints = legacy?.LoyaltyMaximumRedeemPoints,
                CreatedByUserId = _currentUser.UserIdOrNull
            });
            return true;
        }

        private async Task<int> EnsureTiersAsync(Guid tenantId, SystemSettings? legacy, CancellationToken ct)
        {
            var present = (await _context.LoyaltyTiers
                .Where(t => t.TenantId == tenantId)
                .Select(t => t.LegacyTierEnum)
                .ToListAsync(ct)).ToHashSet();

            var seeds = new (CustomerTier Tier, string Name, string NameAr, decimal MinPoints, int Sort)[]
            {
                (CustomerTier.Standard, "Standard", "عادي",   0m,                                          0),
                (CustomerTier.Bronze,   "Bronze",   "برونزي", legacy?.LoyaltyBronzeThreshold ?? 500m,      1),
                (CustomerTier.Silver,   "Silver",   "فضي",    legacy?.LoyaltySilverThreshold ?? 1500m,     2),
                (CustomerTier.Gold,     "Gold",     "ذهبي",   legacy?.LoyaltyGoldThreshold ?? 5000m,       3),
                (CustomerTier.VIP,      "VIP",      "مميز",   legacy?.LoyaltyVipThreshold ?? 10000m,       4),
            };

            var created = 0;
            foreach (var seed in seeds)
            {
                if (present.Contains(seed.Tier)) continue;

                _context.LoyaltyTiers.Add(new LoyaltyTier
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = seed.Name,
                    NameAr = seed.NameAr,
                    LegacyTierEnum = seed.Tier,
                    MinPoints = seed.MinPoints,
                    MinSpend = 0m,
                    EarnMultiplier = LegacyLoyaltyParity.TierMultiplier(seed.Tier),
                    SortOrder = seed.Sort,
                    IsActive = true,
                    CreatedByUserId = _currentUser.UserIdOrNull
                });
                created++;
            }
            return created;
        }

        private async Task<bool> EnsureBaseEarningRuleAsync(Guid tenantId, CancellationToken ct)
        {
            var hasPerAmount = await _context.EarningRules.AnyAsync(
                r => r.TenantId == tenantId && r.IsCurrent && r.RuleType == EarningRuleType.PerAmount, ct);
            if (hasPerAmount) return false;

            _context.EarningRules.Add(new EarningRule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RuleGroupId = Guid.NewGuid(),
                RuleVersion = 1,
                IsCurrent = true,
                EffectiveFrom = DateTime.UtcNow,
                Name = "Base points",
                NameAr = "نقاط أساسية",
                RuleType = EarningRuleType.PerAmount,
                PointsPerCurrencyUnit = LegacyLoyaltyParity.BasePointsPerCurrencyUnit,
                ApprovalMode = PointsApprovalMode.Immediate,
                ExpirationMode = PointsExpirationMode.Never,
                Priority = 100,
                IsActive = true,
                CreatedByUserId = _currentUser.UserIdOrNull
            });
            return true;
        }

        private static string ResolveCurrency(string? legacyCurrency)
            => !string.IsNullOrWhiteSpace(legacyCurrency) && legacyCurrency.Length >= 3
                ? legacyCurrency[..3].ToUpperInvariant()
                : "JOD";
    }
}
