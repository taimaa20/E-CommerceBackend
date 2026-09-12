using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class PosLogRepository : IPosLogRepository
    {
        private readonly PosDbContext _context;

        public PosLogRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<CashierBalanceShift?> GetShiftAsync(
            Guid tenantId,
            Guid shiftId,
            CancellationToken cancellationToken = default)
        {
            return _context.CashierBalanceShifts
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == shiftId, cancellationToken);
        }

        public async Task<PosLogPageDto> GetLogsAsync(
            Guid tenantId,
            PosLogQueryDto query,
            Guid currentUserId,
            bool isAdmin,
            CashierBalanceShift? shift,
            CancellationToken cancellationToken = default)
        {
            var page = Math.Max(query.Page, 1);
            var limit = Math.Clamp(query.Limit, 1, 200);
            var includeCancel = IsCancelIncluded(query.Type);
            var includeWaste = IsWasteIncluded(query.Type);
            var rows = new List<IQueryable<PosLogEntryDto>>();

            if (includeCancel)
                rows.Add(BuildCancelQuery(tenantId, query, currentUserId, isAdmin, shift));

            if (includeWaste)
                rows.Add(BuildWasteQuery(tenantId, query, currentUserId, isAdmin, shift));

            if (rows.Count == 0)
                return new PosLogPageDto { Page = page, Limit = limit };

            var combined = rows.Aggregate((left, right) => left.Concat(right));
            var total = await combined.CountAsync(cancellationToken);
            var data = await combined
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.Id)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return new PosLogPageDto
            {
                Data = data,
                Total = total,
                Page = page,
                Limit = limit
            };
        }

        private IQueryable<PosLogEntryDto> BuildCancelQuery(
            Guid tenantId,
            PosLogQueryDto query,
            Guid currentUserId,
            bool isAdmin,
            CashierBalanceShift? shift)
        {
            var logs = _context.CancelLogs
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId);

            if (!isAdmin)
                logs = logs.Where(c => c.CancelledById == currentUserId);

            if (IsToday(query.Date))
            {
                var todayStart = DateTime.UtcNow.Date;
                var todayEnd = todayStart.AddDays(1);
                logs = logs.Where(c => c.CancelledAt >= todayStart && c.CancelledAt < todayEnd);
            }

            if (shift != null)
            {
                var shiftEnd = shift.ClosedAt ?? DateTime.UtcNow;
                logs = logs.Where(c =>
                    c.ShiftId == shift.Id ||
                    (c.ShiftId == null &&
                     c.CancelledById == shift.CashierId &&
                     c.CancelledAt >= shift.OpenedAt &&
                     c.CancelledAt <= shiftEnd));
            }

            return logs.Select(c => new PosLogEntryDto
            {
                Id = c.Id,
                Type = PosLogTypes.Cancel,
                OrderId = c.OrderId,
                OrderNumber = c.Order.OrderNumber,
                ItemId = c.ItemId,
                OrderItemId = c.OrderItemId,
                ItemName = c.ItemName
                    ?? (c.WasteLogId != null
                        ? c.WasteLog!.ItemName
                        : c.OrderItemId.HasValue
                            ? c.Order.OrderItems
                                .Where(oi => oi.Id == c.OrderItemId.Value)
                                .Select(oi => oi.ProductName)
                                .FirstOrDefault()
                            : null),
                Quantity = c.Quantity > 0
                    ? c.Quantity
                    : c.WasteLogId != null
                        ? c.WasteLog!.Quantity
                        : c.OrderItemId.HasValue
                            ? c.Order.OrderItems
                                .Where(oi => oi.Id == c.OrderItemId.Value)
                                .Select(oi => (decimal?)oi.Quantity)
                                .FirstOrDefault() ?? 0m
                            : c.Order.OrderItems.Sum(oi => (decimal?)oi.Quantity) ?? 0m,
                Amount = c.Amount > 0
                    ? c.Amount
                    : c.WasteLogId != null && c.WasteLog!.Amount > 0
                        ? c.WasteLog.Amount
                        : c.OrderItemId.HasValue
                            ? c.Order.OrderItems
                                .Where(oi => oi.Id == c.OrderItemId.Value)
                                .Select(oi => (decimal?)(((oi.IsComplimentary ? 0m : oi.Price + oi.Modifiers.Sum(m => m.Price * m.Quantity)) * oi.Quantity) * (100m - c.Order.DiscountPercentage) / 100m))
                                .FirstOrDefault() ?? 0m
                            : (c.Order.OrderItems.Sum(oi => (decimal?)((oi.IsComplimentary ? 0m : oi.Price + oi.Modifiers.Sum(m => m.Price * m.Quantity)) * oi.Quantity)) ?? 0m) * (100m - c.Order.DiscountPercentage) / 100m,
                Reason = string.IsNullOrEmpty(c.CancelReasonNote)
                    ? c.CancelReasonCode
                    : c.CancelReasonCode + ": " + c.CancelReasonNote,
                CreatedAt = c.CancelledAt,
                ShiftId = c.ShiftId ?? (shift != null ? shift.Id : null),
                ShiftNumber = c.Shift != null ? c.Shift.ShiftNumber : shift != null ? shift.ShiftNumber : null,
                ActorName = c.CancelledByName,
                Source = c.OrderItemId == null ? PosLogSources.Order : PosLogSources.Item
            });
        }

        private IQueryable<PosLogEntryDto> BuildWasteQuery(
            Guid tenantId,
            PosLogQueryDto query,
            Guid currentUserId,
            bool isAdmin,
            CashierBalanceShift? shift)
        {
            var logs = _context.WasteLogs
                .AsNoTracking()
                .Where(w => w.TenantId == tenantId &&
                            w.Type == WasteLogType.Waste &&
                            w.SourceOrderId != null);

            if (!isAdmin)
                logs = logs.Where(w => w.CashierActionById == currentUserId || w.LoggedById == currentUserId);

            if (IsToday(query.Date))
            {
                var todayStart = DateTime.UtcNow.Date;
                var todayEnd = todayStart.AddDays(1);
                logs = logs.Where(w => (w.WasteDate ?? w.CreatedAt) >= todayStart && (w.WasteDate ?? w.CreatedAt) < todayEnd);
            }

            if (shift != null)
            {
                var shiftEnd = shift.ClosedAt ?? DateTime.UtcNow;
                logs = logs.Where(w =>
                    w.ShiftId == shift.Id ||
                    (w.ShiftId == null &&
                     (w.CashierActionById == shift.CashierId || w.LoggedById == shift.CashierId) &&
                     (w.WasteDate ?? w.CreatedAt) >= shift.OpenedAt &&
                     (w.WasteDate ?? w.CreatedAt) <= shiftEnd));
            }

            return logs.Select(w => new PosLogEntryDto
            {
                Id = w.Id,
                Type = PosLogTypes.Waste,
                OrderId = w.SourceOrderId,
                OrderNumber = w.SourceOrder != null ? w.SourceOrder.OrderNumber : null,
                ItemId = w.ItemId,
                OrderItemId = w.SourceOrderItemId,
                ItemName = w.ItemName,
                Quantity = w.Quantity,
                Amount = w.Amount > 0
                    ? w.Amount
                    : w.SourceOrderItemId.HasValue && w.SourceOrder != null
                        ? w.SourceOrder.OrderItems
                            .Where(oi => oi.Id == w.SourceOrderItemId.Value)
                            .Select(oi => (decimal?)(((oi.IsComplimentary ? 0m : oi.Price + oi.Modifiers.Sum(m => m.Price * m.Quantity)) * oi.Quantity) * (100m - w.SourceOrder.DiscountPercentage) / 100m))
                            .FirstOrDefault() ?? 0m
                        : 0m,
                Reason = w.Reason,
                CreatedAt = w.WasteDate ?? w.CreatedAt,
                ShiftId = w.ShiftId ?? (shift != null ? shift.Id : null),
                ShiftNumber = w.Shift != null ? w.Shift.ShiftNumber : shift != null ? shift.ShiftNumber : null,
                ActorName = w.CashierActionByName ?? w.LoggedByName,
                Source = w.SourceOrderItemId == null ? PosLogSources.Order : PosLogSources.Item
            });
        }

        private static bool IsCancelIncluded(string? type)
            => string.IsNullOrWhiteSpace(type) ||
               string.Equals(type, PosLogTypes.Cancel, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type, PosLogTypeFilters.Cancel, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type, PosLogTypeFilters.Cancelled, StringComparison.OrdinalIgnoreCase);

        private static bool IsWasteIncluded(string? type)
            => string.IsNullOrWhiteSpace(type) ||
               string.Equals(type, PosLogTypes.Waste, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type, PosLogTypeFilters.Waste, StringComparison.OrdinalIgnoreCase);

        private static bool IsToday(string? date)
            => !string.Equals(date, PosLogDateFilters.All, StringComparison.OrdinalIgnoreCase);
    }
}
