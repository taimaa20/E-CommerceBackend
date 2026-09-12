using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers
{
    public static class CostSharingHelper
    {
        public const decimal FullSharePercentage = 100m;
        public const decimal ZeroSharePercentage = 0m;

        public static CostSharingCalculation Calculate(decimal grossAmount, CostSharingRuleSnapshot rule)
        {
            var normalizedGross = RoundCurrency(Math.Max(0m, grossAmount));
            var commission = RoundCurrency(normalizedGross * NormalizePercentage(rule.TotalCommissionPercentage) / 100m);
            var percentages = NormalizePercentages(
                rule.Mode,
                rule.RestaurantPercentage,
                rule.CounterpartyPercentage);

            var restaurantShare = RoundCurrency(commission * percentages.RestaurantPercentage / 100m);
            var counterpartyShare = RoundCurrency(commission - restaurantShare);

            return new CostSharingCalculation(
                rule with
                {
                    TotalCommissionPercentage = NormalizePercentage(rule.TotalCommissionPercentage),
                    RestaurantPercentage = percentages.RestaurantPercentage,
                    CounterpartyPercentage = percentages.CounterpartyPercentage
                },
                normalizedGross,
                commission,
                restaurantShare,
                counterpartyShare);
        }

        public static CostSharingPercentages NormalizePercentages(
            CostSharingMode mode,
            decimal restaurantPercentage,
            decimal counterpartyPercentage)
        {
            return mode switch
            {
                CostSharingMode.CounterpartyBearsAll => new CostSharingPercentages(ZeroSharePercentage, FullSharePercentage),
                CostSharingMode.Shared => new CostSharingPercentages(
                    NormalizePercentage(restaurantPercentage),
                    NormalizePercentage(counterpartyPercentage)),
                _ => new CostSharingPercentages(FullSharePercentage, ZeroSharePercentage)
            };
        }

        public static bool SharedPercentagesTotalOneHundred(decimal restaurantPercentage, decimal counterpartyPercentage)
            => NormalizePercentage(restaurantPercentage) + NormalizePercentage(counterpartyPercentage) == FullSharePercentage;

        public static decimal NormalizePercentage(decimal percentage)
            => Math.Round(Math.Clamp(percentage, 0m, FullSharePercentage), 2, MidpointRounding.AwayFromZero);

        public static decimal RoundCurrency(decimal amount)
            => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
