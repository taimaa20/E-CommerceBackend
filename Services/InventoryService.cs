using MediatR;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly PosDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<InventoryService> _logger;
        private readonly INotificationService _notificationService;
        private readonly IWarehouseService _warehouseService;
        private readonly IBranchContext _branchContext;
        private readonly IProductStockLedger _productStockLedger;

        public InventoryService(
            PosDbContext context,
            IMediator mediator,
            ILogger<InventoryService> logger,
            INotificationService notificationService,
            IWarehouseService warehouseService,
            IBranchContext branchContext,
            IProductStockLedger productStockLedger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
            _notificationService = notificationService;
            _warehouseService = warehouseService;
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _productStockLedger = productStockLedger ?? throw new ArgumentNullException(nameof(productStockLedger));
        }

        /// <summary>
        /// Process stock for a single order item. Called immediately when an item becomes Ready.
        /// Transaction-safe and idempotent (IsReady guard at controller level prevents double calls).
        /// CONTRACT: assigns batch-derived cost to OrderItem.StockDeductedCost. Selling price
        /// (OrderItem.Price / Product.BasePrice) is INTENTIONALLY never read or written here.
        /// </summary>
        public async Task<decimal> ProcessOrderItemStockAsync(Guid orderId, Guid orderItemId)
        {
            _logger.LogInformation("[FIFO] Processing stock for OrderItem {OrderItemId} in Order {OrderId}", orderItemId, orderId);

            var orderItem = await _context.Set<OrderItem>()
                .Include(oi => oi.RecipeSnapshotItems)
                .FirstOrDefaultAsync(oi => oi.Id == orderItemId && oi.OrderId == orderId);

            if (orderItem == null)
            {
                _logger.LogWarning("[FIFO] OrderItem {OrderItemId} not found in Order {OrderId}. Skipping.", orderItemId, orderId);
                return 0;
            }

            if (orderItem.StockDeductedAt.HasValue)
            {
                _logger.LogInformation("[FIFO] OrderItem {OrderItemId} stock already deducted. Skipping.", orderItemId);
                return orderItem.StockDeductedCost;
            }

            var recipeSnapshotItems = orderItem.RecipeSnapshotItems?.ToList() ?? new List<OrderItemRecipeSnapshot>();
            if (recipeSnapshotItems.Count == 0)
            {
                var product = await _context.Products
                    .Include(p => p.RecipeItems)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == orderItem.ProductId);
                if (product?.RecipeItems == null || product.RecipeItems.Count == 0)
                {
                    // No recipe does not always mean no stock: finished goods are stocked as the
                    // product itself. The ledger owns that path end to end — exactly-once posting,
                    // cost snapshot and order rollup — so nothing is added on top of what it returns.
                    var ledgerCost = await _productStockLedger.SyncOrderItemAsync(orderId, orderItemId);
                    if (ledgerCost.HasValue)
                    {
                        _logger.LogInformation(
                            "[Ledger] OrderItem {OrderItemId} relieved from finished-goods stock. Cost: {Cost:F2}",
                            orderItemId, ledgerCost.Value);
                        return ledgerCost.Value;
                    }

                    _logger.LogInformation("[FIFO] Product {ProductId} has no recipe. No stock to deduct.", orderItem.ProductId);
                    return 0;
                }

                recipeSnapshotItems = product.RecipeItems.Select(recipeItem => new OrderItemRecipeSnapshot
                {
                    RawMaterialId = recipeItem.RawMaterialId,
                    Quantity = orderItem.Quantity * recipeItem.Amount
                }).ToList();
            }

            if (recipeSnapshotItems.Count == 0)
            {
                return 0;
            }

            decimal itemCost = 0;

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync()
                : null;
            try
            {
                foreach (var recipeItem in recipeSnapshotItems)
                {
                    decimal materialCost = await DeductStockFifoAsync(recipeItem.RawMaterialId, recipeItem.Quantity, orderItem.BranchId);
                    itemCost += materialCost;
                }

                orderItem.StockDeductedAt = DateTime.UtcNow;
                orderItem.StockDeductedCost = itemCost;

                // Roll cost up to the order. Selling price is preserved — only TotalCost/NetProfit move.
                var order = await _context.Orders.FindAsync(orderId);
                if (order != null)
                {
                    order.TotalCost += itemCost;
                    order.NetProfit = GetProfitRevenue(order) - order.TotalCost;

                    // Defensive: cost > revenue per item is a costing red flag, not a price-change trigger.
                    decimal lineRevenue = orderItem.IsComplimentary ? 0 : orderItem.Price * orderItem.Quantity;
                    if (itemCost > lineRevenue && lineRevenue > 0)
                    {
                        _logger.LogWarning(
                            "[FIFO] OrderItem {OrderItemId} cost {Cost} exceeds revenue {Revenue}. Selling price NOT adjusted — review batch unit costs.",
                            orderItemId, itemCost, lineRevenue);
                    }
                }

                await _context.SaveChangesAsync();
                if (ownsTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                }

                if (order != null)
                {
                    await _mediator.Publish(new OrderStockProcessedEvent(order.Id, order.TenantId));
                }

                _logger.LogInformation("[FIFO] OrderItem {OrderItemId} processed. Cost: {Cost:F2}", orderItemId, itemCost);

                // Check profit margin after each item deduction
                var revenue = order == null ? 0m : GetProfitRevenue(order);
                if (order != null && revenue > 0)
                {
                    decimal margin = (order.NetProfit / revenue) * 100;
                    if (margin < 20)
                    {
                        await _mediator.Publish(new LowMarginAlertEvent(order.Id, order.OrderNumber, margin, order.NetProfit));
                    }
                }

                return itemCost;
            }
            catch (Exception ex)
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                _logger.LogError(ex, "[FIFO] Failed to process stock for OrderItem {OrderItemId}. Transaction rolled back.", orderItemId);
                throw;
            }
        }

        /// <summary>
        /// Bulk fallback: processes all unprocessed items when the entire order is marked Ready at once.
        /// Skips items that were already individually processed (if order already has TotalCost).
        /// </summary>
        public async Task ProcessOrderStockAsync(Guid orderId)
        {
            _logger.LogInformation("[FIFO Engine] Processing stock for Order {OrderId}", orderId);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.RecipeSnapshotItems)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return;

            // Guard: prevent duplicate deduction (TotalCost > 0 means already processed)
            if (order.TotalCost > 0)
            {
                _logger.LogWarning("[FIFO Engine] Order {OrderId} already processed (TotalCost={TotalCost}). Skipping duplicate deduction.", orderId, order.TotalCost);
                return;
            }

            decimal totalOrderCost = 0;

            // Load products with recipes in a single query
            // Transaction ensures atomic deduction for the whole order
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var item in order.OrderItems)
                {
                    if (item.StockDeductedAt.HasValue)
                    {
                        totalOrderCost += item.StockDeductedCost;
                        continue;
                    }

                    var recipeSnapshotItems = item.RecipeSnapshotItems?.ToList() ?? new List<OrderItemRecipeSnapshot>();
                    if (recipeSnapshotItems.Count == 0)
                    {
                        var product = await _context.Products
                            .Include(p => p.RecipeItems)
                            .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                        // Finished-goods lines have no recipe but do have stock. The ledger settles
                        // them and writes their cost snapshot; the cost still has to join this
                        // order's total, or the rollup below would drop it.
                        if (product?.RecipeItems == null || product.RecipeItems.Count == 0)
                        {
                            var ledgerCost = await _productStockLedger.SyncOrderItemAsync(order.Id, item.Id, CancellationToken.None);
                            if (ledgerCost.HasValue)
                            {
                                totalOrderCost += ledgerCost.Value;
                                continue;
                            }
                        }
                        if (product?.RecipeItems == null) continue;

                        recipeSnapshotItems = product.RecipeItems.Select(recipeItem => new OrderItemRecipeSnapshot
                        {
                            RawMaterialId = recipeItem.RawMaterialId,
                            Quantity = item.Quantity * recipeItem.Amount
                        }).ToList();
                    }

                    foreach (var recipeItem in recipeSnapshotItems)
                    {
                        decimal costForThisMaterial = await DeductStockFifoAsync(recipeItem.RawMaterialId, recipeItem.Quantity, order.BranchId);
                        totalOrderCost += costForThisMaterial;
                        item.StockDeductedCost += costForThisMaterial;
                    }

                    item.StockDeductedAt = DateTime.UtcNow;
                }

                // Selling price (item.Price) is preserved verbatim — only cost/profit fields move.
                order.TotalCost = totalOrderCost;
                order.NetProfit = GetProfitRevenue(order) - totalOrderCost;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                await _mediator.Publish(new OrderStockProcessedEvent(order.Id, order.TenantId));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "[FIFO Engine] Failed to process stock for Order {OrderId}. Transaction rolled back.", orderId);
                throw;
            }

            // Profit margin alert (outside transaction)
            var profitRevenue = GetProfitRevenue(order);
            if (profitRevenue > 0)
            {
                decimal margin = (order.NetProfit / profitRevenue) * 100;
                if (margin < 20)
                {
                    await _mediator.Publish(new LowMarginAlertEvent(order.Id, order.OrderNumber, margin, order.NetProfit));
                }
            }
        }

        public async Task<decimal> DeductRawMaterialStockAsync(Guid rawMaterialId, decimal quantity)
        {
            if (quantity <= 0)
            {
                return 0;
            }

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var branchId = await ResolveCurrentBranchIdAsync(CancellationToken.None);
                var cost = await DeductStockFifoAsync(rawMaterialId, quantity, branchId);
                await _context.SaveChangesAsync();

                if (ownsTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return cost;
            }
            catch
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }
        }

        public async Task RestoreRawMaterialStockAsync(
            Guid rawMaterialId,
            decimal quantity,
            CancellationToken cancellationToken = default)
        {
            if (quantity <= 0)
                return;

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;

            try
            {
                var branchId = await ResolveCurrentBranchIdAsync(cancellationToken);
                await RestoreStockFifoAsync(rawMaterialId, quantity, branchId, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                if (ownsTransaction && transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
        }

        // Single definition, shared with the retail stock ledger.
        private static decimal GetProfitRevenue(Order order)
            => DeliveryAccountingHelper.ProfitRevenue(order);

        /// <summary>
        /// FIFO (First-In-First-Out) deduction engine.
        /// Marks expired batches (excluded from consumption), deducts from oldest-received batch first,
        /// marks finished batches. Recomputes CurrentStock from approved Good batches only.
        /// COSTING ONLY — never reads or writes any selling-price field.
        /// </summary>
        private async Task<decimal> DeductStockFifoAsync(Guid rawMaterialId, decimal requiredAmount, Guid branchId)
        {
            decimal totalCost = 0;
            decimal remainingToDeduct = requiredAmount;

            // Warehouse-aware source selection (additive). FIFO costing on
            // StockBatch is unchanged; we just also debit the chosen warehouse's
            // RawMaterialInventory row so per-warehouse balances stay in sync.
            // Legacy materials without a Main/inventory row are no-ops here.
            var sourceWarehouseId = await _warehouseService.ResolveSourceWarehouseIdAsync(rawMaterialId, CancellationToken.None);

            var today = DateTime.UtcNow.Date;

            // 1. Mark expired batches (do NOT change quantity, only status)
            var expiredBatches = await _context.StockBatches
                .Where(l => l.MaterialId == rawMaterialId
                         && l.BranchId == branchId
                         && l.IsApproved
                         && (l.PurchaseOrderId == null || l.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                         && l.Status == BatchStatus.Good
                         && l.ExpiryDate < today)
                .ToListAsync();

            foreach (var expired in expiredBatches)
            {
                expired.Status = BatchStatus.Expired;
                _logger.LogInformation("[FIFO] Batch {BatchId} marked as Expired (ExpiryDate: {ExpiryDate:yyyy-MM-dd})", expired.Id, expired.ExpiryDate);

                // Notify Admin about expired batch
                var expiredMaterial = await _context.RawMaterials.FindAsync(expired.MaterialId);
                if (expiredMaterial != null)
                {
                    _ = _notificationService.SendAsync(
                        NotificationType.ExpiryWarning,
                        "Stock Expired",
                        $"{expiredMaterial.Name}: Batch expired on {expired.ExpiryDate:yyyy-MM-dd} ({expired.RemainingQuantity:F1} {expiredMaterial.Unit} lost)",
                        expired.MaterialId.ToString(),
                        "Admin");
                }
            }

            // 2. FIFO: deduct from Good batches only, oldest received first (CreatedAt asc).
            //    ExpiryDate is the secondary sort to keep behavior deterministic when two batches
            //    share the same CreatedAt (e.g. a single bulk import).
            var lots = await _context.StockBatches
                .Where(l => l.MaterialId == rawMaterialId
                         && l.BranchId == branchId
                         && l.IsApproved
                         && (l.PurchaseOrderId == null || l.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                         && l.RemainingQuantity > 0
                         && l.Status == BatchStatus.Good)
                .OrderBy(l => l.CreatedAt)
                .ThenBy(l => l.ExpiryDate)
                .ToListAsync();

            var rawMaterial = await _context.RawMaterials.FindAsync(rawMaterialId);

            // 3. Deduct batch by batch — cost is the batch's UnitCost at receipt time, frozen.
            foreach (var lot in lots)
            {
                if (remainingToDeduct <= 0) break;

                decimal deductFromLot = Math.Min(lot.RemainingQuantity, remainingToDeduct);

                totalCost += deductFromLot * lot.UnitCost;

                lot.RemainingQuantity -= deductFromLot;
                remainingToDeduct -= deductFromLot;

                // When fully consumed → mark as Finished (keep in DB for history).
                // Selling price is NOT recalculated — the next order continues at the same Price,
                // and cost simply rolls to the next FIFO batch.
                if (lot.RemainingQuantity == 0)
                {
                    lot.Status = BatchStatus.Finished;
                    _logger.LogInformation("[FIFO] Batch {BatchId} fully consumed → Status = Finished", lot.Id);

                    // Notify Admin about finished batch
                    if (rawMaterial != null)
                    {
                        _ = _notificationService.SendAsync(
                            NotificationType.LowStock,
                            "Stock Batch Finished",
                            $"{rawMaterial.Name}: A batch has been fully consumed",
                            rawMaterialId.ToString(),
                            "Admin");
                    }
                }

                _logger.LogInformation("[FIFO] Deducted {Qty} from Batch {BatchId}. Cost: {Cost:F2}", deductFromLot, lot.Id, deductFromLot * lot.UnitCost);
            }

            // 4. Insufficient stock fallback — record the gap at the material's reference cost
            //    so profit math stays meaningful. This is a costing fallback ONLY; it does not
            //    affect what the customer was charged. Operationally, this should be rare and
            //    indicates a missed PO / under-received batch.
            if (remainingToDeduct > 0 && rawMaterial != null)
            {
                decimal missingCost = remainingToDeduct * rawMaterial.CostPerUnit;
                totalCost += missingCost;
                _logger.LogWarning("[FIFO] Insufficient batches for Material {Name}. Charged reference CostPerUnit for missing {Qty}. Selling price unchanged.", rawMaterial.Name, remainingToDeduct);
            }

            // Mirror the deduction onto the per-warehouse ledger so the
            // warehouse-level Quantity reflects sales. The call is a no-op
            // when no RawMaterialInventory row exists (legacy data path).
            if (sourceWarehouseId.HasValue)
            {
                await _warehouseService.DecrementInventoryAsync(
                    rawMaterialId,
                    sourceWarehouseId.Value,
                    requiredAmount,
                    branchId,
                    CancellationToken.None);
            }

            // 5. Recompute CurrentStock + CostPerUnit (head of FIFO queue) on the parent material.
            if (rawMaterial != null)
            {
                var branchStock = await RefreshMaterialFifoCostForBranchAsync(rawMaterialId, branchId, CancellationToken.None);

                // 6. Alert if stock below minimum threshold
                if (branchStock <= rawMaterial.MinimumAlertLevel && rawMaterial.MinimumAlertLevel > 0)
                {
                    _ = _notificationService.SendAsync(
                        NotificationType.LowStock,
                        "Low Stock Alert",
                        $"{rawMaterial.Name}: {branchStock:F1} {rawMaterial.Unit} remaining (min: {rawMaterial.MinimumAlertLevel:F1})",
                        rawMaterialId.ToString(),
                        "Admin");
                }
            }

            return totalCost;
        }

        private async Task RestoreStockFifoAsync(
            Guid rawMaterialId,
            decimal quantity,
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var remaining = quantity;
            var batches = await _context.StockBatches
                .Where(b => b.MaterialId == rawMaterialId
                         && b.BranchId == branchId
                         && b.IsApproved
                         && (b.PurchaseOrderId == null || b.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                         && b.RemainingQuantity < b.Quantity)
                .OrderByDescending(b => b.CreatedAt)
                .ThenByDescending(b => b.ExpiryDate)
                .ToListAsync(cancellationToken);

            foreach (var batch in batches)
            {
                if (remaining <= 0)
                    break;

                var restored = Math.Min(batch.Quantity - batch.RemainingQuantity, remaining);
                batch.RemainingQuantity += restored;
                remaining -= restored;

                if (batch.RemainingQuantity > 0 && batch.Status == BatchStatus.Finished)
                    batch.Status = BatchStatus.Good;
            }

            var material = await _context.RawMaterials.FindAsync(
                new object[] { rawMaterialId },
                cancellationToken);
            var isMainBranch = await IsMainBranchAsync(branchId, cancellationToken);

            if (batches.Count == 0 && material != null && isMainBranch)
            {
                material.CurrentStock += quantity;
            }
            else
            {
                await RefreshMaterialFifoCostForBranchAsync(rawMaterialId, branchId, cancellationToken);
                if (remaining > 0 && material != null && isMainBranch)
                {
                    material.CurrentStock += remaining;
                    _logger.LogWarning(
                        "[FIFO] Restored {Quantity} for Material {MaterialId} outside batch capacity during waste edit reversal.",
                        remaining,
                        rawMaterialId);
                }
            }

            var sourceWarehouseId = await _warehouseService.ResolveSourceWarehouseIdAsync(rawMaterialId, cancellationToken);
            if (sourceWarehouseId.HasValue)
            {
                await _warehouseService.IncrementInventoryAsync(
                    rawMaterialId,
                    sourceWarehouseId.Value,
                    quantity,
                    branchId,
                    cancellationToken);
            }
        }

        public Task ReceiveStockAsync(Guid purchaseOrderId)
        {
            // Future implementation
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RefreshMaterialFifoCostAsync(
            Guid rawMaterialId,
            CancellationToken cancellationToken = default)
        {
            var branchId = await ResolveCurrentBranchIdAsync(cancellationToken);
            await RefreshMaterialFifoCostForBranchAsync(rawMaterialId, branchId, cancellationToken);
        }

        private async Task<decimal> RefreshMaterialFifoCostForBranchAsync(
            Guid rawMaterialId,
            Guid branchId,
            CancellationToken cancellationToken = default)
        {
            var material = await _context.RawMaterials.FindAsync(
                new object[] { rawMaterialId },
                cancellationToken);
            if (material == null) return 0m;

            var previousCostPerUnit = material.CostPerUnit;
            var branchStock = await _context.StockBatches
                .Where(b => b.MaterialId == rawMaterialId
                         && b.BranchId == branchId
                         && b.IsApproved
                         && (b.PurchaseOrderId == null || b.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                         && b.Status == BatchStatus.Good)
                .SumAsync(b => b.RemainingQuantity, cancellationToken);

            // CostPerUnit = UnitCost of the next FIFO batch (oldest Good batch with remaining qty).
            // When no Good batch is left, leave CostPerUnit as the last known value so the
            // insufficient-stock fallback in DeductStockFifoAsync still has a sensible reference.
            var nextFifoCost = await _context.StockBatches
                .Where(b => b.MaterialId == rawMaterialId
                         && b.BranchId == branchId
                         && b.IsApproved
                         && (b.PurchaseOrderId == null || b.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                         && b.Status == BatchStatus.Good
                         && b.RemainingQuantity > 0)
                .OrderBy(b => b.CreatedAt)
                .ThenBy(b => b.ExpiryDate)
                .Select(b => (decimal?)b.UnitCost)
                .FirstOrDefaultAsync(cancellationToken);

            if (!await IsMainBranchAsync(branchId, cancellationToken))
                return branchStock;

            material.CurrentStock = branchStock;
            if (nextFifoCost.HasValue)
            {
                material.CostPerUnit = nextFifoCost.Value;
            }

            if (previousCostPerUnit != material.CostPerUnit)
            {
                await _mediator.Publish(
                    new RawMaterialCostChangedEvent(material.Id, material.TenantId),
                    cancellationToken);
            }

            return branchStock;
        }

        public Task<decimal> GetBranchStockAsync(
            Guid rawMaterialId,
            Guid branchId,
            CancellationToken cancellationToken = default)
            => SumBranchGoodStockAsync(rawMaterialId, branchId, cancellationToken);

        private Task<decimal> SumBranchGoodStockAsync(
            Guid rawMaterialId,
            Guid branchId,
            CancellationToken cancellationToken)
            => _context.StockBatches
                .Where(b => b.MaterialId == rawMaterialId
                         && b.BranchId == branchId
                         && b.IsApproved
                         && (b.PurchaseOrderId == null || b.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                         && b.Status == BatchStatus.Good)
                .SumAsync(b => b.RemainingQuantity, cancellationToken);

        public async Task<StockAdjustmentOutcome> ApplyStockAdjustmentAsync(
            Guid rawMaterialId,
            decimal targetStock,
            Guid branchId,
            CancellationToken cancellationToken = default)
        {
            var material = await _context.RawMaterials.FindAsync(new object[] { rawMaterialId }, cancellationToken)
                ?? throw new Exceptions.NotFoundException($"Raw material {rawMaterialId} was not found.");

            var previousStock = await SumBranchGoodStockAsync(rawMaterialId, branchId, cancellationToken);
            var delta = targetStock - previousStock;

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                if (delta > 0)
                {
                    // Increase → append an approved correction batch. Newest CreatedAt
                    // places it last in the FIFO queue (consumed after existing stock),
                    // so historical batch ordering and costing are untouched.
                    await IncreaseStockViaAdjustmentBatchAsync(material, delta, branchId, cancellationToken);
                    await RefreshMaterialFifoCostForBranchAsync(rawMaterialId, branchId, cancellationToken);
                }
                else if (delta < 0)
                {
                    // Decrease → consume the shortfall through the standard FIFO engine,
                    // which reduces existing batches oldest-first, marks emptied batches
                    // Finished, mirrors the warehouse ledger and refreshes the material.
                    // Because target >= 0, the amount removed never exceeds available stock.
                    await DeductStockFifoAsync(rawMaterialId, -delta, branchId);
                }

                await _context.SaveChangesAsync(cancellationToken);
                if (ownsTransaction && transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                var newStock = await SumBranchGoodStockAsync(rawMaterialId, branchId, cancellationToken);
                return new StockAdjustmentOutcome(previousStock, newStock, delta);
            }
            catch
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
        }

        public async Task<UnitCostAdjustmentOutcome> ApplyUnitCostAdjustmentAsync(
            Guid rawMaterialId,
            decimal newUnitCost,
            Guid branchId,
            CancellationToken cancellationToken = default)
        {
            var material = await _context.RawMaterials.FindAsync(new object[] { rawMaterialId }, cancellationToken)
                ?? throw new Exceptions.NotFoundException($"Raw material {rawMaterialId} was not found.");

            var previousUnitCost = material.CostPerUnit;

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                // Revalue ONLY the current on-hand inventory: approved Good batches with
                // remaining quantity, scoped to this branch. Finished/consumed batches
                // (RemainingQuantity == 0) are excluded, so already-recorded COGS and
                // historical batch costs stay frozen. Purchase orders are never touched.
                var remainingBatches = await _context.StockBatches
                    .Where(b => b.MaterialId == rawMaterialId
                             && b.BranchId == branchId
                             && b.IsApproved
                             && (b.PurchaseOrderId == null || b.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                             && b.Status == BatchStatus.Good
                             && b.RemainingQuantity > 0)
                    .ToListAsync(cancellationToken);

                foreach (var batch in remainingBatches)
                {
                    batch.UnitCost = newUnitCost;
                    // TotalCost mirrors the existing batch-edit convention (Quantity * UnitCost);
                    // it is a display/query field only — not used for COGS or valuation math.
                    batch.TotalCost = batch.Quantity * newUnitCost;
                }

                if (remainingBatches.Count > 0)
                {
                    // Refresh recomputes CostPerUnit from the now-revalued head batch
                    // (= newUnitCost) on the Main branch and publishes the cost-changed
                    // event so recipe/product costing recalculates. Non-Main branches keep
                    // the global Main-derived CostPerUnit untouched (existing behavior).
                    await RefreshMaterialFifoCostForBranchAsync(rawMaterialId, branchId, cancellationToken);
                }
                else if (await IsMainBranchAsync(branchId, cancellationToken))
                {
                    // No on-hand batches to revalue on Main → set the reference cost directly
                    // (Refresh leaves CostPerUnit unchanged when there is no Good batch) so
                    // recipe/estimate costing reflects the intended value.
                    material.CostPerUnit = newUnitCost;
                    if (previousUnitCost != newUnitCost)
                    {
                        await _mediator.Publish(
                            new RawMaterialCostChangedEvent(material.Id, material.TenantId),
                            cancellationToken);
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                if (ownsTransaction && transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return new UnitCostAdjustmentOutcome(previousUnitCost, newUnitCost, remainingBatches.Count);
            }
            catch
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
        }

        private async Task IncreaseStockViaAdjustmentBatchAsync(
            RawMaterial material,
            decimal amount,
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var batch = new StockBatch
            {
                Id = Guid.NewGuid(),
                TenantId = material.TenantId,
                BranchId = branchId,
                MaterialId = material.Id,
                PurchaseOrderId = null,
                BatchNumber = $"ADJ-{DateTime.UtcNow:yyyyMMddHHmmss}-{material.Id.ToString()[..6]}",
                UnitCost = material.CostPerUnit, // stock-only correction — cost is never edited here
                Quantity = amount,
                RemainingQuantity = amount,
                TotalCost = amount * material.CostPerUnit,
                CreatedAt = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddYears(5),
                IsApproved = true,
                Status = BatchStatus.Good,
            };

            _context.StockBatches.Add(batch);

            // Mirror the increase onto the per-warehouse ledger, matching the
            // PO-approval path. No-op for legacy materials without an inventory row.
            var sourceWarehouseId = await _warehouseService.ResolveSourceWarehouseIdAsync(material.Id, cancellationToken);
            if (sourceWarehouseId.HasValue)
            {
                await _warehouseService.IncrementInventoryAsync(
                    material.Id,
                    sourceWarehouseId.Value,
                    amount,
                    branchId,
                    cancellationToken);
            }
        }

        private async Task<Guid> ResolveCurrentBranchIdAsync(CancellationToken cancellationToken)
        {
            var context = await _branchContext.GetCurrentAsync(cancellationToken);
            return context.CurrentBranch.Id;
        }

        private Task<bool> IsMainBranchAsync(Guid branchId, CancellationToken cancellationToken)
            => _context.Branches
                .AsNoTracking()
                .Where(branch => branch.Id == branchId)
                .Select(branch => branch.IsMainBranch)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
