using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <summary>
    /// The configuration that reproduces the legacy <c>CustomerService.AddLoyaltyPointsAsync</c>
    /// behavior (points = orderTotal × tierRate) under the new rules engine, so enabling earn
    /// delegation causes no regression. Used by the seeder (later) and the golden-parity test.
    ///
    /// Legacy rate ≡ <see cref="BasePointsPerCurrencyUnit"/> × <see cref="TierMultiplier"/>:
    ///   Standard 1% = 0.01 × 1 · Bronze 3% = 0.01 × 3 · Silver 5% = 0.01 × 5
    ///   Gold 10% = 0.01 × 10 · VIP 15% = 0.01 × 15
    /// </summary>
    public static class LegacyLoyaltyParity
    {
        public const decimal BasePointsPerCurrencyUnit = 0.01m;

        public static decimal TierMultiplier(CustomerTier tier) => tier switch
        {
            CustomerTier.Bronze => 3m,
            CustomerTier.Silver => 5m,
            CustomerTier.Gold => 10m,
            CustomerTier.VIP => 15m,
            _ => 1m
        };

        /// <summary>The effective legacy earn rate per currency unit, for reference/verification.</summary>
        public static decimal LegacyRate(CustomerTier tier) => tier switch
        {
            CustomerTier.Bronze => 0.03m,
            CustomerTier.Silver => 0.05m,
            CustomerTier.Gold => 0.10m,
            CustomerTier.VIP => 0.15m,
            _ => 0.01m
        };
    }
}
