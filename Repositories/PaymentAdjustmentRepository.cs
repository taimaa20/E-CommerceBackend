using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed class PaymentAdjustmentRepository : IPaymentAdjustmentRepository
    {
        private readonly PosDbContext _context;

        public PaymentAdjustmentRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct)
            => _context.Database.BeginTransactionAsync(ct);

        public async Task LockOrderAsync(Guid orderId, CancellationToken ct)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Orders\" WHERE \"Id\" = {orderId} FOR UPDATE",
                ct);
        }

        public Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct)
            => _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        public Task<User?> GetUserAsync(Guid userId, CancellationToken ct)
            => _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.Id == userId, ct);

        public Task<bool> HasPendingAsync(Guid orderId, CancellationToken ct)
            => _context.PaymentAdjustments.AnyAsync(
                adjustment => adjustment.OrderId == orderId &&
                    adjustment.Status == PaymentAdjustmentStatus.Pending,
                ct);

        public Task<PaymentAdjustment?> GetPendingAsync(
            Guid orderId,
            Guid adjustmentId,
            CancellationToken ct)
        {
            return _context.PaymentAdjustments
                .Include(adjustment => adjustment.Details)
                .Include(adjustment => adjustment.Order)
                    .ThenInclude(order => order.Payments)
                .FirstOrDefaultAsync(
                    adjustment => adjustment.Id == adjustmentId &&
                        adjustment.OrderId == orderId &&
                        adjustment.Status == PaymentAdjustmentStatus.Pending,
                    ct);
        }

        public Task<List<PaymentMethod>> GetActivePaymentMethodsAsync(CancellationToken ct)
            => _context.PaymentMethods
                .AsNoTracking()
                .Where(method => method.IsActive)
                .OrderBy(method => method.DisplayOrder)
                .ThenBy(method => method.NameEn)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<PaymentAdjustmentApproverDto>> GetApproversAsync(
            Guid requesterId,
            CancellationToken ct)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(user => user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin || user.Role == UserRole.Manager)
                .OrderBy(user => user.FullName ?? user.Username)
                .Select(user => new PaymentAdjustmentApproverDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.FullName ?? user.Username,
                    DisplayNameAr = user.FullNameAr,
                    Role = user.Role.ToString()
                })
                .ToListAsync(ct);
        }

        public async Task SaveRequestAsync(PaymentAdjustment adjustment, CancellationToken ct)
        {
            _context.PaymentAdjustments.Add(adjustment);
            await _context.SaveChangesAsync(ct);
        }

        public async Task SaveApprovedAsync(
            Order order,
            IReadOnlyCollection<Payment> oldPayments,
            IReadOnlyCollection<Payment> newPayments,
            PaymentAdjustment adjustment,
            string paymentMethodSummary,
            DateTime approvedAt,
            CancellationToken ct)
        {
            foreach (var payment in oldPayments)
                payment.DeletedAt = approvedAt;

            order.PaymentMethod = paymentMethodSummary;
            _context.Payments.AddRange(newPayments);
            await _context.SaveChangesAsync(ct);
        }

        public Task SaveDecisionAsync(CancellationToken ct)
            => _context.SaveChangesAsync(ct);

        public async Task<PaginatedResponse<PaymentAdjustmentHistoryDto>> GetHistoryAsync(
            PaymentAdjustmentHistoryQuery filter,
            CancellationToken ct)
        {
            var query = ApplyHistoryFilters(
                _context.PaymentAdjustmentDetails
                    .AsNoTracking()
                    .Where(detail => detail.Adjustment.Status == PaymentAdjustmentStatus.Approved),
                filter);
            var totalCount = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(detail => detail.Adjustment.ApprovedAt)
                .ThenBy(detail => detail.AdjustmentId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(detail => new PaymentAdjustmentHistoryDto
                {
                    AdjustmentId = detail.AdjustmentId,
                    OrderId = detail.Adjustment.OrderId,
                    OrderNumber = detail.Adjustment.Order.DisplayOrderNumber
                        ?? detail.Adjustment.Order.OrderNumber,
                    OrderType = detail.Adjustment.Order.OrderType.ToString(),
                    OldPaymentMethod = detail.OldPaymentMethodName,
                    OldPaymentMethodAr = detail.OldPaymentMethodNameAr,
                    NewPaymentMethod = detail.NewPaymentMethodName,
                    NewPaymentMethodAr = detail.NewPaymentMethodNameAr,
                    Amount = detail.Amount,
                    RequestedBy = detail.Adjustment.RequestedByUserName,
                    ApprovedBy = detail.Adjustment.ApprovedByUserName ?? string.Empty,
                    Reason = detail.Adjustment.Reason,
                    ApprovedAt = detail.Adjustment.ApprovedAt!.Value
                })
                .ToListAsync(ct);

            return new PaginatedResponse<PaymentAdjustmentHistoryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.Page,
                PageSize = filter.PageSize
            };
        }

        private static IQueryable<PaymentAdjustmentDetail> ApplyHistoryFilters(
            IQueryable<PaymentAdjustmentDetail> query,
            PaymentAdjustmentHistoryQuery filter)
        {
            if (filter.DateFrom.HasValue)
                query = query.Where(detail => detail.Adjustment.ApprovedAt >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(detail => detail.Adjustment.ApprovedAt < filter.DateTo.Value);
            if (!string.IsNullOrWhiteSpace(filter.User))
                query = query.Where(detail => EF.Functions.ILike(
                    detail.Adjustment.RequestedByUserName, $"%{filter.User.Trim()}%"));
            if (!string.IsNullOrWhiteSpace(filter.Manager))
                query = query.Where(detail => EF.Functions.ILike(
                    detail.Adjustment.ApprovedByUserName!, $"%{filter.Manager.Trim()}%"));
            if (filter.PaymentMethodId.HasValue)
                query = query.Where(detail =>
                    detail.OldPaymentMethodId == filter.PaymentMethodId ||
                    detail.NewPaymentMethodId == filter.PaymentMethodId);
            if (filter.OrderType.HasValue)
                query = query.Where(detail => detail.Adjustment.Order.OrderType == filter.OrderType.Value);
            if (filter.BranchId.HasValue)
                query = query.Where(detail => detail.BranchId == filter.BranchId.Value);

            return query;
        }
    }
}
