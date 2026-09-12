using MediatR;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Handlers
{
    public class StockEntryHandler : INotificationHandler<PurchaseOrderReceivedEvent>
    {
        private readonly PosDbContext _context;
        private readonly ILogger<StockEntryHandler> _logger;

        public StockEntryHandler(
            PosDbContext context,
            ILogger<StockEntryHandler> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Handle(PurchaseOrderReceivedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Processing stock entry for purchase order {PurchaseOrderId}",
                notification.PurchaseOrderId);

            var order = await GetOrderAsync(notification.PurchaseOrderId, cancellationToken);
            if (!CanCreateBatches(order, notification.PurchaseOrderId))
                return;

            var existingIds = await GetExistingMaterialIdsAsync(order!.Id, cancellationToken);
            var batches = order.Items
                .Where(item => !existingIds.Contains(item.RawMaterialId))
                .Select(item => BuildBatch(order, item, notification.TenantId))
                .ToList();

            if (batches.Count == 0)
            {
                _logger.LogWarning(
                    "No new stock batches were needed for purchase order {PurchaseOrderId}",
                    order.Id);
                return;
            }

            _context.StockBatches.AddRange(batches);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Created {BatchCount} stock batches for purchase order {PurchaseOrderId}",
                batches.Count,
                order.Id);
        }

        private Task<PurchaseOrder?> GetOrderAsync(Guid id, CancellationToken ct)
            => _context.PurchaseOrders
                .AsNoTracking()
                .Include(order => order.Items)
                .FirstOrDefaultAsync(order => order.Id == id, ct);

        private bool CanCreateBatches(PurchaseOrder? order, Guid id)
        {
            if (order is null)
            {
                _logger.LogError("Purchase order {PurchaseOrderId} was not found", id);
                return false;
            }

            if (order.Status == PurchaseOrderStatus.Received && order.Items.Count > 0)
                return true;

            _logger.LogWarning(
                "Purchase order {PurchaseOrderId} is not ready for stock batch creation",
                id);
            return false;
        }

        private async Task<HashSet<Guid>> GetExistingMaterialIdsAsync(Guid orderId, CancellationToken ct)
            => (await _context.StockBatches
                .AsNoTracking()
                .Where(batch => batch.PurchaseOrderId == orderId)
                .Select(batch => batch.MaterialId)
                .ToListAsync(ct))
                .ToHashSet();

        private static StockBatch BuildBatch(
            PurchaseOrder order,
            PurchaseOrderItem item,
            Guid tenantId)
        {
            var createdAt = DateTime.UtcNow;
            return new StockBatch
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = order.BranchId,
                MaterialId = item.RawMaterialId,
                PurchaseOrderId = order.Id,
                BatchNumber = $"PO-{order.OrderNumber}-{item.RawMaterialId.ToString()[..6]}",
                PurchaseOrderNumber = order.OrderNumber,
                UnitCost = item.UnitPrice,
                Quantity = item.Quantity,
                RemainingQuantity = item.Quantity,
                TotalCost = item.Quantity * item.UnitPrice,
                CreatedAt = createdAt,
                ExpiryDate = createdAt.AddMonths(6),
                IsApproved = false
            };
        }
    }
}
