using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.DTOs;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <summary>
    /// Operational purchasing for retail finished goods: real SKU lines, landed cost, and a
    /// receipt that is the only thing in the system allowed to raise retail stock from a
    /// purchase. Suppliers come from the shared master — this service never creates one.
    /// </summary>
    public interface IRetailPurchasingService
    {
        Task<PaginatedResponse<RetailPurchaseOrderDto>> GetPageAsync(
            Guid branchId,
            RetailPurchaseOrderQuery query,
            bool isArabic,
            CancellationToken ct = default);

        Task<RetailPurchaseOrderDto> GetAsync(Guid id, bool isArabic, CancellationToken ct = default);

        Task<RetailPurchaseOrderDto> CreateAsync(
            Guid branchId,
            RetailPurchaseOrderCreateDto input,
            Guid? userId,
            bool isArabic,
            CancellationToken ct = default);

        Task<RetailPurchaseOrderDto> UpdateAsync(
            Guid id,
            RetailPurchaseOrderCreateDto input,
            bool isArabic,
            CancellationToken ct = default);

        /// <summary>Draft to Submitted. Lines are frozen from here on.</summary>
        Task<RetailPurchaseOrderDto> SubmitAsync(Guid id, bool isArabic, CancellationToken ct = default);

        /// <summary>
        /// Confirms received quantities, posts one PurchaseReceipt movement per line and rolls
        /// the weighted-average landed cost into the product. Idempotent at the document level:
        /// an order that has already posted its stock returns unchanged.
        /// </summary>
        Task<RetailPurchaseOrderDto> ReceiveAsync(
            Guid id,
            RetailPurchaseReceiptDto receipt,
            Guid? userId,
            bool isArabic,
            CancellationToken ct = default);

        Task<RetailPurchaseOrderDto> CancelAsync(Guid id, bool isArabic, CancellationToken ct = default);

        /// <summary>Stock audit trail for a product, newest first, with a running balance.</summary>
        Task<IReadOnlyList<RetailStockMovementDto>> GetMovementsAsync(
            Guid productId,
            Guid? branchId,
            int limit,
            CancellationToken ct = default);
    }
}
