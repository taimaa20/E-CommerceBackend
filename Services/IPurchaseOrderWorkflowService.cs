using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services;

public interface IPurchaseOrderWorkflowService
{
    Task<PurchaseOrder> UpdateStatusAsync(
        Guid purchaseOrderId,
        PurchaseOrderStatus status,
        CancellationToken ct);
}
