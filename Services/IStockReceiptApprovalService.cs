using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services;

public interface IStockReceiptApprovalService
{
    Task ApproveAsync(PurchaseOrder order, CancellationToken ct);
    Task CancelPendingAsync(PurchaseOrder order, CancellationToken ct);
}
