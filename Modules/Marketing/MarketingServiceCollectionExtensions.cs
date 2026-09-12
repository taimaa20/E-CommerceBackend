using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Modules.Marketing.Jobs;
using RestaurantPos.Api.Modules.Marketing.Services;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing
{
    /// <summary>
    /// Single DI entry point for the Marketing module. Implementations are wired per sprint:
    ///   Sprint 2: IWalletService, IMarketingAuditLogger
    ///   Sprint 3: IEarningEngine  (this commit)
    ///   Sprint 4: ICustomerMergeService, event handlers
    /// </summary>
    public static class MarketingServiceCollectionExtensions
    {
        public static IServiceCollection AddMarketingEngine(this IServiceCollection services)
        {
            services.AddScoped<IMarketingAuditLogger, MarketingAuditLogger>();
            services.AddScoped<IWalletService, WalletService>();
            services.AddScoped<IEarningEngine, EarningEngine>();

            // Sprint 5: read/admin query + CRUD services behind the versioned API.
            services.AddScoped<IWalletQueryService, WalletQueryService>();
            services.AddScoped<IEarningRuleService, EarningRuleService>();
            services.AddScoped<IMarketingSettingsService, MarketingSettingsService>();
            services.AddScoped<IMarketingSeedService, MarketingSeedService>();
            services.AddScoped<ICustomerMergeService, CustomerMergeService>();

            // Phase 1: loyalty tier administration.
            services.AddScoped<ITierAdminService, TierAdminService>();

            // Phase 2: operations & automation (dashboard, segments, dashboards, job services).
            services.AddScoped<ILoyaltyDashboardService, LoyaltyDashboardService>();
            services.AddScoped<ISegmentService, SegmentService>();
            services.AddScoped<ISegmentComputationEngine, SegmentComputationEngine>();
            services.AddScoped<ILoyaltyExpirationJobService, LoyaltyExpirationJobService>();
            services.AddScoped<ITierRecalculationJobService, TierRecalculationJobService>();
            services.AddScoped<ISegmentComputationJobService, SegmentComputationJobService>();

            // Phase 3 (core hardening): pending approval, distributed locking, job schedule, tier benefits.
            services.AddScoped<IPendingApprovalJobService, PendingApprovalJobService>();
            services.AddScoped<IAdvisoryLockService, AdvisoryLockService>();
            services.AddScoped<ITierBenefitProvider, TierBenefitProvider>();
            services.AddSingleton<ILoyaltyJobSchedule, LoyaltyJobSchedule>();

            // Phase 3.1: campaign management (consumes wallet engine via the processing job).
            services.AddScoped<ICampaignService, CampaignService>();
            services.AddScoped<ICampaignProcessingJobService, CampaignProcessingJobService>();

            // Phase 3.2: simple rewards redemption (consumes wallet engine via RedeemAsync).
            services.AddScoped<IRewardService, RewardService>();
            return services;
        }
    }
}
