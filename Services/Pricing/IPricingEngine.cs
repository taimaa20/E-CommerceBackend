using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Pricing
{
    public interface IPricingEngine
    {
        PricingMetrics CalculateMetrics(decimal? costPrice, decimal basePrice);
        decimal CalculateBasePrice(decimal costPrice, string pricingMode, decimal pricingValue);
        decimal CalculateDiscountedPrice(decimal basePrice, decimal discountPercentage);
        void ApplySavedPricing(Product product, decimal? costPrice, decimal requestedBasePrice);
        void ApplyCostChange(Product product, decimal? costPrice);
    }

    public readonly record struct PricingMetrics(
        decimal ProfitAmount,
        decimal ProfitMargin,
        decimal ProfitMultiplier);
}
