using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers
{
    public static class DeliveryPartnerPricingHelper
    {
        public static decimal CalculatePrice(
            decimal basePrice,
            DeliveryPartnerPricingRuleType rule,
            decimal value,
            decimal? customPrice)
        {
            var result = rule switch
            {
                DeliveryPartnerPricingRuleType.PercentageIncrease => basePrice + (basePrice * value / 100m),
                DeliveryPartnerPricingRuleType.PercentageDiscount => basePrice - (basePrice * value / 100m),
                DeliveryPartnerPricingRuleType.FixedIncrease => basePrice + value,
                DeliveryPartnerPricingRuleType.FixedDiscount => basePrice - value,
                DeliveryPartnerPricingRuleType.CustomPrice => customPrice ?? basePrice,
                _ => basePrice
            };

            if (result <= 0m)
                return 0m;

            return rule == DeliveryPartnerPricingRuleType.CustomPrice
                ? result
                : decimal.Round(result, 2, MidpointRounding.AwayFromZero);
        }
    }
}
