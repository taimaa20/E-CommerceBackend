using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>
    /// Canonical, pure predicate for "is this lot due to expire". <c>WalletService.ExpireDueLotsAsync</c>
    /// applies the same condition server-side; this mirror keeps the rule documented and unit-testable.
    /// </summary>
    public static class ExpirationPolicy
    {
        public static bool IsDue(PointsLotStatus status, decimal remainingPoints, DateTime? expiresAt, DateTime asOfUtc)
            => status == PointsLotStatus.Active
               && remainingPoints > 0
               && expiresAt is { } e
               && e <= asOfUtc;
    }
}
