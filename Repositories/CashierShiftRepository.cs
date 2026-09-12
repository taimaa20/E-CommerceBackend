using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class CashierShiftRepository : ICashierShiftRepository
    {
        private readonly PosDbContext _context;

        public CashierShiftRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<CashierBalanceShift>> GetShiftsAsync(
            Guid tenantId,
            Guid branchId,
            CashierShiftQueryDto query,
            bool isAdmin,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var shifts = _context.CashierBalanceShifts
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.BranchId == branchId);

            if (!isAdmin)
                shifts = shifts.Where(s => s.CashierId == currentUserId);
            else if (query.CashierId.HasValue)
                shifts = shifts.Where(s => s.CashierId == query.CashierId.Value);

            if (query.IsClosed.HasValue)
            {
                shifts = query.IsClosed.Value
                    ? shifts.Where(s => s.ClosedAt != null)
                    : shifts.Where(s => s.ClosedAt == null);
            }

            shifts = CashierShiftQueryHelper.ApplyDateRange(shifts, query);

            return await shifts
                .OrderByDescending(s => s.OpenedAt)
                .ThenByDescending(s => s.Id)
                .ToListAsync(cancellationToken);
        }

        public Task<List<CashierShiftOwnerOptionDto>> GetShiftOwnerOptionsAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            return _context.Users
                .AsNoTracking()
                .Where(u => u.TenantId == tenantId
                    && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin || u.Role == UserRole.Cashier))
                .OrderBy(u => u.FullName ?? u.Username)
                .Select(u => new CashierShiftOwnerOptionDto
                {
                    UserId = u.Id,
                    Username = u.Username,
                    FullName = u.FullName ?? u.Username,
                    Role = u.Role.ToString()
                })
                .ToListAsync(cancellationToken);
        }

        public Task<CashierBalanceShift?> GetShiftByIdAsync(
            Guid tenantId,
            Guid branchId,
            Guid shiftId,
            CancellationToken cancellationToken = default)
        {
            return _context.CashierBalanceShifts
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.BranchId == branchId && s.Id == shiftId, cancellationToken);
        }

        public Task<CashierBalanceShift?> GetActiveShiftAsync(
            Guid tenantId,
            Guid branchId,
            Guid cashierId,
            CancellationToken cancellationToken = default)
        {
            return _context.CashierBalanceShifts
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.BranchId == branchId && s.CashierId == cashierId && s.ClosedAt == null)
                .OrderByDescending(s => s.OpenedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<CashierBalanceShift?> GetShiftByOpenTransactionIdAsync(
            Guid tenantId,
            Guid branchId,
            string clientActionId,
            CancellationToken cancellationToken = default)
        {
            return _context.CashierBalanceShifts
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.TenantId == tenantId && s.BranchId == branchId && s.OpenTransactionId == clientActionId,
                    cancellationToken);
        }

        public Task<User?> GetCashierByIdAsync(
            Guid tenantId,
            Guid cashierId,
            CancellationToken cancellationToken = default)
        {
            return _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == cashierId, cancellationToken);
        }

        public Task<bool> HasActiveShiftAsync(
            Guid tenantId,
            Guid branchId,
            Guid cashierId,
            CancellationToken cancellationToken = default)
        {
            return _context.CashierBalanceShifts
                .AnyAsync(s => s.TenantId == tenantId && s.BranchId == branchId && s.CashierId == cashierId && s.ClosedAt == null, cancellationToken);
        }

        public Task<bool> HasAnyActiveShiftAsync(
            Guid tenantId,
            Guid branchId,
            CancellationToken cancellationToken = default)
        {
            return _context.CashierBalanceShifts
                .AnyAsync(s => s.TenantId == tenantId && s.BranchId == branchId && s.ClosedAt == null, cancellationToken);
        }

        public Task<bool> HasActiveShiftForDeviceAsync(
            Guid tenantId,
            Guid branchId,
            string posDeviceId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(posDeviceId)) return Task.FromResult(false);
            return _context.CashierBalanceShifts
                .AnyAsync(s => s.TenantId == tenantId
                            && s.BranchId == branchId
                            && s.PosDeviceId == posDeviceId
                            && s.ClosedAt == null,
                          cancellationToken);
        }

        public async Task<ShiftDashboardCounts> GetShiftDashboardCountsAsync(
            Guid tenantId,
            Guid branchId,
            Guid cashierId,
            DateTime windowStartUtc,
            DateTime windowEndUtc,
            CancellationToken cancellationToken = default)
        {
            // Scope to the cashier's own orders only — either they took it
            // (WaiterId) or they processed payment for it (PaidByUserId). A
            // colleague's parallel shift on the same tenant is excluded.
            var orders = _context.Orders
                .AsNoTracking()
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && o.CreatedAt >= windowStartUtc
                         && o.CreatedAt <  windowEndUtc
                         && (o.WaiterId == cashierId || o.PaidByUserId == cashierId));

            var total      = await orders.CountAsync(cancellationToken);
            var revenue    = await orders.WherePaid()
                                         .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

            // Unified completion — matches Helpers/OrderCompletion.IsCompleted so
            // the dashboard "Completed" KPI reflects the same definition the shift
            // close validator uses. Includes Paid+Served partner orders that
            // never explicitly transition to Status=Completed.
            var completed  = await orders.CountAsync(Helpers.OrderCompletion.IsCompletedExpr, cancellationToken);
            var served     = await orders.CountAsync(o => o.Status == OrderStatus.Served,    cancellationToken);
            var ready      = await orders.CountAsync(o => o.Status == OrderStatus.Ready,     cancellationToken);
            var preparing  = await orders.CountAsync(o => o.Status == OrderStatus.Preparing, cancellationToken);
            var pending    = await orders.CountAsync(o => o.Status == OrderStatus.New,       cancellationToken);
            var paid       = await orders.CountAsync(o => o.Status == OrderStatus.Paid,      cancellationToken);
            var cancelled  = await orders.CountAsync(o => o.Status == OrderStatus.Cancelled, cancellationToken);

            var delivery   = await orders.CountAsync(o => o.OrderType == OrderType.Delivery, cancellationToken);
            var takeaway   = await orders.CountAsync(o => o.OrderType == OrderType.Takeaway, cancellationToken);
            var dineIn     = await orders.CountAsync(o => o.OrderType == OrderType.DineIn,   cancellationToken);
            var partner    = await orders.CountAsync(o => o.OrderSource == OrderSource.Talabat
                                                       || o.OrderSource == OrderSource.DeliveryPartner,
                                                     cancellationToken);
            var qr         = await orders.CountAsync(o => o.OrderSource == OrderSource.Mobile, cancellationToken);

            var refunded = await _context.RefundLogs
                .AsNoTracking()
                .CountAsync(r => r.TenantId == tenantId
                              && r.BranchId == branchId
                              && r.CreatedAt >= windowStartUtc
                              && r.CreatedAt <  windowEndUtc
                              && r.ProcessedById == cashierId,
                            cancellationToken);

            return new ShiftDashboardCounts
            {
                TotalOrders     = total,
                TotalRevenue    = revenue,
                CompletedOrders = completed,
                ServedOrders    = served,
                ReadyOrders     = ready,
                PreparingOrders = preparing,
                PendingOrders   = pending,
                PaidOrders      = paid,
                CancelledOrders = cancelled,
                RefundedOrders  = refunded,
                DeliveryOrders  = delivery,
                PartnerOrders   = partner,
                TakeawayOrders  = takeaway,
                DineInOrders    = dineIn,
                QrOrders        = qr,
            };
        }

        public async Task<ShiftValidationCounts> GetShiftValidationCountsAsync(
            Guid tenantId,
            Guid branchId,
            Guid cashierId,
            DateTime windowStartUtc,
            DateTime windowEndUtc,
            CancellationToken cancellationToken = default)
        {
            // Single round-trip — counts grouped in the database, not in memory.
            // Window matches CreatedAt (creation time) so unpaid/preparing orders
            // are correctly attributed to the shift in which they were taken.
            // Per-cashier scope (WaiterId OR PaidByUserId) — a colleague's
            // parallel shift's open orders do not block this cashier's close.
            var orders = _context.Orders
                .AsNoTracking()
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && o.CreatedAt >= windowStartUtc
                         && o.CreatedAt <  windowEndUtc
                         && (o.WaiterId == cashierId || o.PaidByUserId == cashierId));

            var newCnt        = await orders.CountAsync(o => o.Status == OrderStatus.New,        cancellationToken);
            var preparingCnt  = await orders.CountAsync(o => o.Status == OrderStatus.Preparing,  cancellationToken);
            var readyCnt      = await orders.CountAsync(o => o.Status == OrderStatus.Ready,      cancellationToken);
            var servedCnt     = await orders.CountAsync(o => o.Status == OrderStatus.Served,     cancellationToken);
            var paidCnt       = await orders.CountAsync(o => o.Status == OrderStatus.Paid,       cancellationToken);

            // Use PaidAt (financial timestamp) for unpaid semantics — an order may
            // legitimately have Status=Served while PaidAt is already set.
            var unpaid = await orders.CountAsync(o =>
                o.PaidAt == null
             && o.Status != OrderStatus.Cancelled, cancellationToken);

            // Fulfillment gates are payment-independent. Once an order is paid its
            // Status stays Paid while the kitchen is still working (see
            // OrdersController.MarkOrderAsReady's keepPaidStatus), so readiness /
            // served / completed are derived from the real fulfillment signals —
            // item readiness and the Served/Completed states — not from Status
            // alone. Payment has its own gate (UnpaidCount above). This is what
            // makes a settled-at-creation partner credit order block the close
            // until the kitchen actually prepares it.
            var notReady = await orders.CountAsync(o =>
                o.Status != OrderStatus.Cancelled
             && o.Status != OrderStatus.Completed
             && o.OrderItems.Any(i => !i.IsReady), cancellationToken);

            var notServed = await orders.CountAsync(o =>
                o.Status != OrderStatus.Cancelled
             && o.Status != OrderStatus.Served
             && o.Status != OrderStatus.Completed, cancellationToken);

            // Ready and Paid are independent flags, not terminal fulfillment.
            // Takeaway remains open until pickup (Completed); other channels
            // remain open until handoff (Served) or explicit completion.
            var notCompleted = await orders.CountAsync(
                Helpers.OrderCompletion.IsOpenExpr,
                cancellationToken);

            // PendingDelivery is intentionally NOT computed per-OrderType anymore.
            // The unified completion check above already covers Delivery/Partner
            // orders identically to DineIn/Takeaway. Reported as 0 for back-compat
            // with the DTO shape — UI hides the row when count is zero.
            var pendingDelivery = 0;

            // Pending cancellation requests scoped to this cashier — either they
            // raised the cancellation themselves (CancelledById) or the underlying
            // order belongs to them (Order.WaiterId / PaidByUserId).
            var pendingCancel = await _context.CancelLogs
                .AsNoTracking()
                .CountAsync(c => c.TenantId == tenantId
                              && c.BranchId == branchId
                              && c.CreatedAt >= windowStartUtc
                              && c.CreatedAt <  windowEndUtc
                              && c.ApprovalStatus == CancellationApprovalStatus.PendingKitchenApproval
                              && (c.CancelledById == cashierId
                                  || c.Order.WaiterId == cashierId
                                  || c.Order.PaidByUserId == cashierId),
                            cancellationToken);

            return new ShiftValidationCounts
            {
                NewCount                 = newCnt,
                PreparingCount           = preparingCnt,
                ReadyCount               = readyCnt,
                ServedCount              = servedCnt,
                PaidCount                = paidCnt,
                PendingDeliveryCount     = pendingDelivery,
                PendingCancellationCount = pendingCancel,
                UnpaidCount              = unpaid,
                NotReadyCount            = notReady,
                NotServedCount           = notServed,
                NotCompletedCount        = notCompleted,
            };
        }

        public async Task<int> GetNextShiftNumberAsync(
            Guid tenantId,
            Guid branchId,
            Guid cashierId,
            CancellationToken cancellationToken = default)
        {
            var currentMax = await _context.CashierBalanceShifts
                .Where(s => s.TenantId == tenantId && s.BranchId == branchId && s.CashierId == cashierId)
                .MaxAsync(s => (int?)s.ShiftNumber, cancellationToken);

            return (currentMax ?? 0) + 1;
        }

        public Task<SystemSettings?> GetSystemSettingsAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            return _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        }

        public async Task<List<CashierShiftLiveMetrics>> GetLiveMetricsAsync(
            Guid tenantId,
            Guid branchId,
            IReadOnlyCollection<Guid> shiftIds,
            DateTime cutoffUtc,
            CancellationToken cancellationToken = default)
        {
            if (shiftIds.Count == 0)
                return new List<CashierShiftLiveMetrics>();

            var shiftRows = await _context.CashierBalanceShifts
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.BranchId == branchId && shiftIds.Contains(s.Id))
                .Select(s => new
                {
                    s.Id,
                    s.OpeningBalance
                })
                .ToListAsync(cancellationToken);

            var orderRows = await GetPaidOrderMetricRowsAsync(tenantId, branchId, shiftIds, cutoffUtc, cancellationToken);
            var paymentRows = await GetPaidPaymentMetricRowsAsync(tenantId, branchId, shiftIds, cutoffUtc, cancellationToken);
            var refundTotals = await GetRefundTotalsAsync(tenantId, branchId, shiftIds, cutoffUtc, cancellationToken);
            var expenseAmounts = await GetExpenseAmountsAsync(tenantId, branchId, shiftIds, cancellationToken);

            return shiftRows
                .Select(shift =>
                {
                    var paidOrders = orderRows.Where(o => o.ShiftId == shift.Id).ToList();
                    var paidPayments = paymentRows.Where(p => p.ShiftId == shift.Id).ToList();
                    var legacyOrders = paidOrders.Where(o => !o.HasPayments).ToList();
                    var cashCollected = GetCashCollected(paidPayments, legacyOrders);
                    var cardCollected = GetCardCollected(paidPayments, legacyOrders);
                    var deliveryCost = paidOrders.Sum(o => o.ActualDeliveryCost);
                    var expenses = expenseAmounts.GetValueOrDefault(shift.Id, ShiftExpenseAmounts.Empty);

                    return new CashierShiftLiveMetrics
                    {
                        ShiftId = shift.Id,
                        PaidOrderCount = paidOrders.Count,
                        OrdersTotal = paidOrders.Sum(o => o.TotalAmount),
                        CashOrdersTotal = cashCollected,
                        CardOrdersTotal = cardCollected,
                        RefundsTotal = refundTotals.GetValueOrDefault(shift.Id),
                        // Documented cash payouts leave the drawer, so they are part
                        // of the expectation — not a shortage discovered at close.
                        ExpectedBalance = shift.OpeningBalance + cashCollected - deliveryCost - expenses.CashExpensesTotal,
                        ExpenseCount = expenses.ExpenseCount,
                        ExpensesTotal = expenses.ExpensesTotal,
                        CashExpensesTotal = expenses.CashExpensesTotal,
                        NonCashExpensesTotal = expenses.NonCashExpensesTotal,
                        FoodRevenue = paidOrders.Sum(o => o.EffectiveFoodRevenue),
                        DeliveryCollected = paidOrders.Sum(o => o.CustomerDeliveryFee),
                        DeliveryCost = deliveryCost,
                        DeliveryProfit = paidOrders.Sum(o => o.DeliveryMargin),
                        MarketplaceFees = paidOrders.Sum(o => o.MarketplaceDeliveryFee),
                        MarketplaceServiceFees = paidOrders.Sum(o => o.MarketplaceServiceFee),
                        NetRestaurantRevenue = paidOrders.Sum(o => o.EffectiveNetRestaurantRevenue),
                        PartnerSalesBreakdown = BuildPartnerSalesBreakdown(paidOrders),
                        PaymentMethodsBreakdown = BuildPaymentMethodBreakdown(paidPayments, legacyOrders)
                    };
                })
                .ToList();
        }

        public async Task<Dictionary<Guid, ShiftExpenseAmounts>> GetExpenseAmountsAsync(
            Guid tenantId,
            Guid branchId,
            IReadOnlyCollection<Guid> shiftIds,
            CancellationToken cancellationToken = default)
        {
            if (shiftIds.Count == 0)
                return new Dictionary<Guid, ShiftExpenseAmounts>();

            // Expenses are stamped with the shift id at write time, so no time-window
            // join is needed and a closed shift's totals can never drift.
            var rows = await _context.ExpenseInvoices
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId
                         && e.BranchId == branchId
                         && e.CashierShiftId != null
                         && shiftIds.Contains(e.CashierShiftId.Value)
                         && e.Status != ExpenseInvoiceStatus.Cancelled)
                .GroupBy(e => e.CashierShiftId!.Value)
                .Select(g => new
                {
                    ShiftId = g.Key,
                    Count = g.Count(),
                    Total = g.Sum(e => e.TotalAmount),
                    Cash = g.Sum(e => e.PaymentMethod == ExpensePaymentMethod.Cash ? e.TotalAmount : 0m)
                })
                .ToListAsync(cancellationToken);

            return rows.ToDictionary(
                r => r.ShiftId,
                r => new ShiftExpenseAmounts
                {
                    ExpenseCount = r.Count,
                    ExpensesTotal = r.Total,
                    CashExpensesTotal = r.Cash,
                    NonCashExpensesTotal = r.Total - r.Cash
                });
        }

        private async Task<List<PaidOrderMetricRow>> GetPaidOrderMetricRowsAsync(
            Guid tenantId,
            Guid branchId,
            IReadOnlyCollection<Guid> shiftIds,
            DateTime cutoffUtc,
            CancellationToken cancellationToken)
        {
            return await (
                from shift in GetMetricShifts(tenantId, branchId, shiftIds)
                from order in _context.Orders.AsNoTracking()
                where order.TenantId == shift.TenantId &&
                      order.BranchId == shift.BranchId &&
                      order.PaidByUserId == shift.CashierId &&
                      order.PaidAt.HasValue &&
                      order.PaidAt.Value >= shift.OpenedAt &&
                      order.PaidAt.Value <= (shift.ClosedAt ?? cutoffUtc) &&
                      order.Status != OrderStatus.Cancelled
                select new PaidOrderMetricRow(
                    shift.Id,
                    order.Id,
                    order.TotalAmount,
                    order.FoodSubtotal,
                    order.CustomerDeliveryFee,
                    order.ActualDeliveryCost,
                    order.DeliveryMargin,
                    order.MarketplaceDeliveryFee,
                    order.MarketplaceServiceFee,
                    order.NetRestaurantRevenue,
                    order.PaymentMethod,
                    order.Payments.Any(),
                    order.OrderSource,
                    order.DeliveryPartnerId,
                    order.DeliveryPartnerName,
                    order.DeliveryPartnerCode))
                .ToListAsync(cancellationToken);
        }

        private async Task<List<PaidPaymentMetricRow>> GetPaidPaymentMetricRowsAsync(
            Guid tenantId,
            Guid branchId,
            IReadOnlyCollection<Guid> shiftIds,
            DateTime cutoffUtc,
            CancellationToken cancellationToken)
        {
            return await (
                from shift in GetMetricShifts(tenantId, branchId, shiftIds)
                from order in _context.Orders.AsNoTracking()
                where order.TenantId == shift.TenantId &&
                      order.BranchId == shift.BranchId &&
                      order.PaidByUserId == shift.CashierId &&
                      order.PaidAt.HasValue &&
                      order.PaidAt.Value >= shift.OpenedAt &&
                      order.PaidAt.Value <= (shift.ClosedAt ?? cutoffUtc) &&
                      order.Status != OrderStatus.Cancelled
                from payment in order.Payments
                select new PaidPaymentMetricRow(
                    shift.Id,
                    order.Id,
                    payment.Amount,
                    payment.Method,
                    payment.PaymentMethodName))
                .ToListAsync(cancellationToken);
        }

        private async Task<Dictionary<Guid, decimal>> GetRefundTotalsAsync(
            Guid tenantId,
            Guid branchId,
            IReadOnlyCollection<Guid> shiftIds,
            DateTime cutoffUtc,
            CancellationToken cancellationToken)
        {
            return await (
                from shift in GetMetricShifts(tenantId, branchId, shiftIds)
                from refund in _context.RefundLogs.AsNoTracking()
                where refund.TenantId == shift.TenantId &&
                      refund.BranchId == shift.BranchId &&
                      refund.ProcessedById == shift.CashierId &&
                      refund.ProcessedAt >= shift.OpenedAt &&
                      refund.ProcessedAt <= (shift.ClosedAt ?? cutoffUtc)
                group refund by shift.Id into g
                select new { ShiftId = g.Key, Total = g.Sum(r => r.RefundAmount) })
                .ToDictionaryAsync(x => x.ShiftId, x => x.Total, cancellationToken);
        }

        private IQueryable<CashierBalanceShift> GetMetricShifts(
            Guid tenantId,
            Guid branchId,
            IReadOnlyCollection<Guid> shiftIds)
        {
            return _context.CashierBalanceShifts
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.BranchId == branchId && shiftIds.Contains(s.Id));
        }

        private static decimal GetCashCollected(
            IEnumerable<PaidPaymentMetricRow> payments,
            IEnumerable<PaidOrderMetricRow> legacyOrders)
        {
            return payments
                .Where(p => OrderPaymentHelper.IsCashMethod(p.Method))
                .Sum(p => p.Amount) +
                legacyOrders
                    .Where(o => OrderPaymentHelper.IsCashMethod(o.PaymentMethod))
                    .Sum(o => o.TotalAmount);
        }

        private static decimal GetCardCollected(
            IEnumerable<PaidPaymentMetricRow> payments,
            IEnumerable<PaidOrderMetricRow> legacyOrders)
        {
            return payments
                .Where(p => !OrderPaymentHelper.IsCashMethod(p.Method))
                .Sum(p => p.Amount) +
                legacyOrders
                    .Where(o => !OrderPaymentHelper.IsCashMethod(o.PaymentMethod))
                    .Sum(o => o.TotalAmount);
        }

        private static List<CashierShiftPartnerSalesDto> BuildPartnerSalesBreakdown(IEnumerable<PaidOrderMetricRow> paidOrders)
        {
            // Group all paid orders by their actual source so the cashier closing screen can
            // surface POS / Talabat / DeliveryPartner-by-name (Jahez, Mrsool, ...) consistently.
            // Reuses the existing per-order metric rows — no extra queries, no calculation changes.
            return paidOrders
                .GroupBy(o => new
                {
                    o.OrderSource,
                    o.DeliveryPartnerId,
                    PartnerName = o.OrderSource == OrderSource.DeliveryPartner
                        ? (string.IsNullOrWhiteSpace(o.DeliveryPartnerName) ? "Delivery Partner" : o.DeliveryPartnerName)
                        : o.OrderSource == OrderSource.Talabat
                            ? "Talabat"
                            : "POS",
                    PartnerCode = o.OrderSource == OrderSource.DeliveryPartner ? o.DeliveryPartnerCode : null
                })
                .Select(g => new CashierShiftPartnerSalesDto
                {
                    DeliveryPartnerId = g.Key.DeliveryPartnerId,
                    PartnerName = g.Key.PartnerName,
                    PartnerCode = g.Key.PartnerCode,
                    OrderCount = g.Count(),
                    TotalAmount = g.Sum(o => o.TotalAmount),
                    FoodRevenue = g.Sum(o => o.EffectiveFoodRevenue),
                    DeliveryCollected = g.Sum(o => o.CustomerDeliveryFee),
                    DeliveryCost = g.Sum(o => o.ActualDeliveryCost),
                    DeliveryProfit = g.Sum(o => o.DeliveryMargin),
                    MarketplaceFees = g.Sum(o => o.MarketplaceDeliveryFee),
                    MarketplaceServiceFees = g.Sum(o => o.MarketplaceServiceFee),
                    NetRevenue = g.Sum(o => o.EffectiveNetRestaurantRevenue)
                })
                .OrderByDescending(row => row.TotalAmount)
                .ToList();
        }

        private static List<CashierShiftPaymentMethodBreakdownDto> BuildPaymentMethodBreakdown(
            IEnumerable<PaidPaymentMetricRow> payments,
            IEnumerable<PaidOrderMetricRow> legacyOrders)
        {
            var paymentRows = payments.Select(p => new PaymentMethodMetricRow(
                p.OrderId,
                ResolvePaymentMethodLabel(p),
                p.Amount,
                OrderPaymentHelper.IsCashMethod(p.Method)));

            var legacyRows = legacyOrders.Select(o => new PaymentMethodMetricRow(
                o.OrderId,
                ResolveLegacyPaymentMethodLabel(o.PaymentMethod),
                o.TotalAmount,
                OrderPaymentHelper.IsCashMethod(o.PaymentMethod)));

            return paymentRows
                .Concat(legacyRows)
                .GroupBy(row => new { row.Method, row.IsCash })
                .Select(g => new CashierShiftPaymentMethodBreakdownDto
                {
                    Method = g.Key.Method,
                    IsCash = g.Key.IsCash,
                    OrderCount = g.Select(row => row.OrderId).Distinct().Count(),
                    Amount = g.Sum(row => row.Amount)
                })
                .OrderBy(row => row.IsCash ? 0 : 1)
                .ThenByDescending(row => row.Amount)
                .ToList();
        }

        private static string ResolvePaymentMethodLabel(PaidPaymentMetricRow payment)
        {
            // Configured-method snapshot wins — real provider name (Visa, InstaPay, ...).
            if (!string.IsNullOrWhiteSpace(payment.PaymentMethodName))
                return payment.PaymentMethodName.Trim();

            // No snapshot → bucket legacy non-cash variants together so the
            // closing screen doesn't list "Card"/"Credit Card"/"Online" separately.
            return OrderPaymentHelper.ResolveLegacyBucket(payment.Method).Label;
        }

        private static string ResolveLegacyPaymentMethodLabel(string? paymentMethod)
            => OrderPaymentHelper.ResolveLegacyBucket(paymentMethod).Label;

        public Task AddAsync(CashierBalanceShift shift, CancellationToken cancellationToken = default)
            => _context.CashierBalanceShifts.AddAsync(shift, cancellationToken).AsTask();

        public void Remove(CashierBalanceShift shift)
            => _context.CashierBalanceShifts.Remove(shift);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);

        private sealed record PaidOrderMetricRow(
            Guid ShiftId,
            Guid OrderId,
            decimal TotalAmount,
            decimal FoodSubtotal,
            decimal CustomerDeliveryFee,
            decimal ActualDeliveryCost,
            decimal DeliveryMargin,
            decimal MarketplaceDeliveryFee,
            decimal MarketplaceServiceFee,
            decimal NetRestaurantRevenue,
            string? PaymentMethod,
            bool HasPayments,
            OrderSource OrderSource,
            Guid? DeliveryPartnerId,
            string? DeliveryPartnerName,
            string? DeliveryPartnerCode)
        {
            public decimal EffectiveFoodRevenue => FoodSubtotal > 0m ? FoodSubtotal : TotalAmount;
            public decimal EffectiveNetRestaurantRevenue => NetRestaurantRevenue > 0m ? NetRestaurantRevenue : TotalAmount;
        }

        private sealed record PaidPaymentMetricRow(
            Guid ShiftId,
            Guid OrderId,
            decimal Amount,
            string Method,
            string? PaymentMethodName);

        private sealed record PaymentMethodMetricRow(
            Guid OrderId,
            string Method,
            decimal Amount,
            bool IsCash);
    }
}
