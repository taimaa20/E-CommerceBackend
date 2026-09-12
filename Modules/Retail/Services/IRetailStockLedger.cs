using RestaurantPos.Api.Modules.Retail.Domain;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <summary>
    /// A movement to post. Quantity is an absolute magnitude — the ledger derives the sign
    /// from <paramref name="MovementType"/>, so a caller cannot post a sale that adds stock.
    /// </summary>
    public sealed record RetailStockPosting(
        Guid ProductId,
        Guid BranchId,
        RetailStockMovementType MovementType,
        decimal Quantity,
        decimal? UnitCost,
        RetailStockSourceDocument SourceDocumentType,
        Guid? SourceDocumentId,
        Guid? SourceLineId,
        string? SourceReference,
        DateTime OccurredAtUtc,
        Guid? PerformedByUserId = null,
        string? PerformedBySystem = null,
        Guid? ReversesMovementId = null,
        string? Notes = null);

    /// <summary>
    /// A product's ledger position: everything that came in, everything that went out, and the
    /// balance. Lifetime receipts drive the reorder level, so they are reported separately
    /// rather than being recoverable only from the net figure.
    /// </summary>
    public readonly record struct RetailStockPosition(decimal TotalIn, decimal TotalOut, decimal OnHand);

    /// <summary>
    /// The ONLY writer of retail stock movements. Nothing else in the codebase may add,
    /// change or delete a row in the ledger — that is what makes "stock on hand is the sum of
    /// its movements" true by construction rather than by convention.
    /// </summary>
    public interface IRetailStockLedger
    {
        /// <summary>
        /// Posts one movement, exactly once. A posting that carries a source line is keyed on
        /// (movement type, source line): a replay returns the movement already recorded and
        /// writes nothing. Returns null only when the quantity is zero, which is not a movement.
        /// </summary>
        Task<RetailStockMovement?> PostAsync(RetailStockPosting posting, CancellationToken ct = default);

        /// <summary>Posts a set of movements under one save. Same exactly-once rule per row.</summary>
        Task<IReadOnlyList<RetailStockMovement>> PostManyAsync(
            IReadOnlyList<RetailStockPosting> postings,
            CancellationToken ct = default);

        /// <summary>
        /// Stock on hand per product for a branch. Every <b>tracked</b> product in the set is
        /// present, reporting 0 when it has no movements on this branch; products that are not
        /// stocked as finished goods are absent. Callers rely on that difference to tell
        /// "none left" from "not stocked here" - reporting the two the same way would let an
        /// untracked-looking product be ordered without limit.
        /// </summary>
        Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(
            Guid branchId,
            IReadOnlyCollection<Guid> productIds,
            CancellationToken ct = default);

        /// <summary>Stock on hand for every tracked product in a branch.</summary>
        Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(Guid branchId, CancellationToken ct = default);

        /// <summary>Stock on hand for a product across every branch. Used by the catalogue,
        /// which is not branch-scoped.</summary>
        Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAllBranchesAsync(
            IReadOnlyCollection<Guid> productIds,
            CancellationToken ct = default);

        /// <summary>
        /// Full position per product. Pass a branch to scope it, or null for every branch —
        /// the catalogue is not branch-scoped, the Inventory screen is.
        /// </summary>
        Task<IReadOnlyDictionary<Guid, RetailStockPosition>> GetPositionsAsync(
            IReadOnlyCollection<Guid> productIds,
            Guid? branchId,
            CancellationToken ct = default);

        /// <summary>Movements for a product, newest first, for the audit trail.</summary>
        Task<IReadOnlyList<RetailStockMovement>> GetMovementsAsync(
            Guid productId,
            Guid? branchId,
            int limit,
            CancellationToken ct = default);

        /// <summary>Movements already recorded against one source document, of any type.</summary>
        Task<IReadOnlyList<RetailStockMovement>> GetBySourceDocumentAsync(
            RetailStockSourceDocument documentType,
            Guid documentId,
            CancellationToken ct = default);
    }
}
