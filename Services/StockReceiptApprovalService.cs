using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services;

public sealed class StockReceiptApprovalService : IStockReceiptApprovalService
{
    private readonly IProcurementRepository _repository;
    private readonly IInventoryService _inventory;
    private readonly IWarehouseService _warehouseService;
    private readonly ILogger<StockReceiptApprovalService> _logger;

    public StockReceiptApprovalService(
        IProcurementRepository repository,
        IInventoryService inventory,
        IWarehouseService warehouseService,
        ILogger<StockReceiptApprovalService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ApproveAsync(PurchaseOrder order, CancellationToken ct)
    {
        if (_repository.HasActiveTransaction)
        {
            await ApproveCoreAsync(order, ct);
            return;
        }

        await using var transaction = await _repository.BeginTransactionAsync(ct);
        await ApproveCoreAsync(order, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task CancelPendingAsync(PurchaseOrder order, CancellationToken ct)
    {
        if (_repository.HasActiveTransaction)
        {
            await CancelCoreAsync(order, ct);
            return;
        }

        await using var transaction = await _repository.BeginTransactionAsync(ct);
        await CancelCoreAsync(order, ct);
        await transaction.CommitAsync(ct);
    }

    private async Task ApproveCoreAsync(PurchaseOrder order, CancellationToken ct)
    {
        var batches = await _repository.GetUnapprovedBatchesAsync(order.Id, ct);
        var materialIds = ApproveBatches(batches);

        order.Status = PurchaseOrderStatus.Approved;
        await _repository.SaveChangesAsync(ct);

        foreach (var materialId in materialIds)
            await _inventory.RefreshMaterialFifoCostAsync(materialId, ct);

        foreach (var batch in batches.Where(batch => batch.RawMaterial != null))
            await IncrementWarehouseAsync(batch, order.BranchId, ct);

        await _repository.SaveChangesAsync(ct);
    }

    private async Task CancelCoreAsync(PurchaseOrder order, CancellationToken ct)
    {
        var batches = await _repository.GetUnapprovedBatchesAsync(order.Id, ct);
        var materialIds = batches.Select(batch => batch.MaterialId).Distinct().ToList();

        _repository.RemoveBatches(batches);
        order.Status = PurchaseOrderStatus.Cancelled;
        await _repository.SaveChangesAsync(ct);

        foreach (var materialId in materialIds)
            await _inventory.RefreshMaterialFifoCostAsync(materialId, ct);

        await _repository.SaveChangesAsync(ct);
    }

    private HashSet<Guid> ApproveBatches(IEnumerable<StockBatch> batches)
    {
        var materialIds = new HashSet<Guid>();
        foreach (var batch in batches)
        {
            if (batch.RawMaterial is null)
            {
                _logger.LogError(
                    "Stock batch {BatchId} has no raw material and cannot affect inventory",
                    batch.Id);
                continue;
            }

            batch.TotalCost = batch.Quantity * batch.UnitCost;
            batch.IsApproved = true;
            materialIds.Add(batch.MaterialId);
        }

        return materialIds;
    }

    private async Task IncrementWarehouseAsync(
        StockBatch batch,
        Guid branchId,
        CancellationToken ct)
    {
        var warehouseId = await _warehouseService.ResolveSourceWarehouseIdAsync(
            batch.MaterialId,
            ct);
        if (!warehouseId.HasValue)
            return;

        await _warehouseService.IncrementInventoryAsync(
            batch.MaterialId,
            warehouseId.Value,
            batch.Quantity,
            branchId,
            ct);
    }
}
