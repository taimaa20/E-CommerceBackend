using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Audit;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Predicates;
using RestaurantPos.Api.Modules.Dashboard.Queries;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Audit
{
    public sealed class AuditAnalyticsService : IAuditAnalyticsService
    {
        private const int LateNightStartHourUtc = 23;
        private const int LateNightEndHourUtc   = 4;

        private readonly IMetricQueryBuilder _q;
        private readonly IDashboardFilterContext _ctx;
        private readonly PosDbContext _db;
        private readonly ILogger<AuditAnalyticsService> _logger;

        public AuditAnalyticsService(IMetricQueryBuilder q, IDashboardFilterContext ctx, PosDbContext db, ILogger<AuditAnalyticsService> logger)
        {
            _q   = q   ?? throw new ArgumentNullException(nameof(q));
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AuditSnapshotDto> BuildSnapshotAsync(CancellationToken ct)
        {
            var refundsByEmp = await SafeAsync.RunAsync(_logger, "audit.refundsByEmp", () => RefundsByEmployeeAsync(ct),       (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var cancelsByEmp = await SafeAsync.RunAsync(_logger, "audit.cancelsByEmp", () => CancellationsByEmployeeAsync(ct), (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var discByEmp    = await SafeAsync.RunAsync(_logger, "audit.discByEmp",    () => DiscountsByEmployeeAsync(ct),     (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var voucherAbuse = await SafeAsync.RunAsync(_logger, "audit.voucherAbuse", () => VoucherAbuseAsync(ct),            (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var manualStock  = await SafeAsync.RunAsync(_logger, "audit.manualStock",  () => ManualStockReductionsAsync(ct),   (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var counters     = await SafeAsync.RunAsync(_logger, "audit.counters",     () => CountersAsync(ct),                new AuditCountersDto());
            var cancelTrend  = await SafeAsync.RunAsync(_logger, "audit.cancelTrend",  () => CancellationTrendAsync(ct),       (IReadOnlyList<TimeSeriesPointDto>)Array.Empty<TimeSeriesPointDto>());
            var refundTrend  = await SafeAsync.RunAsync(_logger, "audit.refundTrend",  () => RefundTrendAsync(ct),             (IReadOnlyList<TimeSeriesPointDto>)Array.Empty<TimeSeriesPointDto>());

            return new AuditSnapshotDto
            {
                Scope                  = _ctx.Scope,
                WindowStartUtc         = _ctx.Window.StartUtc,
                WindowEndUtc           = _ctx.Window.EndUtc,
                AppliedFilter          = _ctx.Filter,
                RefundsByEmployee      = refundsByEmp,
                CancellationsByEmployee= cancelsByEmp,
                DiscountsByEmployee    = discByEmp,
                VoucherAbuseByCashier  = voucherAbuse,
                ManualStockReductions  = manualStock,
                Counters               = counters,
                CancellationTrend      = cancelTrend,
                RefundTrend            = refundTrend
            };
        }

        private async Task<IReadOnlyList<RankedRowDto>> RefundsByEmployeeAsync(CancellationToken ct)
        {
            var rows = await _q.RefundLogs()
                .GroupBy(r => new { r.ProcessedById, r.ProcessedByName })
                .Select(g => new
                {
                    g.Key.ProcessedById,
                    g.Key.ProcessedByName,
                    Total = g.Sum(r => r.RefundAmount),
                    Count = g.Count(),
                })
                .OrderByDescending(r => r.Total)
                .Take(10)
                .ToListAsync(ct);

            var namesAr = await UserNameArMapAsync(rows.Select(r => r.ProcessedById), ct);
            return rows.Select(r => new RankedRowDto(
                r.ProcessedById,
                r.ProcessedByName,
                namesAr.GetValueOrDefault(r.ProcessedById),
                r.Total,
                null,
                r.Count)).ToList();
        }

        private async Task<IReadOnlyList<RankedRowDto>> CancellationsByEmployeeAsync(CancellationToken ct)
        {
            var rows = await _q.CancelLogs()
                .GroupBy(c => new
                {
                    c.CancelledById,
                    Name = c.CancelledBy.FullName ?? c.CancelledByName,
                    NameAr = c.CancelledBy.FullNameAr
                })
                .Select(g => new
                {
                    g.Key.CancelledById,
                    g.Key.Name,
                    g.Key.NameAr,
                    Total = g.Sum(c => c.Amount),
                    Count = g.Count(),
                })
                .OrderByDescending(r => r.Total)
                .Take(10)
                .ToListAsync(ct);
            return rows.Select(r => new RankedRowDto(r.CancelledById, r.Name, r.NameAr, r.Total, null, r.Count)).ToList();
        }

        private async Task<IReadOnlyList<RankedRowDto>> DiscountsByEmployeeAsync(CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.Confirmed)
                .Where(o => o.DiscountAmount > 0 && o.PaidByUserId.HasValue)
                .GroupBy(o => new
                {
                    Id = o.PaidByUserId!.Value,
                    Name = o.PaidByUser != null ? (o.PaidByUser.FullName ?? o.PaidByUser.Username) : "Unknown",
                    NameAr = o.PaidByUser != null ? o.PaidByUser.FullNameAr : null
                })
                .Select(g => new
                {
                    g.Key.Id,
                    g.Key.Name,
                    g.Key.NameAr,
                    Total = g.Sum(o => o.DiscountAmount),
                    Count = g.Count(),
                })
                .OrderByDescending(r => r.Total)
                .Take(10)
                .ToListAsync(ct);
            return rows.Select(r => new RankedRowDto(r.Id, r.Name, r.NameAr, r.Total, null, r.Count)).ToList();
        }

        private async Task<IReadOnlyList<RankedRowDto>> VoucherAbuseAsync(CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(o => o.IsVoucherApplied && o.PaidByUserId.HasValue)
                .GroupBy(o => new
                {
                    Id = o.PaidByUserId!.Value,
                    Name = o.PaidByUser != null ? (o.PaidByUser.FullName ?? o.PaidByUser.Username) : "Unknown",
                    NameAr = o.PaidByUser != null ? o.PaidByUser.FullNameAr : null
                })
                .Select(g => new
                {
                    g.Key.Id,
                    g.Key.Name,
                    g.Key.NameAr,
                    Total = g.Sum(o => o.VoucherDiscountAmount),
                    Count = g.Count(),
                })
                .OrderByDescending(r => r.Total)
                .Take(10)
                .ToListAsync(ct);
            return rows.Select(r => new RankedRowDto(r.Id, r.Name, r.NameAr, r.Total, null, r.Count)).ToList();
        }

        private async Task<IReadOnlyList<RankedRowDto>> ManualStockReductionsAsync(CancellationToken ct)
        {
            var rows = await _q.WasteLogs()
                .Where(w => w.WasteType.HasValue && w.LoggedById.HasValue
                         && (w.Category == WasteCategory.ManualMaterial || w.Category == WasteCategory.ManualProduct))
                .GroupBy(w => new
                {
                    Id = w.LoggedById!.Value,
                    Name = w.LoggedBy != null ? (w.LoggedBy.FullName ?? w.LoggedBy.Username) : (w.LoggedByName ?? "Unknown"),
                    NameAr = w.LoggedBy != null ? w.LoggedBy.FullNameAr : null
                })
                .Select(g => new
                {
                    g.Key.Id,
                    g.Key.Name,
                    g.Key.NameAr,
                    Total = g.Sum(w => w.CostAmount > 0m
                        ? w.CostAmount
                        : (w.InventoryTransactions.Sum(t => (decimal?)t.CostAmount) ?? 0m) > 0m
                            ? w.InventoryTransactions.Sum(t => t.CostAmount)
                            : w.Amount > 0m ? w.Amount : 0m),
                    Count = g.Count(),
                })
                .OrderByDescending(r => r.Total)
                .Take(10)
                .ToListAsync(ct);
            return rows.Select(r => new RankedRowDto(r.Id, r.Name, r.NameAr, r.Total, null, r.Count)).ToList();
        }

        private async Task<AuditCountersDto> CountersAsync(CancellationToken ct)
        {
            var refundCount = await _q.RefundLogs().CountAsync(ct);
            var refundAmount = await _q.RefundLogs().SumAsync(r => (decimal?)r.RefundAmount, ct) ?? 0m;

            var cancelCount = await _q.CancelLogs().CountAsync(ct);
            var cancelAmount = await _q.CancelLogs().SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

            // Cancel-after-payment = order cancelled but has a RefundLog (post-payment refund flow).
            var afterPaid = await _q.RefundLogs().CountAsync(ct);

            var lateNight = await _q.CancelLogs()
                .CountAsync(c => c.CancelledAt.Hour >= LateNightStartHourUtc || c.CancelledAt.Hour < LateNightEndHourUtc, ct);

            var zeroOrders = await _q.Orders()
                .CountAsync(o => o.TotalAmount == 0 && o.Status != OrderStatus.Cancelled, ct);

            var manualWaste = await _q.WasteLogs()
                .CountAsync(w => w.Category == WasteCategory.ManualMaterial || w.Category == WasteCategory.ManualProduct, ct);

            return new AuditCountersDto
            {
                TotalRefunds          = refundCount,
                TotalRefundAmount     = Math.Round(refundAmount, 2),
                TotalCancellations    = cancelCount,
                TotalCancelledAmount  = Math.Round(cancelAmount, 2),
                CancelledAfterPayment = afterPaid,
                LateNightCancellations= lateNight,
                ZeroValueOrders       = zeroOrders,
                ManualWasteEntries    = manualWaste
            };
        }

        private async Task<IReadOnlyList<TimeSeriesPointDto>> CancellationTrendAsync(CancellationToken ct)
        {
            var rows = await _q.CancelLogs()
                .GroupBy(c => new { c.CancelledAt.Year, c.CancelledAt.Month, c.CancelledAt.Day })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Amount = g.Sum(c => c.Amount), Count = g.Count() })
                .ToListAsync(ct);

            return rows.Select(r => new TimeSeriesPointDto(
                    new DateTime(r.Year, r.Month, r.Day, 0, 0, 0, DateTimeKind.Utc),
                    Math.Round(r.Amount, 2), r.Count))
                .OrderBy(p => p.BucketStartUtc)
                .ToList();
        }

        private Task<Dictionary<Guid, string?>> UserNameArMapAsync(IEnumerable<Guid> ids, CancellationToken ct)
        {
            var keys = ids.Distinct().ToArray();
            return _db.Users.AsNoTracking()
                .Where(u => keys.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullNameAr, ct);
        }

        private async Task<IReadOnlyList<TimeSeriesPointDto>> RefundTrendAsync(CancellationToken ct)
        {
            var rows = await _q.RefundLogs()
                .GroupBy(r => new { r.ProcessedAt.Year, r.ProcessedAt.Month, r.ProcessedAt.Day })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Amount = g.Sum(r => r.RefundAmount), Count = g.Count() })
                .ToListAsync(ct);

            return rows.Select(r => new TimeSeriesPointDto(
                    new DateTime(r.Year, r.Month, r.Day, 0, 0, 0, DateTimeKind.Utc),
                    Math.Round(r.Amount, 2), r.Count))
                .OrderBy(p => p.BucketStartUtc)
                .ToList();
        }
    }
}
