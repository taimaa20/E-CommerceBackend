using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers
{
    public static class InventoryUnitConverter
    {
        public static decimal Convert(decimal quantity, string? fromUnit, Unit toUnit)
        {
            var sourceUnit = ParseUnit(fromUnit);
            if (!sourceUnit.HasValue && !string.IsNullOrWhiteSpace(fromUnit))
                throw new ValidationException($"Waste unit '{fromUnit}' is not supported.");

            var resolvedUnit = sourceUnit ?? toUnit;
            if (resolvedUnit == toUnit)
                return quantity;

            if (FamilyOf(resolvedUnit) != FamilyOf(toUnit))
                throw new ValidationException($"Cannot convert waste unit '{fromUnit}' to inventory unit '{toUnit}'.");

            return quantity * FactorToBase(resolvedUnit) / FactorToBase(toUnit);
        }

        private static Unit? ParseUnit(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim().ToLowerInvariant() switch
            {
                "g" or "gram" or "grams" => Unit.Gram,
                "kg" or "kilo" or "kilogram" or "kilograms" => Unit.Kilogram,
                "l" or "lt" or "liter" or "liters" or "litre" or "litres" => Unit.Litre,
                "ml" or "milliliter" or "milliliters" or "millilitre" or "millilitres" or "mililitre" or "mililitres" => Unit.Mililitre,
                "adet" or "piece" or "pieces" or "pc" or "pcs" or "item" or "items" or "unit" or "units" => Unit.Adet,
                _ => Enum.TryParse<Unit>(value, ignoreCase: true, out var parsed) ? parsed : null
            };
        }

        private static string FamilyOf(Unit unit)
            => unit switch
            {
                Unit.Gram or Unit.Kilogram => "Mass",
                Unit.Litre or Unit.Mililitre => "Volume",
                _ => "Count"
            };

        private static decimal FactorToBase(Unit unit)
            => unit switch
            {
                Unit.Kilogram => 1000m,
                Unit.Litre => 1000m,
                _ => 1m
            };
    }
}
