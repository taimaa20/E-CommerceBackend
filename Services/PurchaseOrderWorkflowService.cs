using MediatR;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services;

public sealed class PurchaseOrderWorkflowService : IPurchaseOrderWorkflowService
{
    private readonly IProcurementRepository _repository;
    private readonly IMediator _mediator;
    private readonly IStockReceiptApprovalService _stockApproval;
    private readonly ILogger<PurchaseOrderWorkflowService> _logger;

    public PurchaseOrderWorkflowService(
        IProcurementRepository repository,
        IMediator mediator,
        IStockReceiptApprovalService stockApproval,
        ILogger<PurchaseOrderWorkflowService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _stockApproval = stockApproval ?? throw new ArgumentNullException(nameof(stockApproval));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PurchaseOrder> UpdateStatusAsync(
        Guid purchaseOrderId,
        PurchaseOrderStatus status,
        CancellationToken ct)
    {
        var order = await _repository.GetOrderAsync(purchaseOrderId, ct)
            ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

        ValidateTransition(order.Status, status);

        switch (status)
        {
            case PurchaseOrderStatus.Received:
                await ReceiveAsync(order, ct);
                break;
            case PurchaseOrderStatus.Approved:
                await _stockApproval.ApproveAsync(order, ct);
                break;
            case PurchaseOrderStatus.Cancelled:
                await _stockApproval.CancelPendingAsync(order, ct);
                break;
            default:
                order.Status = status;
                SyncTotals(order);
                await _repository.SaveChangesAsync(ct);
                break;
        }

        _logger.LogInformation(
            "Purchase order {PurchaseOrderId} transitioned to {Status}",
            order.Id,
            order.Status);
        return order;
    }

    private async Task ReceiveAsync(PurchaseOrder order, CancellationToken ct)
    {
        if (_repository.HasActiveTransaction)
        {
            await ReceiveCoreAsync(order, ct);
            return;
        }

        await using var transaction = await _repository.BeginTransactionAsync(ct);
        await ReceiveCoreAsync(order, ct);
        await transaction.CommitAsync(ct);
    }

    private async Task ReceiveCoreAsync(PurchaseOrder order, CancellationToken ct)
    {
        order.Status = PurchaseOrderStatus.Received;
        order.ReceivedDate = DateTime.UtcNow;
        SyncTotals(order);
        await _repository.SaveChangesAsync(ct);
        await _mediator.Publish(new PurchaseOrderReceivedEvent(order.Id, order.TenantId), ct);
    }

    private static void ValidateTransition(
        PurchaseOrderStatus current,
        PurchaseOrderStatus requested)
    {
        if (requested == PurchaseOrderStatus.Received && current != PurchaseOrderStatus.Draft)
            throw new ValidationException("Can only receive orders in Draft status.");

        if (requested == PurchaseOrderStatus.Approved && current != PurchaseOrderStatus.Received)
            throw new ValidationException("Can only approve orders in Received status.");

        if (requested == PurchaseOrderStatus.Cancelled && current == PurchaseOrderStatus.Approved)
            throw new ValidationException(
                "Approved purchase orders cannot be cancelled because inventory was already updated.");
    }

    private static void SyncTotals(PurchaseOrder order)
    {
        order.ItemCount = (int)(order.Items?.Sum(item => item.Quantity) ?? 0);
        order.UnitCost = order.Items?.FirstOrDefault()?.UnitPrice ?? 0;
        order.TotalAmount = order.Items?.Sum(item => item.Quantity * item.UnitPrice) ?? 0;
    }
}
