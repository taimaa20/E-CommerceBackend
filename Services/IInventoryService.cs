using System;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public interface IInventoryService
    {
        /// <summary>
        /// Process stock deduction for the entire order (legacy fallback for bulk-ready).
        /// Only processes items that haven't been individually processed yet.
        /// </summary>
        Task ProcessOrderStockAsync(Guid orderId);

        /// <summary>
        /// Process stock deduction for a single order item immediately when it becomes Ready.
        /// Uses FIFO (First-In-First-Out, by batch CreatedAt). Transaction-safe and idempotent.
        /// Captures the resolved cost into OrderItem.StockDeductedCost. NEVER mutates selling price.
        /// </summary>
        Task<decimal> ProcessOrderItemStockAsync(Guid orderId, Guid orderItemId);

        /// <summary>
        /// Deduct stock from a raw material using FIFO. Used by manual waste logging.
        /// Respects an existing ambient transaction when one is already open.
        /// </summary>
        Task<decimal> DeductRawMaterialStockAsync(Guid rawMaterialId, decimal quantity);

        /// <summary>
        /// Restores raw-material stock for a previously recorded waste deduction.
        /// Respects an existing ambient transaction when one is already open.
        /// </summary>
        Task RestoreRawMaterialStockAsync(Guid rawMaterialId, decimal quantity, CancellationToken cancellationToken = default);

        Task ReceiveStockAsync(Guid purchaseOrderId); // Placeholder for future use

        /// <summary>
        /// Recomputes RawMaterial.CurrentStock and RawMaterial.CostPerUnit from the live Main Branch batch state.
        /// Under FIFO, CostPerUnit reflects the head of the queue — the UnitCost of the oldest Good batch
        /// that the next sale will draw from. Call after any batch insert / update / delete / consume.
        /// Does NOT call SaveChanges — caller owns the transaction.
        /// </summary>
        Task RefreshMaterialFifoCostAsync(Guid rawMaterialId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Current available stock for a material on a branch, derived from approved
        /// Good batches (the same source FIFO consumption draws from). This is the
        /// authoritative quantity — never the cached RawMaterial.CurrentStock mirror.
        /// </summary>
        Task<decimal> GetBranchStockAsync(Guid rawMaterialId, Guid branchId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies a manual stock adjustment to <paramref name="targetStock"/> on the given
        /// branch WITHOUT overwriting CurrentStock directly: an increase creates an approved
        /// correction batch (valued at the material's current CostPerUnit — unit cost is never
        /// changed here); a decrease consumes existing batches via the standard FIFO engine.
        /// Respects an ambient transaction when one is already open. Returns the before/after
        /// quantities and signed delta.
        /// </summary>
        Task<StockAdjustmentOutcome> ApplyStockAdjustmentAsync(
            Guid rawMaterialId,
            decimal targetStock,
            Guid branchId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Revalues the current unit cost WITHOUT touching quantities or history: sets
        /// <paramref name="newUnitCost"/> on the branch's remaining Good batches (which
        /// drives inventory valuation and future FIFO consumption) and refreshes the
        /// material's reference CostPerUnit. Finished/consumed batches, purchase orders
        /// and already-recorded COGS are never modified. On the Main branch this also
        /// updates the global CostPerUnit and cascades to recipe costing; on other
        /// branches only that branch's batches are revalued. Respects an ambient
        /// transaction when one is open.
        /// </summary>
        Task<UnitCostAdjustmentOutcome> ApplyUnitCostAdjustmentAsync(
            Guid rawMaterialId,
            decimal newUnitCost,
            Guid branchId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Before/after result of <see cref="IInventoryService.ApplyStockAdjustmentAsync"/>.</summary>
    public readonly record struct StockAdjustmentOutcome(decimal PreviousStock, decimal NewStock, decimal Delta);

    /// <summary>Before/after result of <see cref="IInventoryService.ApplyUnitCostAdjustmentAsync"/>.
    /// <paramref name="RevaluedBatchCount"/> is how many remaining batches were repriced.</summary>
    public readonly record struct UnitCostAdjustmentOutcome(decimal PreviousUnitCost, decimal NewUnitCost, int RevaluedBatchCount);
}
