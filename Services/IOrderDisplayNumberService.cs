using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Generates the configured human-friendly DisplayOrderNumber ("TA-1",
    /// "TA-202606-1", etc.) using the selected display options and sequence scope:
    ///   TA – Takeaway        DI – Dine In
    ///   DP – Delivery Partner (incl. Talabat / partner-routed delivery)
    ///   QR – QR / Customer Mobile orders      ON – Online Shopping
    /// Concurrency safety is enforced by the database (atomic upsert + increment
    /// against OrderDisplaySequences); there is no in-memory counter.
    /// </summary>
    public interface IOrderDisplayNumberService
    {
        /// <summary>
        /// Reserves and returns the next display number for the given channel /
        /// configured reset bucket. Must be called inside the same transaction that
        /// persists the Order so the row-level lock on the counter row is held
        /// for the lifetime of the order create.
        /// </summary>
        Task<string> GenerateAsync(
            Guid tenantId,
            OrderType orderType,
            OrderSource orderSource,
            bool hasDeliveryPartner,
            DateTime utcNow,
            CancellationToken ct,
            string? partnerCode = null,
            bool isOffline = false);

        /// <summary>
        /// Returns the channel code that GenerateAsync would use, without
        /// touching the database. Useful for tests and for callers that need to
        /// branch behaviour off the same classification.
        /// </summary>
        string ResolveChannelCode(OrderType orderType, OrderSource orderSource, bool hasDeliveryPartner);
    }
}
