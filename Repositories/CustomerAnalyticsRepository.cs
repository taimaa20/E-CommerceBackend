using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed class CustomerAnalyticsRepository : ICustomerAnalyticsRepository
    {
        private const string TalabatPartnerName = "Talabat";
        private const string DirectCustomerName = "POS Customer";
        private const string DirectCustomerNameAr = "عميل نقطة البيع";
        private const string PartnerCustomerName = "Partner Customer";
        private const string PartnerCustomerNameAr = "عميل شريك";
        private const int DashboardListLimit = 10;
        private const int ReturningOrderThreshold = 2;
        private const int LostCustomerDays = 90;
        private const int FrequentCustomerThreshold = 5;
        private const string DirectLabel = "Direct";
        private const string DirectLabelAr = "مباشر";
        private const string PartnerLabel = "Partner";
        private const string PartnerLabelAr = "شريك";
        private readonly PosDbContext _context;

        public CustomerAnalyticsRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<CustomerAnalyticsSnapshotDto> GetDashboardAsync(
            CustomerAnalyticsFilter filter,
            bool isArabic,
            CancellationToken ct)
        {
            var customers = await ProjectCustomers(filter.TenantId, isArabic).ToListAsync(ct);
            var orders = await ProjectOrders(filter).ToListAsync(ct);
            var rows = ApplyCustomerFilters(BuildCustomerRows(customers, orders), filter).ToList();
            return BuildSnapshot(rows, orders, filter);
        }

        private IQueryable<CustomerRow> ProjectCustomers(Guid tenantId, bool isArabic)
            => _context.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId)
                .Select(c => new CustomerRow
                {
                    Id = c.Id,
                    DisplayName = isArabic && c.NameAr != null && c.NameAr != string.Empty ? c.NameAr : c.Name,
                    PhoneNumber = c.PhoneNumber,
                    CreatedAt = c.CreatedAt,
                    IsActive = c.DeletedAt == null
                });

        private IQueryable<OrderRow> ProjectOrders(CustomerAnalyticsFilter filter)
        {
            var query = _context.Orders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(o => o.TenantId == filter.TenantId && o.DeletedAt == null);

            if (filter.BranchId.HasValue)
                query = query.Where(o => o.BranchId == filter.BranchId.Value);
            if (filter.DateFrom.HasValue)
                query = query.Where(o => o.CreatedAt >= filter.DateFrom.Value);
            if (filter.DateToExclusive.HasValue)
                query = query.Where(o => o.CreatedAt < filter.DateToExclusive.Value);

            return query.Select(order => new OrderRow
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                CustomerType = order.OrderSource == OrderSource.DeliveryPartner ||
                    order.OrderSource == OrderSource.Talabat ||
                    order.DeliveryPartnerId.HasValue
                    ? CustomerManagementType.Partner
                    : CustomerManagementType.Direct,
                DisplayName = order.OrderSource == OrderSource.DeliveryPartner ||
                    order.OrderSource == OrderSource.Talabat ||
                    order.DeliveryPartnerId.HasValue
                    ? order.PartnerCustomerName ?? order.TalabatCustomerName ?? PartnerCustomerName
                    : DirectCustomerName,
                DisplayNameAr = order.OrderSource == OrderSource.DeliveryPartner ||
                    order.OrderSource == OrderSource.Talabat ||
                    order.DeliveryPartnerId.HasValue
                    ? order.PartnerCustomerName ?? order.TalabatCustomerName ?? PartnerCustomerNameAr
                    : DirectCustomerNameAr,
                PhoneNumber = ((order.OrderSource == OrderSource.DeliveryPartner ||
                    order.OrderSource == OrderSource.Talabat ||
                    order.DeliveryPartnerId.HasValue
                    ? order.PartnerCustomerPhone ?? order.TalabatCustomerPhone ?? order.CustomerPhone
                    : order.CustomerPhone) ?? string.Empty).Trim(),
                PartnerName = order.DeliveryPartnerName ??
                    (order.OrderSource == OrderSource.Talabat ? TalabatPartnerName : null),
                PartnerNameAr = order.DeliveryPartnerNameAr ?? order.DeliveryPartnerName ??
                    (order.OrderSource == OrderSource.Talabat ? TalabatPartnerName : null),
                OrderNumber = order.DisplayOrderNumber ?? order.PublicOrderNumber ?? order.OrderNumber,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                CreatedAt = order.CreatedAt
            });
        }

        private static IEnumerable<CustomerAnalyticsCustomerDto> BuildCustomerRows(
            List<CustomerRow> customers,
            List<OrderRow> orders)
        {
            var ordersByCustomer = orders.Where(o => o.CustomerId.HasValue)
                .ToLookup(o => o.CustomerId!.Value);
            var registeredPhones = customers
                .Select(c => NormalizePhone(c.PhoneNumber))
                .Where(phone => phone.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var customer in customers)
                yield return BuildRegisteredCustomer(customer, ordersByCustomer[customer.Id]);

            var orderOnlyGroups = orders
                .Where(o => !o.CustomerId.HasValue && o.PhoneNumber.Length > 0)
                .Where(o => o.CustomerType == CustomerManagementType.Partner ||
                    !registeredPhones.Contains(NormalizePhone(o.PhoneNumber)))
                .GroupBy(o => new { Phone = NormalizePhone(o.PhoneNumber), o.CustomerType });

            foreach (var group in orderOnlyGroups)
                yield return BuildOrderCustomer(group);
        }

        private static CustomerAnalyticsCustomerDto BuildRegisteredCustomer(
            CustomerRow customer,
            IEnumerable<OrderRow> orders)
        {
            var orderList = orders.ToList();
            var partnerOrder = orderList
                .Where(o => o.CustomerType == CustomerManagementType.Partner)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();

            return new CustomerAnalyticsCustomerDto
            {
                Id = customer.Id,
                DisplayName = customer.DisplayName,
                PhoneNumber = customer.PhoneNumber,
                CustomerType = partnerOrder is null ? CustomerManagementType.Direct : CustomerManagementType.Partner,
                PartnerName = partnerOrder?.PartnerName,
                PartnerNameAr = partnerOrder?.PartnerNameAr,
                TotalOrders = orderList.Count,
                TotalSpending = RevenueTotal(orderList),
                AverageOrderValue = AverageOrderValue(orderList),
                FirstOrderDate = orderList.Count == 0 ? null : orderList.Min(o => (DateTime?)o.CreatedAt),
                LastOrderDate = orderList.Count == 0 ? null : orderList.Max(o => (DateTime?)o.CreatedAt),
                CreatedAt = customer.CreatedAt,
                IsActive = customer.IsActive
            };
        }

        private static CustomerAnalyticsCustomerDto BuildOrderCustomer(IEnumerable<OrderRow> orders)
        {
            var orderList = orders.OrderByDescending(o => o.CreatedAt).ToList();
            var latest = orderList[0];
            return new CustomerAnalyticsCustomerDto
            {
                Id = latest.Id,
                DisplayName = latest.DisplayName,
                PhoneNumber = latest.PhoneNumber,
                CustomerType = latest.CustomerType,
                PartnerName = latest.PartnerName,
                PartnerNameAr = latest.PartnerNameAr,
                TotalOrders = orderList.Count,
                TotalSpending = RevenueTotal(orderList),
                AverageOrderValue = AverageOrderValue(orderList),
                FirstOrderDate = orderList.Min(o => (DateTime?)o.CreatedAt),
                LastOrderDate = orderList.Max(o => (DateTime?)o.CreatedAt),
                CreatedAt = orderList.Min(o => (DateTime?)o.CreatedAt),
                IsActive = true
            };
        }

        private static IEnumerable<CustomerAnalyticsCustomerDto> ApplyCustomerFilters(
            IEnumerable<CustomerAnalyticsCustomerDto> customers,
            CustomerAnalyticsFilter filter)
        {
            var query = customers;
            if (filter.DateFrom.HasValue || filter.DateToExclusive.HasValue)
                query = query.Where(c => c.TotalOrders > 0 || IsWithinPeriod(c.CreatedAt, filter));
            if (!string.IsNullOrWhiteSpace(filter.Name))
                query = query.Where(c => Contains(c.DisplayName, filter.Name));
            if (!string.IsNullOrWhiteSpace(filter.Phone))
                query = query.Where(c => Contains(c.PhoneNumber, filter.Phone));
            if (!string.IsNullOrWhiteSpace(filter.Partner))
                query = query.Where(c => Contains(c.PartnerName, filter.Partner) || Contains(c.PartnerNameAr, filter.Partner));
            if (filter.CustomerType.HasValue)
                query = query.Where(c => c.CustomerType == filter.CustomerType.Value);
            if (filter.IsActive.HasValue)
                query = query.Where(c => c.IsActive == filter.IsActive.Value);
            if (filter.MinOrders.HasValue)
                query = query.Where(c => c.TotalOrders >= filter.MinOrders.Value);
            if (filter.MaxOrders.HasValue)
                query = query.Where(c => c.TotalOrders <= filter.MaxOrders.Value);
            if (filter.MinSpending.HasValue)
                query = query.Where(c => c.TotalSpending >= filter.MinSpending.Value);
            if (filter.MaxSpending.HasValue)
                query = query.Where(c => c.TotalSpending <= filter.MaxSpending.Value);
            return query;
        }

        private static CustomerAnalyticsSnapshotDto BuildSnapshot(
            List<CustomerAnalyticsCustomerDto> customers,
            List<OrderRow> orders,
            CustomerAnalyticsFilter filter)
        {
            var topCustomers = TopCustomers(customers);
            var recentCustomers = RecentCustomers(customers, filter);
            return new CustomerAnalyticsSnapshotDto
            {
                Summary = BuildSummary(customers, filter),
                Insights = BuildInsights(customers, orders),
                CustomerGrowth = BuildCustomerGrowth(customers, filter),
                OrdersTrend = BuildOrderSeries(orders, _ => 1m),
                RevenueTrend = BuildOrderSeries(orders.Where(IsRevenueOrder), o => o.TotalAmount),
                CustomerActivity = BuildOrderSeries(orders, _ => 1m),
                CustomerDistribution = BuildCustomerDistribution(customers),
                CustomerStatus = BuildCustomerStatus(customers),
                PartnerDistribution = BuildPartnerDistribution(customers, orders),
                CustomerRetention = BuildRetentionSeries(orders),
                TopCustomers = topCustomers,
                RecentCustomers = recentCustomers,
                RecentActivity = BuildRecentActivity(customers, orders),
                Segments = BuildSegments(customers, recentCustomers),
                PartnerOptions = BuildPartnerOptions(customers),
                BranchesEnabled = true,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }

        private static CustomerAnalyticsSummaryDto BuildSummary(
            List<CustomerAnalyticsCustomerDto> customers,
            CustomerAnalyticsFilter filter)
        {
            var totalCustomers = customers.Count;
            var totalOrders = customers.Sum(c => c.TotalOrders);
            var totalRevenue = customers.Sum(c => c.TotalSpending);
            var returning = customers.Count(c => c.TotalOrders >= ReturningOrderThreshold);

            return new CustomerAnalyticsSummaryDto
            {
                TotalCustomers = totalCustomers,
                ActiveCustomers = customers.Count(IsActiveCustomer),
                NewCustomers = customers.Count(c => IsWithinPeriod(c.CreatedAt, filter)),
                ReturningCustomers = returning,
                InactiveCustomers = customers.Count(c => c.TotalOrders == 0 || !c.IsActive),
                LostCustomers = customers.Count(IsLostCustomer),
                DirectCustomers = customers.Count(c => c.CustomerType == CustomerManagementType.Direct),
                PartnerCustomers = customers.Count(c => c.CustomerType == CustomerManagementType.Partner),
                TotalOrders = totalOrders,
                TotalRevenue = Round(totalRevenue),
                AverageOrderValue = totalOrders == 0 ? 0 : Round(totalRevenue / totalOrders),
                AverageCustomerSpend = totalCustomers == 0 ? 0 : Round(totalRevenue / totalCustomers),
                AverageOrdersPerCustomer = totalCustomers == 0 ? 0 : Round((decimal)totalOrders / totalCustomers),
                CustomerGrowthPercent = BuildGrowthPercent(customers, filter),
                RetentionRatePercent = Percent(returning, totalCustomers)
            };
        }

        private static CustomerAnalyticsInsightsDto BuildInsights(
            List<CustomerAnalyticsCustomerDto> customers,
            List<OrderRow> orders)
            => new()
            {
                HighestSpendingCustomer = customers.OrderByDescending(c => c.TotalSpending).FirstOrDefault(),
                MostFrequentCustomer = customers.OrderByDescending(c => c.TotalOrders).FirstOrDefault(),
                MostActivePartner = BuildPartnerOptions(customers).FirstOrDefault(),
                MostActiveDay = MostActiveDatePart(orders, o => o.CreatedAt.DayOfWeek.ToString()),
                MostActiveMonth = MostActiveDatePart(orders, o => o.CreatedAt.ToString("MMMM")),
                AverageCustomerLifetimeDays = AverageCustomerLifetime(customers),
                AverageDaysBetweenOrders = AverageDaysBetweenOrders(customers)
            };

        private static List<CustomerAnalyticsPointDto> BuildCustomerGrowth(
            List<CustomerAnalyticsCustomerDto> customers,
            CustomerAnalyticsFilter filter)
            => BuildDateSeries(
                customers.Where(c => IsWithinPeriod(c.CreatedAt, filter)),
                c => c.CreatedAt,
                _ => 1m);

        private static List<CustomerAnalyticsBreakdownDto> BuildCustomerDistribution(
            List<CustomerAnalyticsCustomerDto> customers)
        {
            var total = customers.Count;
            return new()
            {
                Breakdown(DirectLabel, DirectLabelAr, customers.Count(c => c.CustomerType == CustomerManagementType.Direct), total),
                Breakdown(PartnerLabel, PartnerLabelAr, customers.Count(c => c.CustomerType == CustomerManagementType.Partner), total)
            };
        }

        private static List<CustomerAnalyticsBreakdownDto> BuildCustomerStatus(
            List<CustomerAnalyticsCustomerDto> customers)
        {
            var total = customers.Count;
            var lost = customers.Count(IsLostCustomer);
            return new()
            {
                Breakdown("Active", "نشط", customers.Count(IsActiveCustomer), total),
                Breakdown("Inactive", "غير نشط", customers.Count(c => c.TotalOrders == 0 || !c.IsActive), total),
                Breakdown("Lost", "مفقود", lost, total)
            };
        }

        private static List<CustomerAnalyticsPartnerBreakdownDto> BuildPartnerDistribution(
            List<CustomerAnalyticsCustomerDto> customers,
            List<OrderRow> orders)
            => customers
                .Where(c => c.CustomerType == CustomerManagementType.Partner && !string.IsNullOrWhiteSpace(c.PartnerName))
                .GroupBy(c => c.PartnerName!)
                .OrderByDescending(g => g.Sum(c => c.TotalOrders))
                .Select(g => new CustomerAnalyticsPartnerBreakdownDto
                {
                    Name = g.Key,
                    NameAr = g.Select(c => c.PartnerNameAr).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)),
                    Customers = g.Count(),
                    Orders = orders.Count(o => o.PartnerName == g.Key),
                    Revenue = Round(orders.Where(o => o.PartnerName == g.Key && IsRevenueOrder(o)).Sum(o => o.TotalAmount))
                })
                .ToList();

        private static List<CustomerAnalyticsPointDto> BuildRetentionSeries(List<OrderRow> orders)
            => orders
                .GroupBy(o => o.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var customerOrderCounts = g.GroupBy(CustomerKey).Select(cg => cg.Count()).ToList();
                    var returning = customerOrderCounts.Count(count => count >= ReturningOrderThreshold);
                    return new CustomerAnalyticsPointDto
                    {
                        Date = g.Key,
                        Value = Percent(returning, customerOrderCounts.Count)
                    };
                })
                .ToList();

        private static List<CustomerAnalyticsActivityDto> BuildRecentActivity(
            List<CustomerAnalyticsCustomerDto> customers,
            List<OrderRow> orders)
        {
            var registrations = customers
                .Where(c => c.CreatedAt.HasValue)
                .OrderByDescending(c => c.CreatedAt)
                .Take(DashboardListLimit)
                .Select(c => Activity(c, "Registered", "تسجيل عميل", "Customer registered", "تم تسجيل العميل", null, c.CreatedAt!.Value));
            var orderActivities = orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(DashboardListLimit)
                .Select(o => ActivityForOrder(customers, o));
            return registrations.Concat(orderActivities)
                .OrderByDescending(a => a.OccurredAt)
                .Take(DashboardListLimit)
                .ToList();
        }

        private static List<CustomerAnalyticsSegmentDto> BuildSegments(
            List<CustomerAnalyticsCustomerDto> customers,
            List<CustomerAnalyticsCustomerDto> recentCustomers)
        {
            var total = customers.Count;
            return new()
            {
                Segment("vip", "VIP Customers", "كبار العملاء", total, customers.OrderByDescending(c => c.TotalSpending)),
                Segment("frequent", "Frequent Customers", "العملاء المتكررون", total, customers.Where(c => c.TotalOrders >= FrequentCustomerThreshold).OrderByDescending(c => c.TotalOrders)),
                Segment("returning", "Returning Customers", "عملاء عائدون", total, customers.Where(c => c.TotalOrders >= ReturningOrderThreshold).OrderByDescending(c => c.TotalOrders)),
                Segment("new", "New Customers", "عملاء جدد", total, recentCustomers),
                Segment("inactive", "Inactive Customers", "عملاء غير نشطين", total, customers.Where(c => c.TotalOrders == 0 || !c.IsActive).OrderByDescending(c => c.CreatedAt))
            };
        }

        private static List<CustomerAnalyticsPartnerOptionDto> BuildPartnerOptions(
            List<CustomerAnalyticsCustomerDto> customers)
            => customers
                .Where(c => !string.IsNullOrWhiteSpace(c.PartnerName))
                .GroupBy(c => c.PartnerName!)
                .OrderByDescending(g => g.Sum(c => c.TotalOrders))
                .Select(g => new CustomerAnalyticsPartnerOptionDto
                {
                    Name = g.Key,
                    NameAr = g.Select(c => c.PartnerNameAr).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
                })
                .ToList();

        private static CustomerAnalyticsSegmentDto Segment(
            string key,
            string label,
            string labelAr,
            int total,
            IEnumerable<CustomerAnalyticsCustomerDto> customers)
        {
            var rows = customers.ToList();
            return new CustomerAnalyticsSegmentDto
            {
                Key = key,
                Label = label,
                LabelAr = labelAr,
                Count = rows.Count,
                Percent = Percent(rows.Count, total),
                Customers = rows.Take(DashboardListLimit).ToList()
            };
        }

        private static List<CustomerAnalyticsPointDto> BuildOrderSeries(
            IEnumerable<OrderRow> orders,
            Func<OrderRow, decimal> valueSelector)
            => orders
                .GroupBy(o => o.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new CustomerAnalyticsPointDto
                {
                    Date = g.Key,
                    Value = Round(g.Sum(valueSelector))
                })
                .ToList();

        private static List<CustomerAnalyticsPointDto> BuildDateSeries(
            IEnumerable<CustomerAnalyticsCustomerDto> customers,
            Func<CustomerAnalyticsCustomerDto, DateTime?> dateSelector,
            Func<CustomerAnalyticsCustomerDto, decimal> valueSelector)
            => customers
                .Select(c => new { Date = dateSelector(c), Value = valueSelector(c) })
                .Where(row => row.Date.HasValue)
                .GroupBy(row => row.Date!.Value.Date)
                .OrderBy(g => g.Key)
                .Select(g => new CustomerAnalyticsPointDto { Date = g.Key, Value = Round(g.Sum(row => row.Value)) })
                .ToList();

        private static CustomerAnalyticsActivityDto ActivityForOrder(
            List<CustomerAnalyticsCustomerDto> customers,
            OrderRow order)
        {
            var customer = customers.FirstOrDefault(c => c.Id == (order.CustomerId ?? order.Id)) ??
                customers.FirstOrDefault(c => NormalizePhone(c.PhoneNumber) == NormalizePhone(order.PhoneNumber));
            var name = customer?.DisplayName ?? order.DisplayName;
            return Activity(customer, "Order", "طلب", "Placed order", "قام بطلب", order.OrderNumber, order.CreatedAt, name, order.PhoneNumber, order.Id);
        }

        private static CustomerAnalyticsActivityDto Activity(
            CustomerAnalyticsCustomerDto? customer,
            string type,
            string typeAr,
            string description,
            string descriptionAr,
            string? orderNumber,
            DateTime occurredAt,
            string? fallbackName = null,
            string? fallbackPhone = null,
            Guid? fallbackId = null)
            => new()
            {
                CustomerId = customer?.Id ?? fallbackId ?? Guid.Empty,
                CustomerName = customer?.DisplayName ?? fallbackName ?? string.Empty,
                PhoneNumber = customer?.PhoneNumber ?? fallbackPhone ?? string.Empty,
                ActivityType = type,
                ActivityTypeAr = typeAr,
                Description = description,
                DescriptionAr = descriptionAr,
                OrderNumber = orderNumber,
                OccurredAt = occurredAt
            };

        private static List<CustomerAnalyticsCustomerDto> TopCustomers(List<CustomerAnalyticsCustomerDto> customers)
            => customers.OrderByDescending(c => c.TotalSpending)
                .ThenByDescending(c => c.TotalOrders)
                .Take(DashboardListLimit)
                .ToList();

        private static List<CustomerAnalyticsCustomerDto> RecentCustomers(
            List<CustomerAnalyticsCustomerDto> customers,
            CustomerAnalyticsFilter filter)
            => customers
                .Where(c => c.CreatedAt.HasValue && IsWithinPeriod(c.CreatedAt, filter))
                .OrderByDescending(c => c.CreatedAt)
                .Take(DashboardListLimit)
                .ToList();

        private static decimal BuildGrowthPercent(
            List<CustomerAnalyticsCustomerDto> customers,
            CustomerAnalyticsFilter filter)
        {
            if (!filter.DateFrom.HasValue || !filter.DateToExclusive.HasValue) return 0;
            var days = (filter.DateToExclusive.Value.Date - filter.DateFrom.Value.Date).Days;
            var previousFrom = filter.DateFrom.Value.AddDays(-days);
            var previousTo = filter.DateFrom.Value;
            var current = customers.Count(c => IsWithinPeriod(c.CreatedAt, filter));
            var previous = customers.Count(c => c.CreatedAt >= previousFrom && c.CreatedAt < previousTo);
            return previous == 0 ? (current == 0 ? 0 : 100) : Round((decimal)(current - previous) * 100m / previous);
        }

        private static string? MostActiveDatePart(List<OrderRow> orders, Func<OrderRow, string> selector)
            => orders.GroupBy(selector)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

        private static decimal AverageCustomerLifetime(List<CustomerAnalyticsCustomerDto> customers)
        {
            var lifetimes = customers
                .Where(c => c.FirstOrderDate.HasValue && c.LastOrderDate.HasValue)
                .Select(c => (decimal)(c.LastOrderDate!.Value.Date - c.FirstOrderDate!.Value.Date).TotalDays)
                .ToList();
            return lifetimes.Count == 0 ? 0 : Round(lifetimes.Average());
        }

        private static decimal AverageDaysBetweenOrders(List<CustomerAnalyticsCustomerDto> customers)
        {
            var values = customers
                .Where(c => c.FirstOrderDate.HasValue && c.LastOrderDate.HasValue && c.TotalOrders > 1)
                .Select(c => (decimal)(c.LastOrderDate!.Value.Date - c.FirstOrderDate!.Value.Date).TotalDays / (c.TotalOrders - 1))
                .ToList();
            return values.Count == 0 ? 0 : Round(values.Average());
        }

        private static CustomerAnalyticsBreakdownDto Breakdown(
            string label,
            string labelAr,
            int value,
            int total)
            => new()
            {
                Label = label,
                LabelAr = labelAr,
                Value = value,
                Percent = Percent(value, total)
            };

        private static bool IsWithinPeriod(DateTime? value, CustomerAnalyticsFilter filter)
        {
            if (!value.HasValue) return false;
            return (!filter.DateFrom.HasValue || value.Value >= filter.DateFrom.Value) &&
                (!filter.DateToExclusive.HasValue || value.Value < filter.DateToExclusive.Value);
        }

        private static bool IsRevenueOrder(OrderRow order)
            => order.Status == OrderStatus.Paid || order.Status == OrderStatus.Completed;

        private static bool IsActiveCustomer(CustomerAnalyticsCustomerDto customer)
            => customer.IsActive && customer.TotalOrders > 0 && !IsLostCustomer(customer);

        private static bool IsLostCustomer(CustomerAnalyticsCustomerDto customer)
            => !customer.LastOrderDate.HasValue ||
                customer.LastOrderDate.Value < DateTime.UtcNow.AddDays(-LostCustomerDays);

        private static string CustomerKey(OrderRow order)
            => order.CustomerId?.ToString() ?? $"{order.CustomerType}:{NormalizePhone(order.PhoneNumber)}";

        private static string NormalizePhone(string? phone)
            => (phone ?? string.Empty).Trim();

        private static bool Contains(string? value, string filter)
            => !string.IsNullOrWhiteSpace(value) &&
                value.Contains(filter, StringComparison.OrdinalIgnoreCase);

        private static decimal RevenueTotal(IEnumerable<OrderRow> orders)
            => Round(orders.Where(IsRevenueOrder).Sum(o => o.TotalAmount));

        private static decimal AverageOrderValue(List<OrderRow> orders)
        {
            var revenueOrders = orders.Where(IsRevenueOrder).ToList();
            return revenueOrders.Count == 0 ? 0 : Round(revenueOrders.Sum(o => o.TotalAmount) / revenueOrders.Count);
        }

        private static decimal Percent(int value, int total)
            => total == 0 ? 0 : Round((decimal)value * 100m / total);

        private static decimal Round(decimal value)
            => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private sealed class CustomerRow
        {
            public Guid Id { get; init; }
            public string DisplayName { get; init; } = string.Empty;
            public string PhoneNumber { get; init; } = string.Empty;
            public DateTime CreatedAt { get; init; }
            public bool IsActive { get; init; }
        }

        private sealed class OrderRow
        {
            public Guid Id { get; init; }
            public Guid? CustomerId { get; init; }
            public CustomerManagementType CustomerType { get; init; }
            public string DisplayName { get; init; } = string.Empty;
            public string DisplayNameAr { get; init; } = string.Empty;
            public string PhoneNumber { get; init; } = string.Empty;
            public string? PartnerName { get; init; }
            public string? PartnerNameAr { get; init; }
            public string OrderNumber { get; init; } = string.Empty;
            public decimal TotalAmount { get; init; }
            public OrderStatus Status { get; init; }
            public DateTime CreatedAt { get; init; }
        }
    }
}
