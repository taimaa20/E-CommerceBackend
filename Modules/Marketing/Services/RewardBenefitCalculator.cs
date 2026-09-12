using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <summary>
    /// Pure computation of the monetary discount a reward grants. Free-product / free-delivery
    /// rewards carry no monetary discount here (the cashier applies the item/fee benefit). Testable.
    /// </summary>
    public static class RewardBenefitCalculator
    {
        public static decimal DiscountAmount(RewardType type, decimal? rewardValue, decimal? maxDiscountAmount, decimal? orderTotal)
        {
            switch (type)
            {
                case RewardType.FixedDiscount:
                    return Round(Math.Max(0m, rewardValue ?? 0m));

                case RewardType.PercentageDiscount:
                    if (rewardValue is not > 0 || orderTotal is not > 0) return 0m;
                    var raw = orderTotal.Value * (rewardValue.Value / 100m);
                    if (maxDiscountAmount is > 0) raw = Math.Min(raw, maxDiscountAmount.Value);
                    return Round(Math.Max(0m, raw));

                default:
                    return 0m; // FreeProduct / FreeDelivery
            }
        }

        private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
