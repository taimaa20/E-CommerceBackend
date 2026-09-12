namespace RestaurantPos.Api.Helpers
{
    /// <summary>One line of a bundle offer as it enters the allocation.</summary>
    public sealed record OfferAllocationLine(Guid ProductId, int Quantity, decimal BaseUnitPrice);

    /// <summary>The unit price that line must be charged at once the offer applies.</summary>
    public sealed record OfferAllocationResult(Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);

    /// <summary>
    /// Spreads a bundle offer's total across its lines in proportion to what those lines
    /// would have cost at their normal effective price, giving the last line the remainder
    /// so the parts always add back up to the offer total exactly.
    ///
    /// This is the ONE allocation rule. Order intake charges these numbers and the public
    /// storefront displays them; both call this method so a shopper can never be shown a
    /// line price the order would not honour.
    /// </summary>
    public static class OfferPricingHelper
    {
        public static IReadOnlyList<OfferAllocationResult> Allocate(
            IReadOnlyList<OfferAllocationLine> lines,
            decimal offerTotal)
        {
            if (lines.Count == 0) return [];

            var originalTotal = lines.Sum(line => line.BaseUnitPrice * line.Quantity);
            if (originalTotal <= 0) return [];

            var results = new List<OfferAllocationResult>(lines.Count);
            var runningTotal = 0m;

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var lineBaseTotal = line.BaseUnitPrice * line.Quantity;
                var lineTotal = index == lines.Count - 1
                    ? offerTotal - runningTotal
                    : Math.Round(lineBaseTotal / originalTotal * offerTotal, 2, MidpointRounding.AwayFromZero);

                runningTotal += lineTotal;
                results.Add(new OfferAllocationResult(
                    line.ProductId,
                    line.Quantity,
                    line.Quantity > 0 ? Math.Round(lineTotal / line.Quantity, 2, MidpointRounding.AwayFromZero) : 0m,
                    lineTotal));
            }

            return results;
        }
    }
}
