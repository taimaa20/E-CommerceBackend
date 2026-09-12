using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>
    /// Pure tier resolution: the highest tier whose point AND spend thresholds a wallet meets.
    /// Promotions always apply; demotions only when the tenant enabled them — otherwise the current
    /// (higher) tier sticks. Returns null when no change is warranted. Fully testable, no EF.
    /// </summary>
    public static class TierResolver
    {
        public static LoyaltyTier? Resolve(
            decimal lifetimePoints,
            decimal lifetimeSpend,
            Guid? currentTierId,
            IReadOnlyList<LoyaltyTier> tiers,
            bool demotionEnabled)
        {
            var qualifying =
                tiers.Where(t => lifetimePoints >= t.MinPoints && lifetimeSpend >= t.MinSpend)
                     .OrderByDescending(t => t.SortOrder)
                     .FirstOrDefault()
                ?? tiers.OrderBy(t => t.SortOrder).FirstOrDefault();

            if (qualifying is null) return null;

            var current = tiers.FirstOrDefault(t => t.Id == currentTierId);
            if (current is null) return qualifying;                          // unassigned → set
            if (qualifying.SortOrder > current.SortOrder) return qualifying; // promotion
            if (qualifying.SortOrder < current.SortOrder) return demotionEnabled ? qualifying : null;
            return null;                                                     // already correct
        }
    }
}
