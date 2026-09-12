using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Home;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Predicates;
using RestaurantPos.Api.Modules.Dashboard.Queries;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Home
{
    /// <summary>
    /// Executive overview. Pulls a curated subset of the same metrics the
    /// per-scope dashboards compute, so headline numbers and deep-dive
    /// numbers always agree.
    /// </summary>
    public sealed class HomeAnalyticsService : IHomeAnalyticsService
    {
        private readonly IMetricQueryBuilder     _q;
        private readonly IDashboardFilterContext _ctx;
        private readonly PosDbContext            _db;
        private readonly ILogger<HomeAnalyticsService> _logger;

        public HomeAnalyticsService(IMetricQueryBuilder q, IDashboardFilterContext ctx, PosDbContext db, ILogger<HomeAnalyticsService> logger)
        {
            _q   = q   ?? throw new ArgumentNullException(nameof(q));
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _db  = db  ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HomeSnapshotDto> BuildSnapshotAsync(CancellationToken ct)
        {
            var headline   = await SafeAsync.RunAsync(_logger, "home.headline",   () => BuildHeadlineAsync(ct),       new HomeHeadlineDto());
            var revTrend   = await SafeAsync.RunAsync(_logger, "home.revTrend",   () => BuildRevenueTrendAsync(ct),   (IReadOnlyList<TimeSeriesPointDto>)Array.Empty<TimeSeriesPointDto>());
            var hourly     = await SafeAsync.RunAsync(_logger, "home.hourly",     () => BuildHourlyOrdersAsync(ct),   (IReadOnlyList<TimeSeriesPointDto>)Array.Empty<TimeSeriesPointDto>());
            var topProds   = await SafeAsync.RunAsync(_logger, "home.topProds",   () => BuildTopProductsAsync(ct),    (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var topCash    = await SafeAsync.RunAsync(_logger, "home.topCash",    () => BuildTopCashiersAsync(ct),    (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var payments   = await SafeAsync.RunAsync(_logger, "home.payments",   () => BuildPaymentSplitAsync(ct),   (IReadOnlyList<CategorySliceDto>)Array.Empty<CategorySliceDto>());
            var orderTypes = await SafeAsync.RunAsync(_logger, "home.orderTypes", () => BuildOrderTypeSplitAsync(ct), (IReadOnlyList<CategorySliceDto>)Array.Empty<CategorySliceDto>());
            var alerts     = await SafeAsync.RunAsync(_logger, "home.alerts",     () => BuildAlertsAsync(ct),         (IReadOnlyList<DashboardAlertDto>)Array.Empty<DashboardAlertDto>());
            var activity   = await SafeAsync.RunAsync(_logger, "home.activity",   () => BuildActivityAsync(ct),       (IReadOnlyList<ActivityEntryDto>)Array.Empty<ActivityEntryDto>());

            return new HomeSnapshotDto
            {
                Scope          = Security.DashboardScope.Operations,
                WindowStartUtc = _ctx.Window.StartUtc,
                WindowEndUtc   = _ctx.Window.EndUtc,
                AppliedFilter  = _ctx.Filter,
                Headline       = headline,
                RevenueTrend   = revTrend,
                HourlyOrders   = hourly,
                TopProducts    = topProds,
                TopCashiers    = topCash,
                PaymentSplit   = payments,
                OrderTypeSplit = orderTypes,
                Alerts         = alerts,
                RecentActivity = activity
            };
        }

        private async Task<HomeHeadlineDto> BuildHeadlineAsync(CancellationToken ct)
        {
            var orders = _q.Orders();

            var revenue = await orders.Where(OrderLifecyclePredicates.Confirmed)
                                      .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
            var paid    = await orders.Where(OrderLifecyclePredicates.Confirmed).CountAsync(ct);
            var aov     = paid == 0 ? 0m : Math.Round(revenue / paid, 2);

            var cogs    = await _q.OrderItems()
                                  .Where(OrderLifecyclePredicates.ConfirmedItem)
                                  .SumAsync(oi => (decimal?)oi.StockDeductedCost, ct) ?? 0m;
            var labor   = await _q.TimeEntries().SumAsync(t => (decimal?)t.TotalCost, ct) ?? 0m;
            var net     = revenue - cogs - labor;
            var margin  = revenue == 0 ? 0m : Math.Round(net / revenue * 100m, 2);

            // Same canonical waste-cost definition as the Inventory dashboard,
            // so the headline WasteCost reconciles with Inventory's TotalCost
            // for an identical window/filter.
            var waste   = await WasteMetricProjection.Project(_q.WasteLogs())
                                  .Where(w => w.Status == WasteLogStatus.Approved)
                                  .SumAsync(w => (decimal?)w.CostAmount, ct) ?? 0m;
            var wastePct = revenue == 0 ? 0m : Math.Round(waste / revenue * 100m, 2);

            // Point-in-time lifecycle counters (NOT window-scoped).
            var activeOrders = await _q.AllOrders()
                .CountAsync(OrderLifecyclePredicates.Active, ct);

            // Live stale-order count (Preparing/Ready older than 15 min) — the true
            // count, independent of the capped real-time alert feed.
            var staleOrders = await _q.AllOrders()
                .CountAsync(OrderLifecyclePredicates.StaleSince(DateTime.UtcNow - TimeSpan.FromMinutes(15)), ct);

            var (occupied, total) = await CountDineInTablesAsync(ct);

            return new HomeHeadlineDto
            {
                RevenueToday      = Math.Round(revenue, 2),
                OrdersToday       = paid,
                ActiveOrders      = activeOrders,
                StaleOrders       = staleOrders,
                NetProfit         = Math.Round(net, 2),
                MarginPercent     = margin,
                WasteCost         = Math.Round(waste, 2),
                WastePercent      = wastePct,
                ActiveTables      = occupied,
                TotalTables       = total,
                AverageOrderValue = aov
            };
        }

        private async Task<IReadOnlyList<TimeSeriesPointDto>> BuildRevenueTrendAsync(CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.Confirmed)
                .GroupBy(o => new
                {
                    Year = (o.PaidAt ?? o.CreatedAt).Year,
                    Month = (o.PaidAt ?? o.CreatedAt).Month,
                    Day = (o.PaidAt ?? o.CreatedAt).Day
                })
                .Select(g => new
                {
                    g.Key.Year, g.Key.Month, g.Key.Day,
                    Total = g.Sum(o => o.TotalAmount),
                    Count = g.Count()
                })
                .ToListAsync(ct);

            return rows.Select(r => new TimeSeriesPointDto(
                    new DateTime(r.Year, r.Month, r.Day, 0, 0, 0, DateTimeKind.Utc),
                    r.Total, r.Count))
                .OrderBy(p => p.BucketStartUtc)
                .ToList();
        }

        private async Task<IReadOnlyList<TimeSeriesPointDto>> BuildHourlyOrdersAsync(CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.Confirmed)
                .GroupBy(o => new
                {
                    Year = (o.PaidAt ?? o.CreatedAt).Year,
                    Month = (o.PaidAt ?? o.CreatedAt).Month,
                    Day = (o.PaidAt ?? o.CreatedAt).Day,
                    Hour = (o.PaidAt ?? o.CreatedAt).Hour
                })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Total = g.Sum(o => o.TotalAmount), Count = g.Count() })
                .ToListAsync(ct);

            return rows.Select(r => new TimeSeriesPointDto(
                    new DateTime(r.Year, r.Month, r.Day, r.Hour, 0, 0, DateTimeKind.Utc),
                    r.Total, r.Count))
                .OrderBy(p => p.BucketStartUtc)
                .ToList();
        }

        private async Task<(int Occupied, int Total)> CountDineInTablesAsync(CancellationToken ct)
        {
            var branchId = _ctx.Filter.BranchId;
            if (branchId.HasValue && !await IsMainBranchAsync(branchId.Value, ct))
                return (0, 0);

            var occupied = await _db.Tables.AsNoTracking().CountAsync(t => t.Status == TableStatus.Occupied, ct);
            var total = await _db.Tables.AsNoTracking().CountAsync(ct);
            return (occupied, total);
        }

        private Task<bool> IsMainBranchAsync(Guid branchId, CancellationToken ct)
            => _db.Branches
                .AsNoTracking()
                .AnyAsync(branch => branch.Id == branchId && branch.IsMainBranch, ct);

        private async Task<IReadOnlyList<RankedRowDto>> BuildTopProductsAsync(CancellationToken ct)
        {
            var rows = await _q.OrderItems()
                .Where(OrderLifecyclePredicates.ConfirmedItem)
                .GroupBy(oi => new { oi.ProductId, oi.ProductName, ProductNameAr = oi.Product.NameAr })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Key.ProductNameAr,
                    Revenue  = g.Sum(oi => oi.Price * oi.Quantity),
                    Quantity = g.Sum(oi => oi.Quantity),
                })
                .OrderByDescending(r => r.Revenue)
                .Take(5)
                .ToListAsync(ct);
            return rows.Select(r => new RankedRowDto(r.ProductId, r.ProductName, r.ProductNameAr, r.Revenue, null, r.Quantity)).ToList();
        }

        private async Task<IReadOnlyList<RankedRowDto>> BuildTopCashiersAsync(CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.Confirmed)
                .Where(o => o.PaidByUserId.HasValue)
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
                    Revenue = g.Sum(o => o.TotalAmount),
                    Count   = g.Count(),
                })
                .OrderByDescending(r => r.Revenue)
                .Take(5)
                .ToListAsync(ct);
            return rows.Select(r => new RankedRowDto(r.Id, r.Name, r.NameAr, r.Revenue, null, r.Count)).ToList();
        }

        private async Task<IReadOnlyList<CategorySliceDto>> BuildPaymentSplitAsync(CancellationToken ct)
        {
            // Group by actual configured PaymentMethod (PaymentMethodName/Code captured at payment time).
            // Falls back to legacy Method string for older payments, and to Order.PaymentMethod for orders
            // that predate the multi-payment table.
            var orders = await _q.Orders()
                .Where(OrderLifecyclePredicates.Confirmed)
                .Select(o => new
                {
                    o.TotalAmount,
                    o.PaymentMethod,
                    Payments = o.Payments.Select(p => new
                    {
                        p.Amount,
                        p.Method,
                        p.PaymentMethodName,
                        p.PaymentMethodNameAr,
                        p.PaymentMethodCode
                    }).ToList()
                })
                .ToListAsync(ct);

            var buckets = new Dictionary<string, (decimal Value, string Label, string? LabelAr)>(StringComparer.OrdinalIgnoreCase);

            void Add(string key, string label, string? labelAr, decimal amount)
            {
                if (buckets.TryGetValue(key, out var current))
                    buckets[key] = (current.Value + amount, current.Label, current.LabelAr ?? labelAr);
                else
                    buckets[key] = (amount, label, labelAr);
            }

            foreach (var o in orders)
            {
                if (o.Payments.Count > 0)
                {
                    foreach (var p in o.Payments)
                    {
                        // Configured-method snapshot wins. When absent, fold every legacy
                        // non-cash free-text Method into a single "Card (legacy)" bucket.
                        if (!string.IsNullOrWhiteSpace(p.PaymentMethodCode) ||
                            !string.IsNullOrWhiteSpace(p.PaymentMethodName))
                        {
                            var key = !string.IsNullOrWhiteSpace(p.PaymentMethodCode)
                                ? p.PaymentMethodCode!
                                : p.PaymentMethodName!;
                            var label = !string.IsNullOrWhiteSpace(p.PaymentMethodName)
                                ? p.PaymentMethodName!
                                : p.PaymentMethodCode!;
                            Add(key, label, p.PaymentMethodNameAr, p.Amount);
                        }
                        else
                        {
                            var bucket = OrderPaymentHelper.ResolveLegacyBucket(p.Method);
                            Add(bucket.Key, bucket.Label, bucket.LabelAr, p.Amount);
                        }
                    }
                }
                else
                {
                    var bucket = OrderPaymentHelper.ResolveLegacyBucket(o.PaymentMethod);
                    Add(bucket.Key, bucket.Label, bucket.LabelAr, o.TotalAmount);
                }
            }

            var total = buckets.Values.Sum(b => b.Value);
            return buckets.Values
                .OrderByDescending(b => b.Value)
                .Select(b => new CategorySliceDto(
                    b.Label,
                    b.LabelAr,
                    Math.Round(b.Value, 2),
                    total == 0 ? 0 : Math.Round(b.Value / total * 100m, 2)))
                .ToList();
        }

        private async Task<IReadOnlyList<CategorySliceDto>> BuildOrderTypeSplitAsync(CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.Confirmed)
                .GroupBy(o => o.OrderType)
                .Select(g => new { Type = g.Key, Total = g.Sum(o => o.TotalAmount), Count = g.Count() })
                .ToListAsync(ct);

            var total = rows.Sum(r => r.Count);
            return rows.Select(r => new CategorySliceDto(
                r.Type.ToString(), null, r.Count,
                total == 0 ? 0 : Math.Round((decimal)r.Count / total * 100m, 2))).ToList();
        }

        private async Task<IReadOnlyList<DashboardAlertDto>> BuildAlertsAsync(CancellationToken ct)
        {
            var alerts = new List<DashboardAlertDto>();
            var now = DateTime.UtcNow;
            var branchId = _ctx.Filter.BranchId;

            var lowStockQuery = branchId.HasValue
                ? _db.RawMaterials.AsNoTracking()
                    .Select(m => new
                    {
                        m.Name,
                        m.NameAr,
                        CurrentStock = _db.StockBatches
                            .Where(b => b.MaterialId == m.Id
                                     && b.BranchId == branchId.Value
                                     && b.IsApproved
                                     && b.RemainingQuantity > 0
                                     && b.Status == BatchStatus.Good)
                            .Sum(b => (decimal?)b.RemainingQuantity) ?? 0m,
                        Unit = m.Unit,
                        m.MinimumAlertLevel
                    })
                : _db.RawMaterials.AsNoTracking()
                    .Select(m => new { m.Name, m.NameAr, m.CurrentStock, Unit = m.Unit, m.MinimumAlertLevel });

            var lowStock = await lowStockQuery
                .Where(m => m.CurrentStock <= m.MinimumAlertLevel)
                .Take(3).ToListAsync(ct);
            foreach (var m in lowStock)
            {
                var arabicName = string.IsNullOrWhiteSpace(m.NameAr) ? m.Name : m.NameAr;
                alerts.Add(new DashboardAlertDto("LOW_STOCK", "amber",
                    $"Low stock: {m.Name}",
                    $"{m.CurrentStock} remaining — reorder needed",
                    now,
                    $"مخزون منخفض: {arabicName}",
                    $"المتبقي {m.CurrentStock} — يحتاج طلب"));
            }

            var soon = now.AddDays(3);
            var expiringQuery = _db.StockBatches.AsNoTracking()
                .Where(b => b.RemainingQuantity > 0 && b.ExpiryDate >= now && b.ExpiryDate <= soon);
            if (branchId.HasValue)
                expiringQuery = expiringQuery.Where(b => b.BranchId == branchId.Value);

            var expiring = await expiringQuery
                .OrderBy(b => b.ExpiryDate)
                .Take(2)
                .Select(b => new { b.RawMaterial.Name, b.RawMaterial.NameAr, b.ExpiryDate, b.RemainingQuantity })
                .ToListAsync(ct);
            foreach (var b in expiring)
            {
                var days = Math.Max(0, (int)(b.ExpiryDate - now).TotalDays);
                var arabicName = string.IsNullOrWhiteSpace(b.NameAr) ? b.Name : b.NameAr;
                alerts.Add(new DashboardAlertDto("EXPIRY_SOON", "amber",
                    $"Expiring in {days}d: {b.Name}",
                    $"{b.RemainingQuantity} remaining",
                    now,
                    $"ينتهي خلال {days} يوم: {arabicName}",
                    $"المتبقي {b.RemainingQuantity}"));
            }

            var stale = await _q.AllOrders()
                .Where(OrderLifecyclePredicates.StaleSince(now - TimeSpan.FromMinutes(15)))
                .OrderBy(o => o.CreatedAt)
                .Take(2)
                .Select(o => new { o.OrderNumber, o.TableName, o.CreatedAt })
                .ToListAsync(ct);
            foreach (var o in stale)
            {
                var mins = (int)(now - o.CreatedAt).TotalMinutes;
                alerts.Add(new DashboardAlertDto("STALE_ORDER", "red",
                    $"Order {o.OrderNumber} waiting {mins} min",
                    string.IsNullOrEmpty(o.TableName) ? "Takeaway" : $"Table: {o.TableName}",
                    o.CreatedAt));
            }

            return alerts;
        }

        private async Task<IReadOnlyList<ActivityEntryDto>> BuildActivityAsync(CancellationToken ct)
        {
            var since = DateTime.UtcNow.AddHours(-6);
            var branchId = _ctx.Filter.BranchId;

            var paid = await _q.AllOrders()
                .Where(o => o.PaidAt.HasValue && o.PaidAt >= since)
                .OrderByDescending(o => o.PaidAt)
                .Take(4)
                .Select(o => new
                {
                    o.OrderNumber,
                    o.TotalAmount,
                    o.PaymentMethod,
                    o.PaidAt,
                    LatestMethod = o.Payments
                        .OrderByDescending(p => p.CreatedAt)
                        .Select(p => p.Method)
                        .FirstOrDefault(),
                    LatestPaymentMethodName = o.Payments
                        .OrderByDescending(p => p.CreatedAt)
                        .Select(p => p.PaymentMethodName)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            var paidActivity = paid.Select(o => new ActivityEntryDto(
                "OrderPaid",
                $"Order {o.OrderNumber} paid",
                $"{o.TotalAmount:0.00} · {ResolveActivityPaymentLabel(o.PaymentMethod, o.LatestMethod, o.LatestPaymentMethodName)}",
                o.PaidAt!.Value,
                "green"));

            var waste = _q.ExcludesWasteLogs
                ? new List<ActivityEntryDto>()
                : await BuildWasteActivityQuery(since, branchId)
                    .OrderByDescending(w => w.WasteDate ?? w.CreatedAt)
                    .Take(2)
                    .Select(w => new ActivityEntryDto(
                        "WasteLogged",
                        $"Waste recorded: {w.ItemName}",
                        $"{w.Quantity} {w.Unit} · {w.CostAmount:0.00} loss",
                        w.WasteDate ?? w.CreatedAt,
                        "red"))
                    .ToListAsync(ct);

            var cancels = await _q.AllCancelLogs()
                .Where(c => c.CancelledAt >= since)
                .OrderByDescending(c => c.CancelledAt)
                .Take(2)
                .Select(c => new ActivityEntryDto(
                    "OrderCancelled",
                    $"Cancelled by {c.CancelledByName}",
                    c.CancelReasonCode,
                    c.CancelledAt,
                    "amber"))
                .ToListAsync(ct);

            return paidActivity.Concat(waste).Concat(cancels)
                .OrderByDescending(a => a.OccurredAtUtc)
                .Take(6)
                .ToList();
        }

        private IQueryable<WasteLog> BuildWasteActivityQuery(DateTime since, Guid? branchId)
        {
            var query = _db.WasteLogs.AsNoTracking()
                .Where(w => (w.WasteDate ?? w.CreatedAt) >= since);
            if (branchId.HasValue)
                query = query.Where(w => w.BranchId == branchId.Value);
            return query;
        }

        private static string ResolveActivityPaymentLabel(
            string? orderPaymentMethod,
            string? latestMethod,
            string? latestPaymentMethodName)
        {
            if (!string.IsNullOrWhiteSpace(latestPaymentMethodName))
                return latestPaymentMethodName.Trim();

            var method = !string.IsNullOrWhiteSpace(latestMethod) ? latestMethod : orderPaymentMethod;
            return string.IsNullOrWhiteSpace(method)
                ? "—"
                : OrderPaymentHelper.ResolveLegacyBucket(method).Label;
        }
    }
}
