using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>
    /// Pure computation of campaign bonus points. The job feeds these results into the existing
    /// <c>IWalletService.CreditAsync</c> — this class never touches the wallet or the ledger.
    /// </summary>
    public static class CampaignAwardCalculator
    {
        /// <summary>
        /// Bonus for a single qualifying order (BonusPoints &amp; Multiplier types). FixedReward is
        /// per-customer (see <see cref="ThresholdReward"/>) and returns 0 here.
        /// </summary>
        public static decimal PerOrderBonus(
            CampaignType type,
            decimal orderSubtotal,
            decimal? bonusPoints,
            decimal? spendThreshold,
            decimal? multiplier,
            decimal? pointsPerCurrencyUnit)
        {
            switch (type)
            {
                case CampaignType.BonusPoints:
                    if (bonusPoints is not > 0) return 0m;
                    return orderSubtotal >= (spendThreshold ?? 0m) ? Round(bonusPoints.Value) : 0m;

                case CampaignType.Multiplier:
                    if (multiplier is not > 1m || pointsPerCurrencyUnit is not > 0 || orderSubtotal <= 0) return 0m;
                    // Bonus = the EXTRA points beyond the base rate (×2 ⇒ +1× extra).
                    return Round(orderSubtotal * pointsPerCurrencyUnit.Value * (multiplier.Value - 1m));

                default:
                    return 0m;
            }
        }

        /// <summary>
        /// The effective per-currency-unit BONUS rate of a multiplier campaign:
        /// <c>PointsPerCurrencyUnit × (Multiplier − 1)</c>. This is the extra points granted per unit
        /// of spend at the campaign's own rate (Option B: a "campaign bonus multiplier", surfaced for
        /// reporting/UI) — it is NOT a multiple of the order's actually-earned points. Returns 0 when
        /// the campaign is not a valid multiplier configuration.
        /// </summary>
        public static decimal EffectiveMultiplierRate(decimal? multiplier, decimal? pointsPerCurrencyUnit)
        {
            if (multiplier is not > 1m || pointsPerCurrencyUnit is not > 0) return 0m;
            return Round(pointsPerCurrencyUnit.Value * (multiplier.Value - 1m));
        }

        /// <summary>Reward for FixedReward campaigns once a customer reaches the order-count threshold.</summary>
        public static decimal ThresholdReward(
            CampaignType type, int qualifyingOrderCount, int? orderCountThreshold, decimal? bonusPoints)
        {
            if (type != CampaignType.FixedReward) return 0m;
            if (bonusPoints is not > 0 || orderCountThreshold is not > 0) return 0m;
            return qualifyingOrderCount >= orderCountThreshold.Value ? Round(bonusPoints.Value) : 0m;
        }

        private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
