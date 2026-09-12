using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public class CancelLogService : ICancelLogService
    {
        private readonly PosDbContext _context;
        private readonly IBranchContext _branchContext;

        public CancelLogService(PosDbContext context, IBranchContext branchContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        }

        public async Task<CancelLogPageDto> GetAsync(
            Guid tenantId,
            Guid currentUserId,
            UserRole currentUserRole,
            CancelLogQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var limit = Math.Clamp(query.Limit, 1, 200);
            var page  = Math.Max(query.Page, 1);

            // Cancel logs are branch operational data — the list follows the active branch.
            var branchId = (await _branchContext.GetCurrentAsync(cancellationToken)).CurrentBranch.Id;
            var isPrivileged = currentUserRole == UserRole.Admin || currentUserRole == UserRole.Manager;
            var baseQuery = _context.CancelLogs.AsNoTracking()
                .Where(c => c.BranchId == branchId);

            if (!isPrivileged)
            {
                baseQuery = baseQuery.Where(c => c.CancelledById == currentUserId);
            }

            if (query.OrderId.HasValue)
                baseQuery = baseQuery.Where(c => c.OrderId == query.OrderId.Value);

            if (query.DateFrom.HasValue)
            {
                var from = NormalizeToUtc(query.DateFrom.Value);
                baseQuery = baseQuery.Where(c => c.CancelledAt >= from);
            }
            if (query.DateTo.HasValue)
            {
                var to = NormalizeToUtc(query.DateTo.Value).AddDays(1);
                baseQuery = baseQuery.Where(c => c.CancelledAt <= to);
            }

            var total = await baseQuery.CountAsync(cancellationToken);

            var rows = await baseQuery
                .OrderByDescending(c => c.CancelledAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(c => new ProjectedRow
                {
                    Id = c.Id,
                    OrderId = c.OrderId,
                    OrderNumber = c.Order.OrderNumber,
                    OrderItemId = c.OrderItemId,
                    ItemId = c.ItemId,
                    ItemName = c.ItemName,
                    ItemNameAr = c.ItemNameAr,
                    CancelledByName = c.CancelledByName,
                    CancelledByRole = c.CancelledByRole.ToString(),
                    OrderTime = c.OrderTime,
                    CancelledAt = c.CancelledAt,
                    CancelMode = c.CancelMode,
                    CancelReasonCode = c.CancelReasonCode,
                    CancelReasonNote = c.CancelReasonNote,
                    WasteLogId = c.WasteLogId,
                    RequiresKitchenApproval = c.RequiresKitchenApproval,
                    ApprovalStatus = c.ApprovalStatus.ToString(),
                    KitchenDecision = c.KitchenDecision != null ? c.KitchenDecision.ToString() : null,
                    KitchenDecisionByName = c.KitchenDecisionByName,
                    KitchenDecisionAt = c.KitchenDecidedAt,
                    PendingItemName = c.OrderItemId != null
                        ? c.Order.OrderItems
                            .Where(oi => oi.Id == c.OrderItemId.Value)
                            .Select(oi => oi.ProductName)
                            .FirstOrDefault()
                        : null,
                    PendingItemNameAr = c.OrderItemId != null
                        ? c.Order.OrderItems
                            .Where(oi => oi.Id == c.OrderItemId.Value)
                            .Select(oi => oi.Product.NameAr)
                            .FirstOrDefault()
                        : null
                })
                .ToListAsync(cancellationToken);

            var reasonCodes = rows
                .Select(r => r.CancelReasonCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!)
                .Distinct()
                .ToList();

            var orderIds = rows.Select(r => r.OrderId).Distinct().ToList();
            var productIds = rows
                .Where(r => r.ItemId.HasValue)
                .Select(r => r.ItemId!.Value)
                .Distinct()
                .ToList();

            var reasonLookup = await _context.CancelReasons
                .AsNoTracking()
                .Where(r => reasonCodes.Contains(r.Code))
                .ToDictionaryAsync(r => r.Code, r => new ReasonName(r.Name, r.NameAr), cancellationToken);

            var productNameLookup = await _context.Products
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.NameAr })
                .ToDictionaryAsync(p => p.Id, p => p.NameAr, cancellationToken);

            var wasteByOrder = await _context.WasteLogs
                .AsNoTracking()
                .Where(w => w.SourceOrderId.HasValue && orderIds.Contains(w.SourceOrderId.Value))
                .Select(w => new WasteRef(w.SourceOrderId, w.SourceOrderItemId, w.ItemName, w.ItemNameAr, w.Id))
                .ToListAsync(cancellationToken);

            var orderWasteLookup = wasteByOrder
                .GroupBy(w => w.SourceOrderId!.Value)
                .ToDictionary(g => g.Key, g => g.Any());

            var itemWasteLookup = wasteByOrder
                .Where(w => w.SourceOrderItemId.HasValue)
                .GroupBy(w => w.SourceOrderItemId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var data = rows.Select(row =>
            {
                var reason = !string.IsNullOrEmpty(row.CancelReasonCode)
                             && reasonLookup.TryGetValue(row.CancelReasonCode, out var mapped)
                    ? mapped
                    : new ReasonName(row.CancelReasonCode, row.CancelReasonCode);

                itemWasteLookup.TryGetValue(row.OrderItemId ?? Guid.Empty, out var itemWaste);
                orderWasteLookup.TryGetValue(row.OrderId, out var orderHasWaste);

                var isFullOrder = row.ItemId == null && string.IsNullOrWhiteSpace(row.ItemName);
                var hasWaste = !isFullOrder && row.OrderItemId.HasValue
                    ? itemWaste != null
                    : orderHasWaste;
                productNameLookup.TryGetValue(row.ItemId ?? Guid.Empty, out var productNameAr);

                return new CancelLogRowDto
                {
                    Id = row.Id,
                    OrderId = row.OrderId,
                    OrderNumber = row.OrderNumber,
                    OrderItemId = row.OrderItemId,
                    IsFullOrder = isFullOrder,
                    // Prefer the waste-log snapshot; fall back to the order item snapshot for
                    // pending cancellations (no WasteLog yet).
                    ItemName = itemWaste?.ItemName ?? row.ItemName ?? row.PendingItemName,
                    ItemNameAr = itemWaste?.ItemNameAr ?? row.ItemNameAr ?? row.PendingItemNameAr ?? productNameAr,
                    CancelledByName = row.CancelledByName,
                    CancelledByRole = row.CancelledByRole,
                    OrderTime = row.OrderTime,
                    CancelledAt = row.CancelledAt,
                    CancelMode = row.CancelMode,
                    CancelReasonCode = row.CancelReasonCode,
                    CancelReasonLabel = reason.Name,
                    CancelReasonLabelAr = reason.NameAr,
                    CancelReasonNote = row.CancelReasonNote,
                    HasWaste = hasWaste,
                    WasteLogId = row.WasteLogId ?? itemWaste?.Id,
                    RequiresKitchenApproval = row.RequiresKitchenApproval,
                    ApprovalStatus = row.ApprovalStatus,
                    KitchenDecision = row.KitchenDecision,
                    KitchenDecisionByName = row.KitchenDecisionByName,
                    KitchenDecisionAt = row.KitchenDecisionAt
                };
            }).ToList();

            return new CancelLogPageDto
            {
                Data = data,
                Total = total,
                Page = page,
                Limit = limit
            };
        }

        private static DateTime NormalizeToUtc(DateTime value) =>
            value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

        // Internal projection types — kept private to the service to keep DTOs/ free of
        // pagination scratchpad shapes that the API contract never exposes.
        private sealed class ProjectedRow
        {
            public Guid Id { get; set; }
            public Guid OrderId { get; set; }
            public string? OrderNumber { get; set; }
            public Guid? OrderItemId { get; set; }
            public Guid? ItemId { get; set; }
            public string? ItemName { get; set; }
            public string? ItemNameAr { get; set; }
            public string? CancelledByName { get; set; }
            public string? CancelledByRole { get; set; }
            public DateTime OrderTime { get; set; }
            public DateTime CancelledAt { get; set; }
            public string? CancelMode { get; set; }
            public string? CancelReasonCode { get; set; }
            public string? CancelReasonNote { get; set; }
            public Guid? WasteLogId { get; set; }
            public bool RequiresKitchenApproval { get; set; }
            public string? ApprovalStatus { get; set; }
            public string? KitchenDecision { get; set; }
            public string? KitchenDecisionByName { get; set; }
            public DateTime? KitchenDecisionAt { get; set; }
            public string? PendingItemName { get; set; }
            public string? PendingItemNameAr { get; set; }
        }

        private sealed record ReasonName(string? Name, string? NameAr);
        private sealed record WasteRef(Guid? SourceOrderId, Guid? SourceOrderItemId, string? ItemName, string? ItemNameAr, Guid Id);
    }
}
