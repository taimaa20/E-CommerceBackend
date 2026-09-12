using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public interface IOrderService
    {
        Task<PaginatedResponse<OrderListItemDto>> GetOrdersAsync(
            OrderFilterRequest request,
            Guid tenantId,
            Guid? branchId,
            bool isArabic,
            CancellationToken ct);

        Task<OrderListSummaryDto> GetOrdersSummaryAsync(
            OrderFilterRequest request,
            Guid tenantId,
            Guid? branchId,
            CancellationToken ct);

        Task<string> ExportOrdersCsvAsync(
            OrderFilterRequest request,
            Guid tenantId,
            Guid? branchId,
            CancellationToken ct);

        Task<List<PartnerPriceOverrideAuditDto>> GetPartnerPriceOverrideAuditsAsync(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct);

        Task<PartnerDiscountOverrideDto> UpdatePartnerDiscountOverrideAsync(
            Guid orderId,
            Guid orderItemId,
            PartnerDiscountOverrideRequest request,
            Guid tenantId,
            Guid branchId,
            Guid updatedBy,
            CancellationToken ct);

        Task<DeliveryPartnerPriceAdjustmentDto> UpdateDeliveryPartnerPriceAdjustmentAsync(
            Guid orderId,
            DeliveryPartnerPriceAdjustmentRequest request,
            Guid tenantId,
            Guid branchId,
            Guid updatedBy,
            CancellationToken ct);

        Task<OrderTotalOverrideDto> UpdateOrderTotalOverrideAsync(
            Guid orderId,
            OrderTotalOverrideRequest request,
            Guid tenantId,
            Guid branchId,
            Guid updatedBy,
            CancellationToken ct);

        Task MarkOnlineOrderPreparingAsync(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct);

        Task MarkOnlineOrderDispatchedAsync(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct);
    }
}
