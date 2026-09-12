using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    public class OrderMetricsService : IOrderMetricsService
    {
        private readonly PosDbContext _context;
        private readonly IBranchContext _branchContext;
        private readonly ICurrentUserAccessor _currentUser;

        public OrderMetricsService(
            PosDbContext context,
            IBranchContext branchContext,
            ICurrentUserAccessor currentUser)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<decimal> GetTotalRevenueAsync(
            Guid tenantId,
            DateTime startDate,
            DateTime endDate,
            Guid? branchId,
            CancellationToken ct)
        {
            var resolvedBranchId = await ResolveReportBranchIdAsync(branchId, ct);
            var total = await PaidOrders(tenantId, startDate, endDate, resolvedBranchId)
                .SumAsync(o => (decimal?)o.TotalAmount, ct);

            return total ?? 0m;
        }

        public async Task<int> GetTotalOrdersAsync(
            Guid tenantId,
            DateTime startDate,
            DateTime endDate,
            Guid? branchId,
            CancellationToken ct)
        {
            var resolvedBranchId = await ResolveReportBranchIdAsync(branchId, ct);
            return await PaidOrders(tenantId, startDate, endDate, resolvedBranchId).CountAsync(ct);
        }

        public async Task<int> GetActiveOrdersAsync(Guid tenantId, Guid? branchId, CancellationToken ct)
        {
            var resolvedBranchId = await ResolveReportBranchIdAsync(branchId, ct);
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.TenantId == tenantId)
                .WhereActiveLifecycle();
            if (resolvedBranchId.HasValue)
                query = query.Where(o => o.BranchId == resolvedBranchId.Value);
            return await query.CountAsync(ct);
        }

        public async Task<int> GetCompletedOrdersAsync(
            Guid tenantId,
            DateTime startDate,
            DateTime endDate,
            Guid? branchId,
            CancellationToken ct)
        {
            var resolvedBranchId = await ResolveReportBranchIdAsync(branchId, ct);
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.TenantId == tenantId && o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .WhereCompletedLifecycle();
            if (resolvedBranchId.HasValue)
                query = query.Where(o => o.BranchId == resolvedBranchId.Value);
            return await query.CountAsync(ct);
        }

        public async Task<AnalyticsResponseDto> GetAnalyticsAsync(
            Guid tenantId,
            DateTime? startDate,
            DateTime? endDate,
            Guid? branchId,
            CancellationToken ct)
        {
            var resolvedBranchId = await ResolveReportBranchIdAsync(branchId, ct);
            var start = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var end = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var orders = await PaidOrders(tenantId, start, end, resolvedBranchId)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .ToListAsync(ct);

            if (orders.Count == 0)
                return EmptyAnalytics();

            var menuEngineering = CalculateMenuEngineering(orders);
            var heatmapData = CalculateHeatmapData(orders);

            return new AnalyticsResponseDto
            {
                MenuEngineering = menuEngineering,
                HeatmapData = heatmapData,
                Summary = CalculateSummary(orders, menuEngineering, heatmapData)
            };
        }

        public async Task<DailySummaryDto> GetDailySummaryAsync(Guid? branchId, CancellationToken ct)
        {
            var resolvedBranchId = await ResolveReportBranchIdAsync(branchId, ct);
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var paidOrders = await GetPaidOrdersWithPaymentsAsync(today, tomorrow, resolvedBranchId, ct);
            var totalOrdersCreated = await CountCreatedOrdersAsync(today, tomorrow, resolvedBranchId, ct);
            var cancelledOrders = await CountCancelledOrdersAsync(today, tomorrow, resolvedBranchId, ct);

            return new DailySummaryDto
            {
                TotalRevenue = paidOrders.Sum(o => o.TotalAmount),
                TotalOrders = paidOrders.Count,
                TotalCashOrders = paidOrders.Count(HasCashPayment),
                TotalOnlineOrders = paidOrders.Count(HasNonCashPayment),
                TotalOrdersCreated = totalOrdersCreated,
                CancelledOrders = cancelledOrders,
                CancellationRate = totalOrdersCreated == 0 ? 0 : Math.Round((decimal)cancelledOrders / totalOrdersCreated * 100m, 2),
                CancelWasteCount = await CountWasteAsync(WasteCategory.CancelProduct, today, tomorrow, resolvedBranchId, ct),
                ExpiryWasteCount = await CountWasteAsync(WasteCategory.ExpiryProduct, today, tomorrow, resolvedBranchId, ct),
                ActiveTables = await CountActiveTablesAsync(resolvedBranchId, ct),
                BestSellingProducts = await GetBestSellingProductsAsync(paidOrders.Select(o => o.Id).ToList(), ct),
                HourlySales = GetHourlySales(paidOrders),
                RecentOrders = await GetRecentOrdersAsync(today, tomorrow, resolvedBranchId, ct),
                PaymentMethodBreakdown = GetPaymentMethodBreakdown(paidOrders)
            };
        }

        public async Task<List<DailyReportDto>> GetZReportAsync(
            DateTime? startDate,
            DateTime? endDate,
            Guid? branchId,
            CancellationToken ct)
        {
            var resolvedBranchId = await ResolveReportBranchIdAsync(branchId, ct);
            var start = startDate?.Date ?? DateTime.UtcNow.Date.AddDays(-30);
            var end = endDate?.Date.AddDays(1) ?? DateTime.UtcNow.Date.AddDays(1);
            var paidOrders = await GetPaidOrdersWithPaymentsAsync(start, end, resolvedBranchId, ct);

            return paidOrders
                .GroupBy(o => GetPaidBucket(o).Date)
                .Select(g => new DailyReportDto
                {
                    Date = g.Key,
                    TotalOrders = g.Count(),
                    CashPayments = g.Sum(GetCashPaidAmount),
                    CardPayments = g.Sum(GetCardPaidAmount),
                    TotalRevenue = g.Sum(o => o.TotalAmount),
                    PaymentMethodBreakdown = GetPaymentMethodBreakdown(g)
                })
                .OrderByDescending(r => r.Date)
                .ToList();
        }

        private IQueryable<Order> PaidOrders(Guid tenantId, DateTime startDate, DateTime endDate, Guid? branchId)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.TenantId == tenantId
                    && (o.PaidAt ?? o.CreatedAt) >= startDate
                    && (o.PaidAt ?? o.CreatedAt) <= endDate)
                .WherePaid();
            if (branchId.HasValue)
                query = query.Where(o => o.BranchId == branchId.Value);
            return query;
        }

        private IQueryable<Order> PaidOrders(DateTime startDate, DateTime endDate, Guid? branchId)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => (o.PaidAt ?? o.CreatedAt) >= startDate && (o.PaidAt ?? o.CreatedAt) < endDate)
                .WherePaid();
            if (branchId.HasValue)
                query = query.Where(o => o.BranchId == branchId.Value);
            return query;
        }

        private Task<List<Order>> GetPaidOrdersWithPaymentsAsync(
            DateTime startDate,
            DateTime endDate,
            Guid? branchId,
            CancellationToken ct)
        {
            return PaidOrders(startDate, endDate, branchId)
                .Include(o => o.Payments)
                .ToListAsync(ct);
        }

        private Task<int> CountCreatedOrdersAsync(
            DateTime startDate,
            DateTime endDate,
            Guid? branchId,
            CancellationToken ct)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt < endDate);
            if (branchId.HasValue)
                query = query.Where(o => o.BranchId == branchId.Value);
            return query.CountAsync(ct);
        }

        private Task<int> CountCancelledOrdersAsync(
            DateTime startDate,
            DateTime endDate,
            Guid? branchId,
            CancellationToken ct)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.Status == OrderStatus.Cancelled && o.CreatedAt >= startDate && o.CreatedAt < endDate);
            if (branchId.HasValue)
                query = query.Where(o => o.BranchId == branchId.Value);
            return query.CountAsync(ct);
        }

        private Task<int> CountWasteAsync(
            WasteCategory category,
            DateTime startDate,
            DateTime endDate,
            Guid? branchId,
            CancellationToken ct)
        {
            var query = _context.WasteLogs
                .AsNoTracking()
                .Where(w => w.Category == category && w.CreatedAt >= startDate && w.CreatedAt < endDate);
            if (branchId.HasValue)
                query = query.Where(w => w.BranchId == branchId.Value);
            return query.CountAsync(ct);
        }

        private async Task<int> CountActiveTablesAsync(Guid? branchId, CancellationToken ct)
        {
            if (branchId.HasValue && !await IsMainBranchAsync(branchId.Value, ct))
                return 0;

            return await _context.Tables
                .AsNoTracking()
                .CountAsync(t => t.Status == TableStatus.Occupied, ct);
        }

        private Task<bool> IsMainBranchAsync(Guid branchId, CancellationToken ct)
            => _context.Branches
                .AsNoTracking()
                .AnyAsync(branch => branch.Id == branchId && branch.IsMainBranch, ct);

        private Task<List<BestSellingProductDto>> GetBestSellingProductsAsync(List<Guid> paidOrderIds, CancellationToken ct)
        {
            if (paidOrderIds.Count == 0)
                return Task.FromResult(new List<BestSellingProductDto>());

            return _context.OrderItems
                .AsNoTracking()
                .Where(oi => paidOrderIds.Contains(oi.OrderId))
                .GroupBy(oi => new { oi.ProductId, oi.ProductName })
                .Select(g => new BestSellingProductDto
                {
                    ProductName = g.Key.ProductName,
                    Quantity = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.Price * oi.Quantity)
                })
                .OrderByDescending(p => p.Quantity)
                .Take(3)
                .ToListAsync(ct);
        }

        private Task<List<RecentOrderDto>> GetRecentOrdersAsync(
            DateTime startDate,
            DateTime endDate,
            Guid? branchId,
            CancellationToken ct)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt < endDate);
            if (branchId.HasValue)
                query = query.Where(o => o.BranchId == branchId.Value);

            return query
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .Select(o => new RecentOrderDto
                {
                    Id = o.OrderNumber,
                    Table = o.TableName ?? "Paket",
                    Status = o.Status.ToString(),
                    Total = o.TotalAmount,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync(ct);
        }

        private async Task<Guid?> ResolveReportBranchIdAsync(Guid? requestedBranchId, CancellationToken ct)
        {
            if (requestedBranchId == Guid.Empty)
            {
                if (!_currentUser.IsAdminOrManager)
                    throw new ForbiddenException("All branches report scope requires administrator or manager access.");
                return null;
            }

            var context = await _branchContext.GetCurrentAsync(ct);
            if (!requestedBranchId.HasValue)
                return context.CurrentBranch.Id;

            var selected = context.AssignedBranches.FirstOrDefault(b => b.Id == requestedBranchId.Value);
            if (selected is null)
                throw new ForbiddenException("You are not assigned to the selected branch.");
            if (!selected.IsActive)
                throw new ValidationException("Inactive branches cannot be used as report scope.");

            return requestedBranchId.Value;
        }

        private static List<HourlySalesDto> GetHourlySales(IEnumerable<Order> paidOrders)
        {
            return paidOrders
                .GroupBy(o => GetPaidBucket(o).Hour)
                .Select(g => new HourlySalesDto
                {
                    Hour = $"{g.Key:00}:00",
                    Sales = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(x => x.Hour)
                .ToList();
        }

        private static bool HasCashPayment(Order order)
        {
            return order.Payments.Any()
                ? order.Payments.Any(p => OrderPaymentHelper.IsCashMethod(p.Method))
                : OrderPaymentHelper.IsCashMethod(order.PaymentMethod);
        }

        private static bool HasNonCashPayment(Order order)
        {
            return order.Payments.Any()
                ? order.Payments.Any(p => !OrderPaymentHelper.IsCashMethod(p.Method))
                : HasNonCashPaymentMethod(order.PaymentMethod);
        }

        private static decimal GetCashPaidAmount(Order order)
        {
            return order.Payments.Any()
                ? order.Payments.Where(p => OrderPaymentHelper.IsCashMethod(p.Method)).Sum(p => p.Amount)
                : OrderPaymentHelper.IsCashMethod(order.PaymentMethod) ? order.TotalAmount : 0m;
        }

        private static decimal GetCardPaidAmount(Order order)
        {
            return order.Payments.Any()
                ? order.Payments.Where(p => !OrderPaymentHelper.IsCashMethod(p.Method)).Sum(p => p.Amount)
                : OrderPaymentHelper.IsCashMethod(order.PaymentMethod) ? 0m : order.TotalAmount;
        }

        private static List<PaymentMethodBreakdownDto> GetPaymentMethodBreakdown(IEnumerable<Order> orders)
        {
            return orders
                .SelectMany(GetPaymentRows)
                .GroupBy(row => row.Method, StringComparer.OrdinalIgnoreCase)
                .Select(g => new PaymentMethodBreakdownDto
                {
                    Method = g.First().Method,
                    Orders = g.Select(row => row.OrderId).Distinct().Count(),
                    Amount = g.Sum(row => row.Amount)
                })
                .OrderByDescending(row => row.Amount)
                .ThenBy(row => row.Method)
                .ToList();
        }

        private static IEnumerable<PaymentBreakdownRow> GetPaymentRows(Order order)
        {
            if (order.Payments.Any())
            {
                return order.Payments.Select(payment => new PaymentBreakdownRow(
                    order.Id,
                    ResolveBreakdownLabel(payment),
                    payment.Amount));
            }

            // Order pre-dates the Payments table — fold legacy free-text into our
            // standard buckets so reports group with the rest of the dashboard.
            var bucket = OrderPaymentHelper.ResolveLegacyBucket(order.PaymentMethod);
            return new[] { new PaymentBreakdownRow(order.Id, bucket.Label, order.TotalAmount) };
        }

        private static string ResolveBreakdownLabel(Payment payment)
        {
            // Configured-method snapshot wins (real provider name appears).
            if (!string.IsNullOrWhiteSpace(payment.PaymentMethodName))
                return payment.PaymentMethodName!.Trim();

            // Legacy snapshot only — fold every non-cash variant into one bucket
            // so "Card" / "Credit Card" / "Online" don't appear as separate slices.
            return OrderPaymentHelper.ResolveLegacyBucket(payment.Method).Label;
        }

        private static bool HasNonCashPaymentMethod(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return false;

            return paymentMethod
                .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(method => !OrderPaymentHelper.IsCashMethod(method));
        }

        private static AnalyticsResponseDto EmptyAnalytics()
        {
            return new AnalyticsResponseDto
            {
                MenuEngineering = new List<MenuEngineeringDto>(),
                HeatmapData = new List<HeatmapDataDto>(),
                Summary = new AnalyticsSummaryDto()
            };
        }

        private static List<MenuEngineeringDto> CalculateMenuEngineering(List<Order> orders)
        {
            var productStats = orders
                .SelectMany(o => o.OrderItems)
                .GroupBy(oi => new { oi.ProductId, oi.ProductName })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.ProductName,
                    SalesVolume = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.Price * oi.Quantity),
                    TotalCost = g.Sum(oi => (oi.Product?.CostPrice ?? 0) * oi.Quantity)
                })
                .Select(p => new
                {
                    p.ProductId,
                    p.ProductName,
                    p.SalesVolume,
                    p.Revenue,
                    TotalProfit = p.Revenue - p.TotalCost,
                    ProfitMargin = p.Revenue > 0 ? ((p.Revenue - p.TotalCost) / p.Revenue) * 100 : 0
                })
                .ToList();

            if (productStats.Count == 0)
                return new List<MenuEngineeringDto>();

            var medianSales = CalculateMedian(productStats.Select(p => (double)p.SalesVolume).ToList());
            var medianMargin = CalculateMedian(productStats.Select(p => (double)p.ProfitMargin).ToList());

            return productStats.Select(p => new MenuEngineeringDto
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                SalesVolume = p.SalesVolume,
                ProfitMargin = p.ProfitMargin,
                Revenue = p.Revenue,
                TotalProfit = p.TotalProfit,
                Category = CategorizeProduct(p.SalesVolume, p.ProfitMargin, medianSales, medianMargin)
            }).ToList();
        }

        private static MenuEngineeringCategory CategorizeProduct(
            int salesVolume,
            decimal profitMargin,
            double medianSales,
            double medianMargin)
        {
            var highSales = salesVolume >= medianSales;
            var highMargin = profitMargin >= (decimal)medianMargin;

            if (highSales && highMargin)
                return MenuEngineeringCategory.Star;
            if (highSales)
                return MenuEngineeringCategory.Plow;
            return highMargin ? MenuEngineeringCategory.Puzzle : MenuEngineeringCategory.Dog;
        }

        private static double CalculateMedian(List<double> values)
        {
            if (values.Count == 0)
                return 0;

            var sorted = values.OrderBy(v => v).ToList();
            var count = sorted.Count;
            return count % 2 == 0
                ? (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0
                : sorted[count / 2];
        }

        private static List<HeatmapDataDto> CalculateHeatmapData(List<Order> orders)
        {
            var turkishDays = new[] { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };
            var hourlyData = orders
                .GroupBy(o => new { DayOfWeek = (int)GetPaidBucket(o).DayOfWeek, Hour = GetPaidBucket(o).Hour })
                .Select(g => new
                {
                    DayIndex = g.Key.DayOfWeek == 0 ? 6 : g.Key.DayOfWeek - 1,
                    g.Key.Hour,
                    AverageRevenue = g.Average(o => o.TotalAmount),
                    OrderCount = g.Count(),
                    AverageWaiters = g.Select(o => o.WaiterId).Distinct().Count()
                })
                .ToList();

            if (hourlyData.Count == 0)
                return new List<HeatmapDataDto>();

            var maxRevenue = hourlyData.Max(h => h.AverageRevenue);
            return hourlyData.Select(h => new HeatmapDataDto
            {
                DayOfWeek = turkishDays[h.DayIndex],
                DayIndex = h.DayIndex,
                Hour = h.Hour,
                AverageRevenue = h.AverageRevenue,
                AverageWaiters = h.AverageWaiters,
                OrderCount = h.OrderCount,
                Intensity = maxRevenue > 0 ? (double)(h.AverageRevenue / maxRevenue) : 0
            }).ToList();
        }

        private static AnalyticsSummaryDto CalculateSummary(
            List<Order> orders,
            List<MenuEngineeringDto> menuEngineering,
            List<HeatmapDataDto> heatmapData)
        {
            var totalRevenue = orders.Sum(o => o.TotalAmount);
            var totalProfit = orders.Sum(o => o.NetProfit);
            var bestSelling = menuEngineering.OrderByDescending(m => m.SalesVolume).FirstOrDefault();
            var mostProfitable = menuEngineering.OrderByDescending(m => m.TotalProfit).FirstOrDefault();
            var peakHourData = heatmapData.OrderByDescending(h => h.AverageRevenue).FirstOrDefault();
            var peakDayData = GetPeakDay(heatmapData);

            return new AnalyticsSummaryDto
            {
                TotalRevenue = totalRevenue,
                TotalProfit = totalProfit,
                AverageProfitMargin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0,
                TotalOrders = orders.Count,
                TotalProductsSold = orders.SelectMany(o => o.OrderItems).Sum(oi => oi.Quantity),
                BestSellingProduct = bestSelling?.ProductName ?? "N/A",
                MostProfitableProduct = mostProfitable?.ProductName ?? "N/A",
                PeakHour = peakHourData != null ? $"{peakHourData.Hour}:00" : "N/A",
                PeakDay = peakDayData ?? "N/A"
            };
        }

        private static string? GetPeakDay(List<HeatmapDataDto> heatmapData)
        {
            return heatmapData
                .GroupBy(h => h.DayOfWeek)
                .Select(g => new { Day = g.Key, Revenue = g.Sum(h => h.AverageRevenue) })
                .OrderByDescending(d => d.Revenue)
                .FirstOrDefault()
                ?.Day;
        }

        private static DateTime GetPaidBucket(Order order)
            => order.PaidAt ?? order.CreatedAt;

        private sealed record PaymentBreakdownRow(Guid OrderId, string Method, decimal Amount);
    }
}
