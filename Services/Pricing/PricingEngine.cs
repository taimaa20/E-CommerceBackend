using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Pricing
{
    public sealed class PricingEngine : IPricingEngine
    {
        private const decimal MaximumMarginPercentage = 100m;

        public PricingMetrics CalculateMetrics(decimal? costPrice, decimal basePrice)
        {
            ValidateNonNegative(costPrice ?? 0m, "Cost price");
            ValidateNonNegative(basePrice, "Base price");

            var cost = costPrice ?? 0m;
            var profitAmount = RoundCurrency(basePrice - cost);
            var profitMargin = basePrice > 0m
                ? RoundMetric((profitAmount / basePrice) * 100m)
                : 0m;
            var profitMultiplier = cost > 0m
                ? RoundMetric(basePrice / cost)
                : 0m;

            return new PricingMetrics(profitAmount, profitMargin, profitMultiplier);
        }

        public decimal CalculateBasePrice(decimal costPrice, string pricingMode, decimal pricingValue)
        {
            ValidateNonNegative(costPrice, "Cost price");
            var mode = ProductPricingModes.Normalize(pricingMode);

            return mode switch
            {
                ProductPricingModes.Margin => CalculateMarginPrice(costPrice, pricingValue),
                ProductPricingModes.Multiplier => CalculateMultiplierPrice(costPrice, pricingValue),
                _ => throw new ValidationException("Automatic base price requires margin or multiplier mode.")
            };
        }

        public decimal CalculateDiscountedPrice(decimal basePrice, decimal discountPercentage)
        {
            ValidateNonNegative(basePrice, "Base price");
            if (discountPercentage < 0m || discountPercentage > 100m)
                throw new ValidationException("Discount percentage must be between 0 and 100.");

            return RoundCurrency(basePrice * (1m - (discountPercentage / 100m)));
        }

        public void ApplySavedPricing(Product product, decimal? costPrice, decimal requestedBasePrice)
        {
            product.CostPrice = NormalizeCost(costPrice);
            product.BasePrice = ResolveSavedBasePrice(product, requestedBasePrice);
            product.DiscountedPrice = product.DiscountPercentage > 0m
                ? CalculateDiscountedPrice(product.BasePrice, product.DiscountPercentage)
                : null;
        }

        public void ApplyCostChange(Product product, decimal? costPrice)
        {
            product.CostPrice = NormalizeCost(costPrice);
            if (!ProductPricingModes.IsAutomatic(product.PricingMode) || product.CostPrice is not > 0m)
                return;

            product.BasePrice = CalculateBasePrice(product.CostPrice.Value, product.PricingMode, product.Markup);
            product.DiscountedPrice = product.DiscountPercentage > 0m
                ? CalculateDiscountedPrice(product.BasePrice, product.DiscountPercentage)
                : null;
        }

        private decimal ResolveSavedBasePrice(Product product, decimal requestedBasePrice)
        {
            ValidateNonNegative(requestedBasePrice, "Base price");
            if (!ProductPricingModes.IsAutomatic(product.PricingMode))
                return RoundCurrency(requestedBasePrice);
            if (product.CostPrice is not > 0m)
                throw new ValidationException("Cost price must be greater than zero for automatic pricing.");

            return CalculateBasePrice(product.CostPrice.Value, product.PricingMode, product.Markup);
        }

        private static decimal CalculateMarginPrice(decimal costPrice, decimal marginPercentage)
        {
            if (marginPercentage < 0m || marginPercentage >= MaximumMarginPercentage)
                throw new ValidationException("Profit margin must be between 0 and less than 100%.");

            return RoundGeneratedBasePrice(costPrice / (1m - (marginPercentage / 100m)));
        }

        private static decimal CalculateMultiplierPrice(decimal costPrice, decimal multiplier)
        {
            if (multiplier <= 0m)
                throw new ValidationException("Profit multiplier must be greater than zero.");

            return RoundGeneratedBasePrice(costPrice * multiplier);
        }

        private static decimal? NormalizeCost(decimal? costPrice)
        {
            if (!costPrice.HasValue)
                return null;

            ValidateNonNegative(costPrice.Value, "Cost price");
            return RoundCurrency(costPrice.Value);
        }

        private static void ValidateNonNegative(decimal value, string fieldName)
        {
            if (value < 0m)
                throw new ValidationException($"{fieldName} cannot be negative.");
        }

        private static decimal RoundCurrency(decimal value)
        {
            return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal RoundMetric(decimal value)
        {
            return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
        }

        private static decimal RoundGeneratedBasePrice(decimal price)
        {
            if (price <= 0m)
                return 0m;

            var whole = Math.Ceiling(price);
            var remainder = (long)whole % 10;
            if (remainder == 0)
                return whole;

            return remainder <= 5
                ? whole + (5 - remainder)
                : whole + (10 - remainder);
        }
    }
}
