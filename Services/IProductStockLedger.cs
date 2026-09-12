namespace RestaurantPos.Api.Services
{
    /// <summary>One requested line, used for pre-order stock validation.</summary>
    public readonly record struct ProductStockRequest(Guid ProductId, decimal Quantity);

    /// <summary>
    /// Stock for products that are stocked as finished goods rather than assembled from a
    /// recipe. The restaurant path is unaffected: a product with a recipe (or with no
    /// finished-goods stock record at all) is not tracked here and every method is a no-op
    /// for it, returning null / an empty result.
    ///
    /// This is the seam the Retail module plugs into, so no caller ever has to ask "is this
    /// the retail branch?". Whether a product is ledger-tracked is a property of the product,
    /// not of the branch or of a flag on the request.
    /// </summary>
    public interface IProductStockLedger
    {
        /// <summary>
        /// Validates a prospective order against available stock, at the boundary and before
        /// anything is written. Returns null when every line can be met (including lines for
        /// untracked products); otherwise a display-ready message naming the short items.
        /// </summary>
        Task<string?> ValidateAvailabilityAsync(
            Guid branchId,
            IReadOnlyList<ProductStockRequest> lines,
            bool isArabic,
            CancellationToken ct = default);

        /// <summary>
        /// Stock on hand for the tracked products in the set. Untracked products are absent
        /// from the result rather than reported as zero, so callers can tell "not tracked"
        /// from "none left".
        /// </summary>
        Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(
            Guid branchId,
            IReadOnlyCollection<Guid> productIds,
            CancellationToken ct = default);

        /// <summary>
        /// Brings one order line's ledger position in line with the order's current state, and
        /// returns the line's cost of goods — or null when the product is not tracked here.
        /// Safe to call repeatedly: the desired state is derived from the order, so a replay
        /// posts nothing.
        /// </summary>
        Task<decimal?> SyncOrderItemAsync(Guid orderId, Guid orderItemId, CancellationToken ct = default);

        /// <summary>
        /// Same reconciliation across every line of an order, including lines that were removed
        /// from it. This is what payment, cancellation and refund call — they state that the
        /// order changed, not what the stock effect should be.
        /// </summary>
        Task SyncOrderAsync(Guid orderId, CancellationToken ct = default);
    }
}
