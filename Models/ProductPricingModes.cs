namespace RestaurantPos.Api.Models
{
    public static class ProductPricingModes
    {
        public const string Manual = "manual";
        public const string Margin = "margin";
        public const string Multiplier = "multiplier";

        public static string Normalize(string? pricingMode)
        {
            return string.IsNullOrWhiteSpace(pricingMode)
                ? Manual
                : pricingMode.Trim().ToLowerInvariant();
        }

        public static bool IsSupported(string? pricingMode)
        {
            var normalized = Normalize(pricingMode);
            return normalized == Manual || normalized == Margin || normalized == Multiplier;
        }

        public static bool IsAutomatic(string? pricingMode)
        {
            var normalized = Normalize(pricingMode);
            return normalized == Margin || normalized == Multiplier;
        }
    }

    public static class ProductMarkupTypes
    {
        public const string Multiplier = "multiplier";
        public const string Percentage = "percentage";
        public const string Fixed = "fixed";
    }
}
