using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>Pure, validated campaign lifecycle transitions. Single source of truth for both the admin API and the job.</summary>
    public static class CampaignStatusPolicy
    {
        public static bool CanTransition(CampaignStatus from, CampaignStatus to)
        {
            if (from == to) return false;
            return from switch
            {
                CampaignStatus.Draft => to is CampaignStatus.Scheduled or CampaignStatus.Active or CampaignStatus.Archived,
                CampaignStatus.Scheduled => to is CampaignStatus.Active or CampaignStatus.Paused or CampaignStatus.Expired or CampaignStatus.Archived,
                CampaignStatus.Active => to is CampaignStatus.Paused or CampaignStatus.Expired or CampaignStatus.Archived,
                CampaignStatus.Paused => to is CampaignStatus.Active or CampaignStatus.Expired or CampaignStatus.Archived,
                CampaignStatus.Expired => to is CampaignStatus.Archived,
                CampaignStatus.Archived => false,
                _ => false
            };
        }

        /// <summary>
        /// The status a campaign should hold given the clock — used by the processing job to auto-advance
        /// Scheduled→Active and Active/Scheduled→Expired. Paused/Archived/Draft are operator-controlled and
        /// never auto-advanced here.
        /// </summary>
        public static CampaignStatus? AutoAdvance(CampaignStatus current, DateTime startUtc, DateTime endUtc, DateTime nowUtc)
        {
            if (nowUtc >= endUtc && current is CampaignStatus.Active or CampaignStatus.Scheduled)
                return CampaignStatus.Expired;
            if (current == CampaignStatus.Scheduled && nowUtc >= startUtc && nowUtc < endUtc)
                return CampaignStatus.Active;
            return null;
        }

        /// <summary>Campaigns only issue rewards while genuinely live.</summary>
        public static bool IsAwarding(CampaignStatus status) => status == CampaignStatus.Active;
    }
}
