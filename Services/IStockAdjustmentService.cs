using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IStockAdjustmentService
    {
        /// <summary>
        /// Applies a manual stock adjustment for a raw material on the active branch
        /// and records an immutable audit row. The stock change flows through the
        /// existing batch/FIFO engine — CurrentStock is never overwritten directly.
        /// </summary>
        Task<StockAdjustmentResultDto> AdjustAsync(
            Guid materialId,
            StockAdjustmentCreateDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies a manual unit-cost adjustment for a raw material on the active branch
        /// and records an immutable audit row. Revalues the branch's remaining Good batches
        /// (never rewrites historical costs) through the existing costing engine.
        /// </summary>
        Task<StockAdjustmentResultDto> AdjustUnitCostAsync(
            Guid materialId,
            UnitCostAdjustmentCreateDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>Manual-adjustment history for a material on the active branch (newest first).</summary>
        Task<IReadOnlyList<StockAdjustmentResultDto>> GetHistoryAsync(
            Guid materialId,
            CancellationToken cancellationToken = default);

        /// <summary>Manual-adjustment history for ALL materials on the active branch
        /// (newest first, capped). Drives the SuperAdmin audit page.</summary>
        Task<IReadOnlyList<StockAdjustmentResultDto>> GetBranchHistoryAsync(
            CancellationToken cancellationToken = default);
    }
}
