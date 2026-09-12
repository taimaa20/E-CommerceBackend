using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <summary>
    /// Pure, dependency-free wallet arithmetic. All point math lives here so it is deterministic
    /// and unit-testable without a database. The service layer handles persistence/concurrency.
    /// </summary>
    public static class WalletMath
    {
        public static decimal Round(decimal value)
            => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        /// <summary>Applies a credit to the wallet aggregates and returns the new available balance.</summary>
        public static decimal ApplyCredit(CustomerWallet wallet, decimal points, bool isPending)
        {
            if (points <= 0) throw new ValidationException("Credit points must be positive.");

            if (isPending)
            {
                wallet.PendingPoints = Round(wallet.PendingPoints + points);
            }
            else
            {
                wallet.AvailablePoints = Round(wallet.AvailablePoints + points);
                wallet.LifetimePoints = Round(wallet.LifetimePoints + points);
            }

            return wallet.AvailablePoints;
        }

        /// <summary>Applies a redemption to the wallet aggregates and returns the new available balance.</summary>
        public static decimal ApplyRedeem(CustomerWallet wallet, decimal points)
        {
            if (points <= 0) throw new ValidationException("Redeem points must be positive.");
            if (wallet.AvailablePoints < points) throw new ValidationException("Insufficient available points.");

            wallet.AvailablePoints = Round(wallet.AvailablePoints - points);
            wallet.RedeemedPoints = Round(wallet.RedeemedPoints + points);
            return wallet.AvailablePoints;
        }

        /// <summary>
        /// Distributes <paramref name="points"/> across active lots oldest-first (FIFO).
        /// Returns the per-lot deductions. Throws if the lots cannot cover the amount.
        /// </summary>
        public static IReadOnlyList<LotDeduction> AllocateFifo(
            IReadOnlyList<LotSnapshot> lots, decimal points)
        {
            if (points <= 0) throw new ValidationException("Redeem points must be positive.");

            var ordered = lots.Where(l => l.Remaining > 0)
                              .OrderBy(l => l.EarnedAt)
                              .ToList();

            var available = ordered.Sum(l => l.Remaining);
            if (available < points) throw new ValidationException("Insufficient points in lots.");

            var deductions = new List<LotDeduction>();
            var remaining = points;
            foreach (var lot in ordered)
            {
                if (remaining <= 0) break;
                var take = Math.Min(lot.Remaining, remaining);
                deductions.Add(new LotDeduction(lot.LotId, Round(take)));
                remaining = Round(remaining - take);
            }

            return deductions;
        }

        /// <summary>
        /// The points to reverse for a refund: gross earned × (refunded / orderTotal), capped at the
        /// full earned amount. Returns 0 when the order total is non-positive (nothing to prorate).
        /// </summary>
        public static decimal ProportionalReversal(decimal grossPoints, decimal refundedAmount, decimal orderTotal)
        {
            if (grossPoints <= 0 || refundedAmount <= 0 || orderTotal <= 0) return 0m;

            var fraction = refundedAmount / orderTotal;
            if (fraction > 1m) fraction = 1m;
            return Round(grossPoints * fraction);
        }

        /// <summary>
        /// The expiry to stamp on points transferred during a merge: the soonest concrete expiry
        /// among the source lots (a never-expiring lot does not extend the runway). Null only when
        /// every source lot never expires.
        /// </summary>
        public static DateTime? EarliestExpiry(IEnumerable<DateTime?> expiries)
        {
            DateTime? earliest = null;
            foreach (var expiry in expiries)
            {
                if (expiry is null) continue;
                if (earliest is null || expiry < earliest) earliest = expiry;
            }
            return earliest;
        }

        public readonly record struct LotSnapshot(Guid LotId, decimal Remaining, DateTime EarnedAt);
        public readonly record struct LotDeduction(Guid LotId, decimal Deducted);
    }
}
