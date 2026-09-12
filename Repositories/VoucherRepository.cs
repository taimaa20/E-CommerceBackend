using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed class VoucherRepository : IVoucherRepository
    {
        private readonly PosDbContext _context;

        public VoucherRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task AcquireDailyUsageLockAsync(Guid tenantId, DateOnly businessDate, CancellationToken ct)
        {
            var lockKey = $"voucher:{tenantId:N}:{businessDate:yyyyMMdd}";
            return _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
                ct);
        }

        public Task<int> CountUsedTodayAsync(Guid tenantId, DateOnly businessDate, CancellationToken ct)
        {
            return _context.VoucherUsageAudits
                .AsNoTracking()
                .CountAsync(v => v.TenantId == tenantId && v.BusinessDate == businessDate, ct);
        }

        public Task<bool> ExistsForOrderAsync(Guid tenantId, Guid orderId, CancellationToken ct)
        {
            return _context.VoucherUsageAudits
                .AsNoTracking()
                .AnyAsync(v => v.TenantId == tenantId && v.OrderId == orderId, ct);
        }

        public Task<List<VoucherAuditItemDto>> GetTodayAuditAsync(Guid tenantId, DateOnly businessDate, CancellationToken ct)
        {
            return _context.VoucherUsageAudits
                .AsNoTracking()
                .Where(v => v.TenantId == tenantId && v.BusinessDate == businessDate)
                .OrderByDescending(v => v.UsedAt)
                .Select(v => new VoucherAuditItemDto
                {
                    OrderId = v.OrderId,
                    OrderNumber = v.Order.OrderNumber,
                    UsedAt = v.UsedAt,
                    DiscountAmount = v.DiscountAmount,
                    CreatedByName = v.CreatedByUser != null
                        ? v.CreatedByUser.FullName ?? v.CreatedByUser.Username
                        : null
                })
                .ToListAsync(ct);
        }

        public async Task InsertAuditAsync(VoucherUsageAudit audit, CancellationToken ct)
        {
            await _context.VoucherUsageAudits.AddAsync(audit, ct);
            await _context.SaveChangesAsync(ct);
        }
    }
}
