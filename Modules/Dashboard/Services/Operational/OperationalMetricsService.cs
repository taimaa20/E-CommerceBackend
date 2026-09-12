using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Operations;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Predicates;
using RestaurantPos.Api.Modules.Dashboard.Queries;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Operational
{
    /// <summary>
    /// Live operations metrics. Dashboard counters honour the selected window
    /// so the KPI row, charts, and tables stay numerically consistent when the
    /// user switches Today / Week / Month. Health widgets remain point-in-time
    /// where no historical table/session state exists.
    /// </summary>
    public sealed class OperationalMetricsService : IOperationalMetricsService
    {
        // Threshold for "stale" orders shown on the operations dashboard. The
        // architecture spec calls this out explicitly (Orders waiting > 15 min).
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(15);

        private readonly IMetricQueryBuilder _q;
        private readonly IDashboardFilterContext _ctx;
        private readonly PosDbContext _db; // point-in-time queries that ignore the window
        private readonly ILogger<OperationalMetricsService> _logger;

        public OperationalMetricsService(IMetricQueryBuilder q, IDashboardFilterContext ctx, PosDbContext db, ILogger<OperationalMetricsService> logger)
        {
            _q   = q   ?? throw new ArgumentNullException(nameof(q));
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _db  = db  ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OperationsSnapshotDto> BuildSnapshotAsync(CancellationToken ct)
        {
            var counters     = await SafeAsync.RunAsync(_logger, "ops.counters",     () => BuildOrderCountersAsync(ct),  new OrderCountersDto());
            var revenue      = await SafeAsync.RunAsync(_logger, "ops.revenue",      () => BuildRevenueAsync(ct),        new LiveRevenueDto());
            var health       = await SafeAsync.RunAsync(_logger, "ops.health",       () => BuildHealthAsync(ct),         new OperationsHealthDto());
            var hourly       = await SafeAsync.RunAsync(_logger, "ops.hourly",       () => BuildHourlySeriesAsync(ct),   (IReadOnlyList<TimeSeriesPointDto>)Array.Empty<TimeSeriesPointDto>());
            var typeSplit    = await SafeAsync.RunAsync(_logger, "ops.typeSplit",    () => BuildOrderTypeSplitAsync(ct), (IReadOnlyList<CategorySliceDto>)Array.Empty<CategorySliceDto>());
            var topProducts  = await SafeAsync.RunAsync(_logger, "ops.topProducts",  () => BuildTopProductsAsync(ct),    (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var alerts       = await SafeAsync.RunAsync(_logger, "ops.alerts",       () => BuildAlertsAsync(ct),         (IReadOnlyList<AlertDto>)Array.Empty<AlertDto>());
            var recent       = await SafeAsync.RunAsync(_logger, "ops.recentOrders", () => BuildRecentOrdersAsync(ct),   (IReadOnlyList<LivePipelineOrderDto>)Array.Empty<LivePipelineOrderDto>());

            return new OperationsSnapshotDto
            {
                Scope          = _ctx.Scope,
                WindowStartUtc = _ctx.Window.StartUtc,
                WindowEndUtc   = _ctx.Window.EndUtc,
                AppliedFilter  = _ctx.Filter,
                Orders         = counters,
                Revenue        = revenue,
                Health         = health,
                HourlySeries   = hourly,
                OrderTypeSplit = typeSplit,
                TopProducts    = topProducts,
                Alerts         = alerts,
                RecentOrders   = recent
            };
        }

        private async Task<IReadOnlyList<LivePipelineOrderDto>> BuildRecentOrdersAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var rows = await _q.Orders()
                .OrderByDescending(o => o.CreatedAt)
                .Take(12)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    TableName = o.TableName ?? "",
                    o.OrderType,
                ItemCount = o.OrderItems.Select(i => (int?)i.Quantity).Sum() ?? 0,
                    o.TotalAmount,
                    o.Status,
                    o.CreatedAt,
                    CashierName = o.PaidByUser != null ? (o.PaidByUser.FullName ?? o.PaidByUser.Username) : null,
                    CashierNameAr = o.PaidByUser != null ? o.PaidByUser.FullNameAr : null
                })
                .ToListAsync(ct);

            return rows.Select(r =>
            {
                var tone = r.Status switch
                {
                    OrderStatus.New        => "blue",
                    OrderStatus.Preparing  => "amber",
                    OrderStatus.Ready      => "green",
                    OrderStatus.Served     => "neutral",
                    OrderStatus.Paid       => "green",
                    OrderStatus.Completed  => "neutral",
                    OrderStatus.Cancelled  => "red",
                    _                      => "neutral"
                };
                // "Stale" check — preparing/ready orders older than 15 min escalate to red.
                if ((r.Status == OrderStatus.Preparing || r.Status == OrderStatus.Ready)
                    && (now - r.CreatedAt).TotalMinutes >= 15)
                {
                    tone = "red";
                }
            var itemCount = r.ItemCount > 0 || r.TotalAmount <= 0 ? r.ItemCount : 1;

            return new LivePipelineOrderDto(
                r.Id,
                r.OrderNumber,
                string.IsNullOrEmpty(r.TableName) ? "Takeaway" : r.TableName,
                r.OrderType.ToString(),
                itemCount,
                    r.TotalAmount,
                    r.Status.ToString(),
                    tone,
                    r.CreatedAt,
                    Math.Max(0, (int)(now - r.CreatedAt).TotalSeconds),
                    r.CashierName,
                    r.CashierNameAr);
            }).ToList();
        }

        private async Task<OrderCountersDto> BuildOrderCountersAsync(CancellationToken ct)
        {
            var inWindow = _q.Orders();
            var total      = await inWindow.CountAsync(ct);
            var waiting    = await inWindow.CountAsync(OrderLifecyclePredicates.Waiting,   ct);
            var preparing  = await inWindow.CountAsync(OrderLifecyclePredicates.Preparing, ct);
            var ready      = await inWindow.CountAsync(OrderLifecyclePredicates.Ready,     ct);
            var staleSince = DateTime.UtcNow - StaleThreshold;
            var stale      = await inWindow.CountAsync(OrderLifecyclePredicates.StaleSince(staleSince), ct);
            var paid      = await inWindow.Where(OrderLifecyclePredicates.Confirmed).CountAsync(ct);
            var served    = await inWindow.CountAsync(o => o.Status == OrderStatus.Served, ct);
            var cancelled = await inWindow.Where(OrderLifecyclePredicates.Cancelled).CountAsync(ct);
            var refunded  = await _q.RefundLogs().CountAsync(ct);

            decimal aov = 0m;
            if (paid > 0)
            {
                aov = await inWindow.Where(OrderLifecyclePredicates.Confirmed)
                                    .AverageAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
            }

            return new OrderCountersDto
            {
                Total              = total,
                Active             = waiting + preparing + ready,
                Waiting            = waiting,
                Preparing          = preparing,
                Ready              = ready,
                Paid               = paid,
                Served             = served,
                Cancelled          = cancelled,
                Refunded           = refunded,
                StaleOverThreshold = stale,
                AverageOrderValue  = Math.Round(aov, 2)
            };
        }

        private async Task<LiveRevenueDto> BuildRevenueAsync(CancellationToken ct)
        {
            var inWindow = _q.Orders();

            var confirmed = await inWindow.Where(OrderLifecyclePredicates.Confirmed)
                                          .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
            var pending   = await inWindow.Where(OrderLifecyclePredicates.Pending)
                                          .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

            // Gross / discounts use the same Confirmed set and the same field
            // definitions as the Financial dashboard (Gross = Subtotal,
            // Discounts = DiscountAmount). Net = confirmed collected: a refunded
            // order's status flips to Cancelled and is already excluded by the
            // Confirmed predicate, so there is no separate refund line to deduct.
            var grossAgg = await inWindow.Where(OrderLifecyclePredicates.Confirmed)
                .GroupBy(_ => 1)
                .Select(g => new { Gross = g.Sum(o => o.Subtotal), Discounts = g.Sum(o => o.DiscountAmount) })
                .FirstOrDefaultAsync(ct);
            var gross     = grossAgg?.Gross ?? 0m;
            var discounts = grossAgg?.Discounts ?? 0m;

            // Cash vs card — recognise tenant-specific cash labels via OrderPaymentHelper,
            // honouring split payments where the Payments table is populated.
            var paidProjection = await inWindow.Where(OrderLifecyclePredicates.Confirmed)
                .Select(o => new
                {
                    o.Id,
                    o.OrderType,
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

            decimal cash = 0, card = 0;
            var buckets = new Dictionary<string, PaymentBucket>(StringComparer.OrdinalIgnoreCase);
            var combinedBuckets = new Dictionary<string, PaymentOrderTypeBucket>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in paidProjection)
            {
                if (o.Payments.Count > 0)
                {
                    foreach (var p in o.Payments)
                    {
                        var payment = ResolvePaymentBucket(
                            p.PaymentMethodCode,
                            p.PaymentMethodName,
                            p.PaymentMethodNameAr,
                            p.Method);
                        if (IsCashPayment(p.Method, p.PaymentMethodCode, p.PaymentMethodName)) cash += p.Amount;
                        else                                                                   card += p.Amount;
                        AddPaymentBucket(buckets, payment, o.Id, p.Amount);
                        AddPaymentOrderTypeBucket(combinedBuckets, o.OrderType, payment, o.Id, p.Amount);
                    }
                }
                else
                {
                    var payment = ResolveLegacyPaymentBucket(o.PaymentMethod);
                    if (OrderPaymentHelper.IsCashMethod(o.PaymentMethod)) cash += o.TotalAmount;
                    else                                                   card += o.TotalAmount;
                    AddPaymentBucket(buckets, payment, o.Id, o.TotalAmount);
                    AddPaymentOrderTypeBucket(combinedBuckets, o.OrderType, payment, o.Id, o.TotalAmount);
                }
            }

            var total = cash + card;
            return new LiveRevenueDto
            {
                Confirmed      = Math.Round(confirmed, 2),
                Gross          = Math.Round(gross,     2),
                Discounts      = Math.Round(discounts, 2),
                Net            = Math.Round(confirmed, 2),
                Pending        = Math.Round(pending,   2),
                Cash           = Math.Round(cash,      2),
                Card           = Math.Round(card,      2),
                CashPercentage = total == 0 ? 0 : Math.Round(cash / total * 100m, 2),
                CardPercentage = total == 0 ? 0 : Math.Round(card / total * 100m, 2),
                PaymentBreakdown = BuildPaymentBreakdown(buckets),
                PaymentOrderTypeBreakdown = BuildPaymentOrderTypeBreakdown(combinedBuckets)
            };
        }

        private async Task<OperationsHealthDto> BuildHealthAsync(CancellationToken ct)
        {
            var branchId = _ctx.Filter.BranchId;
            var occupiedTables = await CountOccupiedTablesAsync(branchId, ct);
            var kitchenQueue = await _q.AllOrders()
                .CountAsync(o => o.Status == OrderStatus.Preparing, ct);
            var onlineLive = await _q.AllOrders()
                .CountAsync(o => o.OrderType == OrderType.Takeaway
                              && (o.Status == OrderStatus.New || o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Ready), ct);
            var activeSessionQuery = _db.CashierBalanceShifts.AsNoTracking()
                .Where(s => s.ClosedAt == null);
            if (branchId.HasValue)
                activeSessionQuery = activeSessionQuery.Where(s => s.BranchId == branchId.Value);
            var activeSessions = await activeSessionQuery.CountAsync(ct);

            // Real kitchen preparation time = ReadyAt − CreatedAt. ReadyAt is the
            // canonical "first moment every item was ready" stamp (OrderCompletion).
            // Orders without ReadyAt (legacy, or never fully prepared) are excluded;
            // PrepOrderCount drives the UI empty state so "no data" never shows as 0.
            // Diff computed client-side — Npgsql does not translate DateDiffMinute.
            var prepRows = await _q.Orders()
                .Where(o => o.ReadyAt.HasValue)
                .Select(o => new { o.CreatedAt, Ready = o.ReadyAt!.Value })
                .ToListAsync(ct);

            var avgPrep = prepRows.Count == 0
                ? 0
                : prepRows.Average(r => (r.Ready - r.CreatedAt).TotalMinutes);

            return new OperationsHealthDto
            {
                ActiveDineInTables    = occupiedTables,
                KitchenQueueLength    = kitchenQueue,
                OnlineOrdersLive      = onlineLive,
                ActiveCashierSessions = activeSessions,
                AveragePrepMinutes    = Math.Round(avgPrep, 1),
                PrepOrderCount        = prepRows.Count
            };
        }

        private async Task<int> CountOccupiedTablesAsync(Guid? branchId, CancellationToken ct)
        {
            if (branchId.HasValue && !await IsMainBranchAsync(branchId.Value, ct))
                return 0;

            return await _db.Tables.AsNoTracking()
                .CountAsync(t => t.Status == TableStatus.Occupied, ct);
        }

        private Task<bool> IsMainBranchAsync(Guid branchId, CancellationToken ct)
            => _db.Branches
                .AsNoTracking()
                .AnyAsync(branch => branch.Id == branchId && branch.IsMainBranch, ct);

        private async Task<IReadOnlyList<TimeSeriesPointDto>> BuildHourlySeriesAsync(CancellationToken ct)
        {
            // Bucket on the database (server-side). Use DateTrunc via raw expression
            // would be ideal but EF Core's grouping on a constructed DateTime translates
            // cleanly on Npgsql for hour-of-day buckets.
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.Confirmed)
                .GroupBy(o => new
                {
                    Year = (o.PaidAt ?? o.CreatedAt).Year,
                    Month = (o.PaidAt ?? o.CreatedAt).Month,
                    Day = (o.PaidAt ?? o.CreatedAt).Day,
                    Hour = (o.PaidAt ?? o.CreatedAt).Hour
                })
                .Select(g => new
                {
                    g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour,
                    Total = g.Sum(o => o.TotalAmount),
                    Count = g.Count()
                })
                .ToListAsync(ct);

            return rows
                .Select(r => new TimeSeriesPointDto(
                    new DateTime(r.Year, r.Month, r.Day, r.Hour, 0, 0, DateTimeKind.Utc),
                    r.Total,
                    r.Count))
                .OrderBy(p => p.BucketStartUtc)
                .ToList();
        }

        private async Task<IReadOnlyList<CategorySliceDto>> BuildOrderTypeSplitAsync(CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.Confirmed)
                .GroupBy(o => o.OrderType)
                .Select(g => new { Type = g.Key, Total = g.Sum(o => o.TotalAmount), Count = g.Count() })
                .ToListAsync(ct);

            var total = rows.Sum(r => r.Total);
            return rows.Select(r => new CategorySliceDto(
                r.Type.ToString(), null, r.Total,
                total == 0 ? 0 : Math.Round(r.Total / total * 100m, 2),
                r.Count)).ToList();
        }

        private async Task<IReadOnlyList<RankedRowDto>> BuildTopProductsAsync(CancellationToken ct)
        {
            // Aggregate to anonymous shape (translatable), order on raw column,
            // then materialise into the record DTO. Avoids EF translation
            // edge cases around "OrderBy(dto.PropertyAfterProjection)".
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

        private async Task<IReadOnlyList<AlertDto>> BuildAlertsAsync(CancellationToken ct)
        {
            var alerts = new List<AlertDto>();
            var branchId = _ctx.Filter.BranchId;

            var lowStock = branchId.HasValue
                ? await _db.RawMaterials.AsNoTracking()
                    .Select(m => new
                    {
                        m.MinimumAlertLevel,
                        CurrentStock = _db.StockBatches
                            .Where(b => b.MaterialId == m.Id
                                     && b.BranchId == branchId.Value
                                     && b.IsApproved
                                     && b.RemainingQuantity > 0
                                     && b.Status == BatchStatus.Good)
                            .Sum(b => (decimal?)b.RemainingQuantity) ?? 0m
                    })
                    .CountAsync(m => m.CurrentStock <= m.MinimumAlertLevel, ct)
                : await _db.RawMaterials.AsNoTracking()
                    .CountAsync(m => m.CurrentStock <= m.MinimumAlertLevel, ct);
            if (lowStock > 0)
            {
                alerts.Add(new AlertDto(
                    Code: "LOW_STOCK",
                    Severity: "warning",
                    TitleKey: "dashboardAlertLowStock",
                    Message: $"{lowStock} item(s) below reorder level",
                    Payload: new { count = lowStock }));
            }

            var soon = DateTime.UtcNow.AddDays(3);
            var nowU = DateTime.UtcNow;
            var expiringQuery = _db.StockBatches.AsNoTracking()
                .Where(b => b.RemainingQuantity > 0 && b.ExpiryDate < soon && b.ExpiryDate >= nowU);
            if (branchId.HasValue)
                expiringQuery = expiringQuery.Where(b => b.BranchId == branchId.Value);
            var expiringSoon = await expiringQuery.CountAsync(ct);
            if (expiringSoon > 0)
            {
                alerts.Add(new AlertDto(
                    Code: "EXPIRY_SOON",
                    Severity: "warning",
                    TitleKey: "dashboardAlertExpirySoon",
                    Message: $"{expiringSoon} batch(es) expiring within 3 days",
                    Payload: new { count = expiringSoon }));
            }

            return alerts;
        }

        private static PaymentDescriptor ResolvePaymentBucket(
            string? code,
            string? name,
            string? nameAr,
            string? legacyMethod)
        {
            if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(name))
            {
                var key = !string.IsNullOrWhiteSpace(code) ? code.Trim() : name!.Trim();
                var label = !string.IsNullOrWhiteSpace(name) ? name.Trim() : key;
                return new PaymentDescriptor(
                    key,
                    label,
                    string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim());
            }

            return ResolveLegacyPaymentBucket(legacyMethod);
        }

        private static PaymentDescriptor ResolveLegacyPaymentBucket(
            string? legacyMethod)
        {
            var bucket = OrderPaymentHelper.ResolveLegacyBucket(legacyMethod);
            return new PaymentDescriptor(bucket.Key, bucket.Label, bucket.LabelAr);
        }

        private static bool IsCashPayment(
            string? method,
            string? code,
            string? name)
            => OrderPaymentHelper.IsCashMethod(method)
                || OrderPaymentHelper.IsCashMethod(code)
                || OrderPaymentHelper.IsCashMethod(name);

        private static void AddPaymentBucket(
            Dictionary<string, PaymentBucket> buckets,
            PaymentDescriptor payment,
            Guid orderId,
            decimal amount)
        {
            if (buckets.TryGetValue(payment.Key, out var current))
            {
                current.Value += amount;
                current.LabelAr ??= payment.LabelAr;
                current.OrderIds.Add(orderId);
                return;
            }

            buckets[payment.Key] = new PaymentBucket(
                payment.Label,
                payment.LabelAr,
                amount,
                new HashSet<Guid> { orderId });
        }

        private static void AddPaymentOrderTypeBucket(
            Dictionary<string, PaymentOrderTypeBucket> buckets,
            OrderType orderType,
            PaymentDescriptor payment,
            Guid orderId,
            decimal amount)
        {
            var key = $"{orderType}\u001f{payment.Key}";
            if (buckets.TryGetValue(key, out var current))
            {
                current.Value += amount;
                current.PaymentMethodAr ??= payment.LabelAr;
                current.OrderIds.Add(orderId);
                return;
            }

            buckets[key] = new PaymentOrderTypeBucket(
                orderType.ToString(),
                payment.Label,
                payment.LabelAr,
                amount,
                new HashSet<Guid> { orderId });
        }

        private static IReadOnlyList<CategorySliceDto> BuildPaymentBreakdown(Dictionary<string, PaymentBucket> buckets)
        {
            var total = buckets.Values.Sum(b => b.Value);
            return buckets.Values
                .OrderByDescending(b => b.Value)
                .Select(b => new CategorySliceDto(
                    b.Label,
                    b.LabelAr,
                    Math.Round(b.Value, 2),
                    total == 0 ? 0 : Math.Round(b.Value / total * 100m, 2),
                    b.OrderIds.Count))
                .ToList();
        }

        private static IReadOnlyList<PaymentOrderTypeSliceDto> BuildPaymentOrderTypeBreakdown(
            Dictionary<string, PaymentOrderTypeBucket> buckets)
        {
            var total = buckets.Values.Sum(b => b.Value);
            return buckets.Values
                .OrderBy(b => b.OrderType)
                .ThenByDescending(b => b.Value)
                .Select(b => new PaymentOrderTypeSliceDto(
                    b.OrderType,
                    b.PaymentMethod,
                    b.PaymentMethodAr,
                    Math.Round(b.Value, 2),
                    b.OrderIds.Count,
                    total == 0 ? 0 : Math.Round(b.Value / total * 100m, 2)))
                .ToList();
        }

        private sealed record PaymentDescriptor(string Key, string Label, string? LabelAr);

        private sealed class PaymentBucket(
            string label,
            string? labelAr,
            decimal value,
            HashSet<Guid> orderIds)
        {
            public string Label { get; } = label;
            public string? LabelAr { get; set; } = labelAr;
            public decimal Value { get; set; } = value;
            public HashSet<Guid> OrderIds { get; } = orderIds;
        }

        private sealed class PaymentOrderTypeBucket(
            string orderType,
            string paymentMethod,
            string? paymentMethodAr,
            decimal value,
            HashSet<Guid> orderIds)
        {
            public string OrderType { get; } = orderType;
            public string PaymentMethod { get; } = paymentMethod;
            public string? PaymentMethodAr { get; set; } = paymentMethodAr;
            public decimal Value { get; set; } = value;
            public HashSet<Guid> OrderIds { get; } = orderIds;
        }
    }
}
