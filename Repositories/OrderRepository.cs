using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using System.Linq.Expressions;

namespace RestaurantPos.Api.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly PosDbContext _context;

        public OrderRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<PaginatedResponse<OrderListItemDto>> GetOrdersAsync(
            OrderQueryFilter filter,
            bool isArabic,
            CancellationToken ct)
        {
            var query = ApplyFilters(filter);
            var totalCount = await query.CountAsync(ct);
            var rows = await ApplySort(query, filter)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(ProjectListRow(isArabic))
                .ToListAsync(ct);

            return new PaginatedResponse<OrderListItemDto>
            {
                Items = rows.Select(MapListItem).ToList(),
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<OrderListSummaryDto> GetOrdersSummaryAsync(OrderQueryFilter filter, CancellationToken ct)
        {
            var query = ApplyFilters(filter);
            var paidQuery = query.WherePaid();

            var partnerDiscountItems = BuildPartnerDiscountItemsQuery(query);

            return new OrderListSummaryDto
            {
                TotalCount = await query.CountAsync(ct),
                ActiveCount = await query.WhereActiveLifecycle().CountAsync(ct),
                PaidCount = await paidQuery.CountAsync(ct),
                Revenue = await paidQuery.SumAsync(o => (decimal?)(o.NetRestaurantRevenue > 0m ? o.NetRestaurantRevenue : o.TotalAmount), ct) ?? 0m,
                FoodRevenue = await paidQuery.SumAsync(o => (decimal?)(o.FoodSubtotal > 0m ? o.FoodSubtotal : o.TotalAmount), ct) ?? 0m,
                DeliveryCollected = await paidQuery.SumAsync(o => (decimal?)o.CustomerDeliveryFee, ct) ?? 0m,
                DeliveryCost = await paidQuery.SumAsync(o => (decimal?)o.ActualDeliveryCost, ct) ?? 0m,
                DeliveryProfit = await paidQuery.SumAsync(o => (decimal?)o.DeliveryMargin, ct) ?? 0m,
                MarketplaceFees = await paidQuery.SumAsync(o => (decimal?)o.MarketplaceDeliveryFee, ct) ?? 0m,
                MarketplaceServiceFees = await paidQuery.SumAsync(o => (decimal?)o.MarketplaceServiceFee, ct) ?? 0m,
                NetRestaurantRevenue = await paidQuery.SumAsync(o => (decimal?)(o.NetRestaurantRevenue > 0m ? o.NetRestaurantRevenue : o.TotalAmount), ct) ?? 0m,
                CostSharingTotalCommission = await paidQuery.SumAsync(o => (decimal?)o.CostSharingTotalCommission, ct) ?? 0m,
                CostSharingRestaurantShare = await paidQuery.SumAsync(o => (decimal?)o.CostSharingRestaurantShare, ct) ?? 0m,
                CostSharingCounterpartyShare = await paidQuery.SumAsync(o => (decimal?)o.CostSharingCounterpartyShare, ct) ?? 0m,
                NetRevenueAfterCostSharing = await paidQuery.SumAsync(o => (decimal?)((o.NetRestaurantRevenue > 0m ? o.NetRestaurantRevenue : o.TotalAmount) - o.CostSharingRestaurantShare), ct) ?? 0m,
                VoucherCount = await query.CountAsync(o => o.IsVoucherApplied, ct),
                VoucherDiscountTotal = await query.SumAsync(o => (decimal?)o.VoucherDiscountAmount, ct) ?? 0m,
                PartnerGrossRevenue = await partnerDiscountItems
                    .SumAsync(i => (decimal?)(i.PartnerOriginalUnitPrice ?? i.Price) * i.Quantity, ct) ?? 0m,
                PartnerDiscountRevenue = await partnerDiscountItems
                    .SumAsync(i => (decimal?)i.PartnerDiscountedUnitPrice * i.Quantity, ct) ?? 0m,
                PartnerDiscountAmount = await partnerDiscountItems
                    .SumAsync(i => (decimal?)((i.PartnerOriginalUnitPrice ?? i.Price) - (i.PartnerDiscountedUnitPrice ?? 0m)) * i.Quantity, ct) ?? 0m,
                PartnerDiscountCount = await partnerDiscountItems.CountAsync(ct)
            };
        }

        public Task<List<OrderExportRowDto>> GetOrdersExportRowsAsync(OrderQueryFilter filter, CancellationToken ct)
        {
            return ApplySort(ApplyFilters(filter), filter)
                .Select(o => new OrderExportRowDto
                {
                    OrderNumber = o.DisplayOrderNumber ?? o.PublicOrderNumber ?? o.OrderNumber,
                    CreatedAt = o.CreatedAt,
                    TotalAmount = o.TotalAmount,
                    FoodSubtotal = o.FoodSubtotal,
                    CustomerDeliveryFee = o.CustomerDeliveryFee,
                    ActualDeliveryCost = o.ActualDeliveryCost,
                    DeliveryMargin = o.DeliveryMargin,
                    MarketplaceDeliveryFee = o.MarketplaceDeliveryFee,
                    MarketplaceServiceFee = o.MarketplaceServiceFee,
                    NetRestaurantRevenue = o.NetRestaurantRevenue > 0m ? o.NetRestaurantRevenue : o.TotalAmount,
                    CostSharingTotalCommission = o.CostSharingTotalCommission,
                    CostSharingRestaurantShare = o.CostSharingRestaurantShare,
                    CostSharingCounterpartyShare = o.CostSharingCounterpartyShare,
                    CostSharingNetSettlement = o.CostSharingNetSettlement,
                    PaymentMethod = o.Payments
                        .OrderByDescending(p => p.CreatedAt)
                        .Select(p => p.PaymentMethodName ?? p.Method)
                        .FirstOrDefault() ?? o.PaymentMethod,
                    PaymentReferenceNumber = o.Payments
                        .OrderByDescending(p => p.CreatedAt)
                        .Where(p => p.ReferenceNumber != null && p.ReferenceNumber != string.Empty)
                        .Select(p => p.ReferenceNumber)
                        .FirstOrDefault(),
                    CashierName = o.PaidByUser == null
                        ? null
                        : o.PaidByUser.FullName != null && o.PaidByUser.FullName != string.Empty
                            ? o.PaidByUser.FullName
                            : o.PaidByUser.Username,
                    OrderSource = o.OrderSource.ToString(),
                    TalabatOrderNumber = o.TalabatOrderNumber,
                    PartnerName = o.DeliveryPartnerName,
                    PartnerCode = o.DeliveryPartnerCode,
                    PartnerOrderNumber = o.PartnerOrderNumber,
                    Status = o.Status.ToString()
                })
                .ToListAsync(ct);
        }

        public Task<bool> OrderExistsAsync(Guid orderId, Guid tenantId, Guid branchId, CancellationToken ct)
            => _context.Orders
                .AsNoTracking()
                .AnyAsync(o => o.Id == orderId && o.TenantId == tenantId && o.BranchId == branchId, ct);

        public Task<List<PartnerPriceOverrideAuditDto>> GetPartnerPriceOverrideAuditsAsync(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct)
            => _context.PartnerPriceOverrideAudits
                .AsNoTracking()
                .Where(a => a.OrderId == orderId
                    && a.TenantId == tenantId
                    && _context.Orders.Any(o => o.Id == a.OrderId && o.TenantId == tenantId && o.BranchId == branchId))
                .OrderByDescending(a => a.UpdatedAt)
                .Select(a => new PartnerPriceOverrideAuditDto
                {
                    Id = a.Id,
                    OrderId = a.OrderId,
                    OrderItemId = a.OrderItemId,
                    OldPrice = a.OldPrice,
                    NewPrice = a.NewPrice,
                    OldDiscountAmount = a.OldDiscountAmount,
                    NewDiscountAmount = a.NewDiscountAmount,
                    ReasonCode = a.ReasonCode,
                    Reason = a.Reason,
                    Note = a.Note,
                    DeliveryPartnerId = a.DeliveryPartnerId,
                    DeliveryPartnerName = a.DeliveryPartnerName,
                    DeliveryPartnerNameAr = a.DeliveryPartnerNameAr,
                    DeliveryPartnerCode = a.DeliveryPartnerCode,
                    CorrectionMode = a.CorrectionMode,
                    OriginalTotal = a.OriginalTotal,
                    CorrectTotal = a.CorrectTotal,
                    DifferenceAmount = a.DifferenceAmount,
                    UpdatedBy = a.UpdatedBy,
                    UpdatedByName = _context.Users
                        .Where(u => u.Id == a.UpdatedBy && u.TenantId == tenantId)
                        .Select(u => u.FullName != null && u.FullName != string.Empty ? u.FullName : u.Username)
                        .FirstOrDefault(),
                    UpdatedAt = a.UpdatedAt
                })
                .ToListAsync(ct);

        public Task<OrderItem?> GetOrderItemForPartnerDiscountAsync(
            Guid orderId,
            Guid orderItemId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct)
            => _context.OrderItems
                .Include(i => i.Order)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(oi => oi.Modifiers)
                .Include(i => i.Order)
                    .ThenInclude(o => o.Payments)
                .Include(i => i.PartnerDiscountUpdatedByUser)
                .FirstOrDefaultAsync(i => i.Id == orderItemId
                    && i.OrderId == orderId
                    && i.TenantId == tenantId
                    && i.BranchId == branchId, ct);

        public Task<Order?> GetOrderForDeliveryPartnerPriceAdjustmentAsync(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct)
            => _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Modifiers)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId && o.BranchId == branchId, ct);

        public Task<Order?> GetOrderForTotalOverrideAsync(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct)
            => _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId && o.BranchId == branchId, ct);

        public Task<Order?> GetOrderForLifecycleAsync(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct)
            => _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(
                    o => o.Id == orderId && o.TenantId == tenantId && o.BranchId == branchId,
                    ct);

        public Task<User?> GetUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
            => _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);

        public async Task AddPartnerPriceOverrideAuditAsync(PartnerPriceOverrideAudit audit, CancellationToken ct)
            => await _context.PartnerPriceOverrideAudits.AddAsync(audit, ct);

        public Task SaveChangesAsync(CancellationToken ct)
            => _context.SaveChangesAsync(ct);

        private IQueryable<Order> ApplyFilters(OrderQueryFilter filter)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.TenantId == filter.TenantId);

            if (filter.BranchId.HasValue)
                query = query.Where(o => o.BranchId == filter.BranchId.Value);

            query = ApplyEnumFilters(query, filter);
            query = ApplyVoucherFilter(query, filter.VoucherApplied);
            query = ApplyDateFilters(query, filter);
            query = ApplyPaymentFilter(query, filter.Payment);
            return ApplySearchFilter(query, filter.Search);
        }

        private static IQueryable<Order> ApplyEnumFilters(IQueryable<Order> query, OrderQueryFilter filter)
        {
            if (filter.Status.HasValue)
                query = query.Where(o => o.Status == filter.Status.Value);

            if (filter.OrderSource.HasValue)
                query = query.Where(o => o.OrderSource == filter.OrderSource.Value);

            if (filter.PartnerId.HasValue)
                query = query.Where(o => o.DeliveryPartnerId == filter.PartnerId.Value);

            return filter.OrderType.HasValue
                ? query.Where(o => o.OrderType == filter.OrderType.Value)
                : query;
        }

        private static IQueryable<Order> ApplyDateFilters(IQueryable<Order> query, OrderQueryFilter filter)
        {
            if (filter.DateFrom.HasValue)
                query = query.Where(o => (o.PaidAt ?? o.CreatedAt) >= filter.DateFrom.Value);

            return filter.DateTo.HasValue
                ? query.Where(o => (o.PaidAt ?? o.CreatedAt) < filter.DateTo.Value)
                : query;
        }

        private static IQueryable<Order> ApplyVoucherFilter(IQueryable<Order> query, bool? voucherApplied)
        {
            return voucherApplied.HasValue
                ? query.Where(o => o.IsVoucherApplied == voucherApplied.Value)
                : query;
        }

        private static IQueryable<OrderItem> BuildPartnerDiscountItemsQuery(IQueryable<Order> query)
            => query
                .Where(o => o.OrderSource == OrderSource.Talabat || o.OrderSource == OrderSource.DeliveryPartner)
                .SelectMany(o => o.OrderItems)
                .Where(i => i.HasPartnerDiscountOverride && i.PartnerDiscountedUnitPrice.HasValue);

        private static IQueryable<Order> ApplyPaymentFilter(IQueryable<Order> query, string? payment)
        {
            if (string.IsNullOrWhiteSpace(payment))
                return query;

            var normalized = payment.Trim();
            var pattern = ToContainsPattern(normalized);
            // Legacy 'Cash'/'Card' substring + configurable method match by Code (exact) or Name (LIKE).
            return query.Where(o =>
                (o.PaymentMethod != null && EF.Functions.ILike(o.PaymentMethod, pattern)) ||
                o.Payments.Any(p =>
                    EF.Functions.ILike(p.Method, pattern) ||
                    (p.PaymentMethodCode != null && p.PaymentMethodCode == normalized) ||
                    (p.PaymentMethodName != null && EF.Functions.ILike(p.PaymentMethodName, pattern))));
        }

        private static IQueryable<Order> ApplySearchFilter(IQueryable<Order> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return query;

            var pattern = ToContainsPattern(search);
            return query.Where(o =>
                EF.Functions.ILike(o.OrderNumber, pattern) ||
                (o.DisplayOrderNumber != null && EF.Functions.ILike(o.DisplayOrderNumber, pattern)) ||
                (o.PublicOrderNumber != null && EF.Functions.ILike(o.PublicOrderNumber, pattern)) ||
                (o.TalabatOrderNumber != null && EF.Functions.ILike(o.TalabatOrderNumber, pattern)) ||
                (o.TalabatCustomerName != null && EF.Functions.ILike(o.TalabatCustomerName, pattern)) ||
                (o.TalabatCustomerPhone != null && EF.Functions.ILike(o.TalabatCustomerPhone, pattern)) ||
                (o.DeliveryPartnerName != null && EF.Functions.ILike(o.DeliveryPartnerName, pattern)) ||
                (o.DeliveryPartnerCode != null && EF.Functions.ILike(o.DeliveryPartnerCode, pattern)) ||
                (o.PartnerOrderNumber != null && EF.Functions.ILike(o.PartnerOrderNumber, pattern)) ||
                (o.PartnerCustomerName != null && EF.Functions.ILike(o.PartnerCustomerName, pattern)) ||
                (o.PartnerCustomerPhone != null && EF.Functions.ILike(o.PartnerCustomerPhone, pattern)) ||
                EF.Functions.ILike(o.TableName, pattern) ||
                (o.CustomerPhone != null && EF.Functions.ILike(o.CustomerPhone, pattern)) ||
                (o.CustomerName != null && EF.Functions.ILike(o.CustomerName, pattern)) ||
                (o.Table != null && (EF.Functions.ILike(o.Table.Name, pattern) ||
                    (o.Table.NameAr != null && EF.Functions.ILike(o.Table.NameAr, pattern)))) ||
                o.Payments.Any(p => p.ReferenceNumber != null && EF.Functions.ILike(p.ReferenceNumber, pattern)) ||
                o.OrderItems.Any(i => EF.Functions.ILike(i.ProductName, pattern)));
        }

        private static IOrderedQueryable<Order> ApplySort(IQueryable<Order> query, OrderQueryFilter filter)
            => filter.SortDescending ? query.OrderByDescending(o => o.CreatedAt) : query.OrderBy(o => o.CreatedAt);

        private Expression<Func<Order, OrderListRow>> ProjectListRow(bool isArabic)
            => o => new OrderListRow
            {
                Id = o.Id,
                // Mirrors OrdersController.ResolveOrderNumber: prefer the new
                // monthly DisplayOrderNumber, then the legacy public/offline
                // number, then the raw internal one (historical orders).
                OrderNumber = o.DisplayOrderNumber ?? o.PublicOrderNumber ?? o.OrderNumber,
                TicketId = o.TicketId,
                TableName = o.OrderSource == OrderSource.DeliveryPartner
                    ? (isArabic && o.DeliveryPartnerNameAr != null && o.DeliveryPartnerNameAr != string.Empty
                        ? o.DeliveryPartnerNameAr
                        : o.DeliveryPartnerName ?? o.DeliveryPartnerCode ?? "DELIVERY PARTNER")
                    : o.OrderSource == OrderSource.Talabat
                    ? "TALABAT"
                    : o.TableId == null && o.OrderType == OrderType.Takeaway
                    ? "TAKEAWAY"
                    : o.Table != null
                        ? isArabic && o.Table.NameAr != null && o.Table.NameAr != string.Empty ? o.Table.NameAr : o.Table.Name
                        : o.TableName,
                OrderSource = o.OrderSource,
                TalabatOrderNumber = o.TalabatOrderNumber,
                TalabatPickupTime = o.TalabatPickupTime,
                DeliveryPartnerId = o.DeliveryPartnerId,
                DeliveryPartnerName = o.DeliveryPartnerName,
                DeliveryPartnerNameAr = o.DeliveryPartnerNameAr,
                DeliveryPartnerCode = o.DeliveryPartnerCode,
                PartnerOrderNumber = o.PartnerOrderNumber,
                OrderType = o.OrderType,
                ItemCount = o.OrderItems.Select(i => (int?)i.Quantity).Sum() ?? 0,
                TotalAmount = o.TotalAmount,
                FoodSubtotal = o.FoodSubtotal,
                CustomerDeliveryFee = o.CustomerDeliveryFee,
                ActualDeliveryCost = o.ActualDeliveryCost,
                DeliveryMargin = o.DeliveryMargin,
                MarketplaceDeliveryFee = o.MarketplaceDeliveryFee,
                MarketplaceServiceFee = o.MarketplaceServiceFee,
                NetRestaurantRevenue = o.NetRestaurantRevenue > 0m ? o.NetRestaurantRevenue : o.TotalAmount,
                CostSharingTotalCommission = o.CostSharingTotalCommission,
                CostSharingRestaurantShare = o.CostSharingRestaurantShare,
                CostSharingCounterpartyShare = o.CostSharingCounterpartyShare,
                CostSharingNetSettlement = o.CostSharingNetSettlement,
                Status = o.Status,
                PaymentMethod = o.PaymentMethod,
                CashierName = o.PaidByUser == null
                    ? null
                    : o.PaidByUser.FullName != null && o.PaidByUser.FullName != string.Empty
                        ? o.PaidByUser.FullName
                        : o.PaidByUser.Username,
                IsVoucherApplied = o.IsVoucherApplied,
                VoucherDiscountAmount = o.VoucherDiscountAmount,
                VoucherAppliedAt = o.VoucherAppliedAt,
                PaidAmountFromPayments = o.Payments.Select(p => (decimal?)p.Amount).Sum(),
                PaidAt = o.PaidAt,
                CustomerPhone = o.CustomerPhone,
                CustomerNameSnapshot = o.CustomerName,
                // Latest payment snapshot for display (real method name + reference).
                LatestPaymentMethodName = o.Payments
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => p.PaymentMethodName)
                    .FirstOrDefault(),
                LatestPaymentMethodNameAr = o.Payments
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => p.PaymentMethodNameAr)
                    .FirstOrDefault(),
                LatestPaymentMethodCode = o.Payments
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => p.PaymentMethodCode)
                    .FirstOrDefault(),
                LatestPaymentReferenceNumber = o.Payments
                    .OrderByDescending(p => p.CreatedAt)
                    .Where(p => p.ReferenceNumber != null && p.ReferenceNumber != string.Empty)
                    .Select(p => p.ReferenceNumber)
                    .FirstOrDefault(),
                PendingPaymentAdjustmentId = _context.PaymentAdjustments
                    .Where(adjustment => adjustment.OrderId == o.Id &&
                        adjustment.Status == PaymentAdjustmentStatus.Pending)
                    .Select(adjustment => (Guid?)adjustment.Id)
                    .FirstOrDefault(),
                PendingCancellationId = _context.CancelLogs
                    .Where(c => c.OrderId == o.Id &&
                        c.OrderItemId == null &&
                        c.RequiresKitchenApproval &&
                        c.ApprovalStatus == CancellationApprovalStatus.PendingKitchenApproval)
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefault(),
                CreatedAt = o.CreatedAt,
                LastUpdatedAt = o.LastUpdatedAt,
                HasPriceDifference = o.HasPriceDifference,
                HasPartnerDiscountOverride = o.HasPriceDifference ||
                    o.OrderItems.Any(i => i.HasPartnerDiscountOverride) ||
                    _context.PartnerPriceOverrideAudits.Any(a =>
                        a.TenantId == o.TenantId &&
                        a.OrderId == o.Id &&
                        a.OrderItemId == Guid.Empty)
            };

        private static OrderListItemDto MapListItem(OrderListRow row)
        {
            var paidStatus = row.Status != OrderStatus.Cancelled &&
                (row.Status == OrderStatus.Paid ||
                 row.Status == OrderStatus.Completed ||
                 row.PaidAt.HasValue ||
                 !string.IsNullOrWhiteSpace(row.PaymentMethod));
            var paidAmount = row.PaidAmountFromPayments.HasValue && row.PaidAmountFromPayments.Value > 0
                ? OrderPaymentHelper.RoundCurrency(row.PaidAmountFromPayments.Value)
                : paidStatus
                    ? OrderPaymentHelper.RoundCurrency(row.TotalAmount)
                    : 0m;

            var remainingAmount = OrderPaymentHelper.RoundCurrency(Math.Max(0m, row.TotalAmount - paidAmount));
            var isPaid = row.Status != OrderStatus.Cancelled &&
                remainingAmount <= 0 &&
                (paidAmount > 0 || paidStatus || row.IsVoucherApplied);

            return new OrderListItemDto
            {
                Id = row.Id,
                OrderNumber = row.OrderNumber,
                TicketId = row.TicketId,
                TableName = row.TableName,
                OrderType = row.OrderType.ToString(),
                OrderSource = row.OrderSource.ToString(),
                TalabatOrderNumber = row.TalabatOrderNumber,
                TalabatPickupTime = row.TalabatPickupTime,
                DeliveryPartnerId = row.DeliveryPartnerId,
                DeliveryPartnerName = row.DeliveryPartnerName,
                DeliveryPartnerNameAr = row.DeliveryPartnerNameAr,
                DeliveryPartnerCode = row.DeliveryPartnerCode,
                PartnerOrderNumber = row.PartnerOrderNumber,
                ItemCount = row.ItemCount,
                TotalItemsCount = row.ItemCount,
                TotalAmount = row.TotalAmount,
                FoodSubtotal = row.FoodSubtotal,
                CustomerDeliveryFee = row.CustomerDeliveryFee,
                ActualDeliveryCost = row.ActualDeliveryCost,
                DeliveryMargin = row.DeliveryMargin,
                MarketplaceDeliveryFee = row.MarketplaceDeliveryFee,
                MarketplaceServiceFee = row.MarketplaceServiceFee,
                NetRestaurantRevenue = row.NetRestaurantRevenue,
                CostSharingTotalCommission = row.CostSharingTotalCommission,
                CostSharingRestaurantShare = row.CostSharingRestaurantShare,
                CostSharingCounterpartyShare = row.CostSharingCounterpartyShare,
                CostSharingNetSettlement = row.CostSharingNetSettlement,
                Status = row.Status.ToString(),
                IsPaid = isPaid,
                PaymentStatus = isPaid
                    ? OrderPaymentHelper.PaidStatus
                    : paidAmount <= 0
                    ? OrderPaymentHelper.PendingStatus
                    : OrderPaymentHelper.PartialStatus,
                PaymentMethod = row.PaymentMethod,
                PaymentMethodName = row.LatestPaymentMethodName,
                PaymentMethodNameAr = row.LatestPaymentMethodNameAr,
                PaymentMethodCode = row.LatestPaymentMethodCode,
                PaymentReferenceNumber = row.LatestPaymentReferenceNumber,
                CashierName = row.CashierName,
                IsVoucherApplied = row.IsVoucherApplied,
                VoucherDiscountAmount = row.VoucherDiscountAmount,
                VoucherAppliedAt = row.VoucherAppliedAt,
                PaidAmount = paidAmount,
                RemainingAmount = remainingAmount,
                CustomerPhone = row.CustomerPhone,
                // Walk-in / imported sales carry the name on the order itself (no Customer row).
                CustomerName = row.CustomerNameSnapshot,
                CreatedAt = row.CreatedAt,
                LastUpdatedAt = row.LastUpdatedAt,
                HasPendingPaymentAdjustment = row.PendingPaymentAdjustmentId.HasValue,
                PendingPaymentAdjustmentId = row.PendingPaymentAdjustmentId,
                HasPendingCancellationApproval = row.PendingCancellationId.HasValue,
                PendingCancellationId = row.PendingCancellationId,
                HasPriceDifference = row.HasPriceDifference,
                HasPartnerDiscountOverride = row.HasPartnerDiscountOverride
            };
        }

        private static string ToContainsPattern(string value)
            => $"%{value.Trim()}%";

        private sealed class OrderListRow
        {
            public Guid Id { get; init; }
            public string OrderNumber { get; init; } = string.Empty;
            public int TicketId { get; init; }
            public string TableName { get; init; } = string.Empty;
            public OrderSource OrderSource { get; init; }
            public string? TalabatOrderNumber { get; init; }
            public DateTime? TalabatPickupTime { get; init; }
            public Guid? DeliveryPartnerId { get; init; }
            public string? DeliveryPartnerName { get; init; }
            public string? DeliveryPartnerNameAr { get; init; }
            public string? DeliveryPartnerCode { get; init; }
            public string? PartnerOrderNumber { get; init; }
            public OrderType OrderType { get; init; }
            public int ItemCount { get; init; }
            public decimal TotalAmount { get; init; }
            public decimal FoodSubtotal { get; init; }
            public decimal CustomerDeliveryFee { get; init; }
            public decimal ActualDeliveryCost { get; init; }
            public decimal DeliveryMargin { get; init; }
            public decimal MarketplaceDeliveryFee { get; init; }
            public decimal MarketplaceServiceFee { get; init; }
            public decimal NetRestaurantRevenue { get; init; }
            public decimal CostSharingTotalCommission { get; init; }
            public decimal CostSharingRestaurantShare { get; init; }
            public decimal CostSharingCounterpartyShare { get; init; }
            public decimal CostSharingNetSettlement { get; init; }
            public OrderStatus Status { get; init; }
            public string? PaymentMethod { get; init; }
            public string? CashierName { get; init; }
            public bool IsVoucherApplied { get; init; }
            public decimal VoucherDiscountAmount { get; init; }
            public DateTime? VoucherAppliedAt { get; init; }
            public decimal? PaidAmountFromPayments { get; init; }
            public DateTime? PaidAt { get; init; }
            public string? CustomerPhone { get; init; }
            public string? CustomerNameSnapshot { get; init; }
            public string? LatestPaymentMethodName { get; init; }
            public string? LatestPaymentMethodNameAr { get; init; }
            public string? LatestPaymentMethodCode { get; init; }
            public string? LatestPaymentReferenceNumber { get; init; }
            public Guid? PendingPaymentAdjustmentId { get; init; }
            public Guid? PendingCancellationId { get; init; }
            public DateTime CreatedAt { get; init; }
            public DateTime? LastUpdatedAt { get; init; }
            public bool HasPriceDifference { get; init; }
            public bool HasPartnerDiscountOverride { get; init; }
        }
    }
}
