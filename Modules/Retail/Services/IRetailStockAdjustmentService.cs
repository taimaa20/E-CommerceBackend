using RestaurantPos.Api.Modules.Retail.DTOs;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <summary>
    /// Manual corrections to finished-goods stock — a stock count, breakage, a shrinkage
    /// write-off. Everything else that moves retail stock is a consequence of a document
    /// (a purchase receipt, an order), so this is the only path that exists purely because a
    /// human says the shelf disagrees with the system.
    ///
    /// It posts a movement like everything else. It never edits a balance, and it never
    /// touches <c>RetailProductDetail.ReceivedQuantity</c>.
    /// </summary>
    public interface IRetailStockAdjustmentService
    {
        /// <summary>
        /// Brings the product's stock on the ACTIVE branch to <c>NewStock</c> by posting one
        /// adjustment movement for the difference. A request that matches the current position
        /// posts nothing and reports a zero delta, so a double-submitted form is harmless.
        /// </summary>
        Task<RetailStockAdjustmentResultDto> AdjustAsync(
            Guid productId,
            RetailStockAdjustmentCreateDto dto,
            CancellationToken ct = default);

        /// <summary>Manual adjustments for a product on the active branch, newest first.</summary>
        Task<IReadOnlyList<RetailStockAdjustmentResultDto>> GetHistoryAsync(
            Guid productId,
            CancellationToken ct = default);
    }
}
