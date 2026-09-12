using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly PosDbContext _context;

        public PaymentRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Order?> GetOrderForPaymentAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            return await OrderPaymentQuery()
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        }

        public async Task<Order?> GetOrderForPaymentIntegrationAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            return await OrderPaymentQuery()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == orderId && o.DeletedAt == null, cancellationToken);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => _context.Database.BeginTransactionAsync(cancellationToken);

        public Task<List<PaymentMethod>> GetActivePaymentMethodsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return _context.PaymentMethods
                .AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ThenBy(m => m.NameEn)
                .ToListAsync(cancellationToken);
        }

        public async Task LockOrderRowAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            // PostgreSQL row-level lock. Blocks any other transaction trying
            // to LOCK / UPDATE this row until commit / rollback, which is
            // what we need to serialize concurrent payment attempts for
            // the same order. The SELECT returns 1 row (or none) but its
            // side effect (the lock) is the point.
            await _context.Database
                .ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Orders\" WHERE \"Id\" = {orderId} FOR UPDATE",
                    cancellationToken);
        }

        public async Task<decimal> GetTotalPaidAmountAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            var sum = await _context.Payments
                .Where(p => p.OrderId == orderId)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken);
            return sum ?? 0m;
        }

        public async Task<int> UpdateOrderPaymentFieldsAsync(Order order, CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .IgnoreQueryFilters()
                .Where(o => o.Id == order.Id && o.TenantId == order.TenantId && o.DeletedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.ServiceChargeRate, order.ServiceChargeRate)
                    .SetProperty(o => o.TaxRate, order.TaxRate)
                    .SetProperty(o => o.TotalAmount, order.TotalAmount)
                    .SetProperty(o => o.Subtotal, order.Subtotal)
                    .SetProperty(o => o.DiscountAmount, order.DiscountAmount)
                    .SetProperty(o => o.ServiceChargeAmount, order.ServiceChargeAmount)
                    .SetProperty(o => o.TaxAmount, order.TaxAmount)
                    .SetProperty(o => o.IsVoucherApplied, order.IsVoucherApplied)
                    .SetProperty(o => o.VoucherDiscountAmount, order.VoucherDiscountAmount)
                    .SetProperty(o => o.VoucherAppliedAt, order.VoucherAppliedAt)
                    .SetProperty(o => o.CustomerPhone, order.CustomerPhone)
                    .SetProperty(o => o.AmountTendered, order.AmountTendered)
                    .SetProperty(o => o.ChangeAmount, order.ChangeAmount)
                    .SetProperty(o => o.Status, order.Status)
                    .SetProperty(o => o.PaymentMethod, order.PaymentMethod)
                    .SetProperty(o => o.PaidAt, order.PaidAt)
                    .SetProperty(o => o.PaidByUserId, order.PaidByUserId)
                    .SetProperty(o => o.CostSharingTotalCommission, order.CostSharingTotalCommission)
                    .SetProperty(o => o.CostSharingRestaurantShare, order.CostSharingRestaurantShare)
                    .SetProperty(o => o.CostSharingCounterpartyShare, order.CostSharingCounterpartyShare)
                    .SetProperty(o => o.CostSharingNetSettlement, order.CostSharingNetSettlement)
                    .SetProperty(o => o.CostSharingDetailsJson, order.CostSharingDetailsJson)
                    .SetProperty(o => o.CostSharingCalculatedAt, order.CostSharingCalculatedAt)
                    .SetProperty(o => o.SyncedAt, order.SyncedAt)
                , cancellationToken);
        }

        public async Task AddPaymentRecordAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            await _context.Payments.AddAsync(payment, cancellationToken);
        }

        public async Task InsertPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            _context.ChangeTracker.Clear();
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> UpdateTableStatusAsync(Guid tableId, TableStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.Tables
                .Where(t => t.Id == tableId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, status), cancellationToken);
        }

        private IQueryable<Order> OrderPaymentQuery()
            => _context.Orders
                .AsNoTracking()
                .Include(o => o.Table)
                .Include(o => o.Customer)
                .Include(o => o.DeliveryPartner)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.Payments);
    }
}
