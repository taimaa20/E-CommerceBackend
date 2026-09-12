using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <inheritdoc cref="IMarketingSettingsService"/>
    public class MarketingSettingsService : IMarketingSettingsService
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenant;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMarketingAuditLogger _audit;

        public MarketingSettingsService(
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

        public async Task<MarketingSettingsDto> GetAsync(CancellationToken ct)
            => MapToDto(await GetOrCreateAsync(ct));

        public async Task<MarketingSettingsDto> UpdateAsync(MarketingSettingsUpdateDto dto, CancellationToken ct)
        {
            var settings = await GetOrCreateAsync(ct);
            var beforeJson = System.Text.Json.JsonSerializer.Serialize(MapToDto(settings));

            settings.BaseCurrencyCode = dto.BaseCurrencyCode;
            settings.LoyaltyEnabled = dto.LoyaltyEnabled;
            settings.RewardsEnabled = dto.RewardsEnabled;
            settings.PromosEnabled = dto.PromosEnabled;
            settings.VouchersEnabled = dto.VouchersEnabled;
            settings.ReferralsEnabled = dto.ReferralsEnabled;
            settings.InfluencersEnabled = dto.InfluencersEnabled;
            settings.CampaignsEnabled = dto.CampaignsEnabled;
            settings.DefaultPointValue = dto.DefaultPointValue;
            settings.DefaultExpirationMode = dto.DefaultExpirationMode;
            settings.DefaultExpirationValue = dto.DefaultExpirationValue;
            settings.MinRedeemPoints = dto.MinRedeemPoints;
            settings.MaxRedeemPoints = dto.MaxRedeemPoints;
            settings.MaxStackedDiscount = dto.MaxStackedDiscount;
            settings.EarnDelegationEnabled = dto.EarnDelegationEnabled;
            settings.TierDemotionEnabled = dto.TierDemotionEnabled;
            settings.UpdatedByUserId = _currentUser.UserIdOrNull;

            var afterDto = MapToDto(settings);
            _audit.Track(settings.TenantId, null, MarketingAuditAction.Updated,
                nameof(MarketingSettings), settings.Id, null,
                $"delegation={dto.EarnDelegationEnabled};loyalty={dto.LoyaltyEnabled}",
                beforeJson: beforeJson,
                afterJson: System.Text.Json.JsonSerializer.Serialize(afterDto));

            await _context.SaveChangesAsync(ct);
            return afterDto;
        }

        private async Task<MarketingSettings> GetOrCreateAsync(CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var settings = await _context.MarketingSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
            if (settings is not null) return settings;

            // Safe defaults: delegation OFF so the legacy earn path stays authoritative until an
            // admin both configures earning rules and explicitly enables delegation (no regression).
            settings = new MarketingSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BaseCurrencyCode = "JOD",
                LoyaltyEnabled = true,
                EarnDelegationEnabled = false,
                DefaultExpirationMode = PointsExpirationMode.Never,
                CreatedByUserId = _currentUser.UserIdOrNull
            };
            _context.MarketingSettings.Add(settings);
            await _context.SaveChangesAsync(ct);
            return settings;
        }

        private static MarketingSettingsDto MapToDto(MarketingSettings s) => new()
        {
            BaseCurrencyCode = s.BaseCurrencyCode,
            LoyaltyEnabled = s.LoyaltyEnabled,
            RewardsEnabled = s.RewardsEnabled,
            PromosEnabled = s.PromosEnabled,
            VouchersEnabled = s.VouchersEnabled,
            ReferralsEnabled = s.ReferralsEnabled,
            InfluencersEnabled = s.InfluencersEnabled,
            CampaignsEnabled = s.CampaignsEnabled,
            DefaultPointValue = s.DefaultPointValue,
            DefaultExpirationMode = s.DefaultExpirationMode,
            DefaultExpirationValue = s.DefaultExpirationValue,
            MinRedeemPoints = s.MinRedeemPoints,
            MaxRedeemPoints = s.MaxRedeemPoints,
            MaxStackedDiscount = s.MaxStackedDiscount,
            EarnDelegationEnabled = s.EarnDelegationEnabled,
            TierDemotionEnabled = s.TierDemotionEnabled
        };
    }
}
