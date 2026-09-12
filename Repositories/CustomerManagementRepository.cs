using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed class CustomerManagementRepository : ICustomerManagementRepository
    {
        private const string TalabatPartnerName = "Talabat";
        private const string DirectCustomerName = "POS Customer";
        private const string DirectCustomerNameAr = "عميل نقطة البيع";
        private const string PartnerCustomerName = "Partner Customer";
        private const string PartnerCustomerNameAr = "عميل شريك";
        private readonly PosDbContext _context;

        public CustomerManagementRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<PaginatedResponse<CustomerManagementListItemDto>> GetPagedAsync(
            CustomerManagementFilter filter,
            bool isArabic,
            CancellationToken ct)
        {
            var orders = ActiveOrders(filter.TenantId, filter.BranchId);
            var customers = AllCustomers(filter.TenantId);
            var registeredCustomers = ProjectCustomers(
                ApplyCustomerDateFilter(customers, orders, filter),
                orders,
                isArabic);
            var orderCustomers = ProjectOrderCustomers(customers, orders, filter, isArabic);
            var projected = ApplyListFilters(
                registeredCustomers.Concat(orderCustomers),
                filter);
            var totalCount = await projected.CountAsync(ct);
            var items = await ApplyCustomerSort(projected, filter)
                .ThenBy(customer => customer.Id)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(ct);

            return new PaginatedResponse<CustomerManagementListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<CustomerManagementDetailsDto?> GetByIdAsync(
            Guid tenantId,
            Guid customerId,
            Guid? branchId,
            bool isArabic,
            CancellationToken ct)
        {
            var customer = await ProjectCustomerDetails(tenantId, customerId, isArabic)
                .FirstOrDefaultAsync(ct);
            var orders = ActiveOrders(tenantId, branchId);
            if (customer is null)
                return await GetOrderCustomerDetailsAsync(orders, customerId, isArabic, ct);

            var customerOrders = orders.Where(o => o.CustomerId == customerId);
            customer.OrderStatistics = await GetOrderStatisticsAsync(customerOrders, ct);
            customer.Partner = await GetLatestPartnerAsync(customerOrders, ct);
            customer.CustomerType = customer.Partner is null
                ? CustomerManagementType.Direct
                : CustomerManagementType.Partner;
            return customer;
        }

        public async Task<PaginatedResponse<CustomerManagementOrderDto>?> GetOrdersAsync(
            CustomerOrdersFilter filter,
            bool isArabic,
            CancellationToken ct)
        {
            var orders = await ResolveCustomerOrdersAsync(
                filter.TenantId,
                filter.CustomerId,
                filter.BranchId,
                ct);
            if (orders is null)
                return null;

            var filteredOrders = ApplyOrderFilters(orders, filter);
            var totalCount = await filteredOrders.CountAsync(ct);
            var sortedOrders = ApplyOrderSort(filteredOrders, filter)
                .ThenBy(order => order.Id);
            var items = await ProjectOrders(sortedOrders, isArabic)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(ct);

            return new PaginatedResponse<CustomerManagementOrderDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        private IQueryable<Customer> AllCustomers(Guid tenantId)
            => _context.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId);

        private IQueryable<Order> ActiveOrders(Guid tenantId, Guid? branchId)
        {
            var orders = _context.Orders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(o => o.TenantId == tenantId && o.DeletedAt == null);

            // Branch scope applies to the order stream only — customer master data stays
            // tenant-global; every stat/list derived from this queryable becomes branch stats.
            return branchId.HasValue
                ? orders.Where(o => o.BranchId == branchId.Value)
                : orders;
        }

        private static IQueryable<CustomerManagementListItemDto> ApplyListFilters(
            IQueryable<CustomerManagementListItemDto> query,
            CustomerManagementFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.Name))
            {
                var pattern = ContainsPattern(filter.Name);
                query = query.Where(c => EF.Functions.ILike(c.DisplayName, pattern) ||
                    EF.Functions.ILike(c.Name, pattern) ||
                    (c.NameAr != null && EF.Functions.ILike(c.NameAr, pattern)));
            }

            if (!string.IsNullOrWhiteSpace(filter.Phone))
                query = query.Where(c => EF.Functions.ILike(c.PhoneNumber, ContainsPattern(filter.Phone)));

            if (!string.IsNullOrWhiteSpace(filter.Partner))
            {
                var pattern = ContainsPattern(filter.Partner);
                query = query.Where(c =>
                    (c.PartnerName != null && EF.Functions.ILike(c.PartnerName, pattern)) ||
                    (c.PartnerNameAr != null && EF.Functions.ILike(c.PartnerNameAr, pattern)));
            }

            if (filter.CustomerType.HasValue)
                query = query.Where(c => c.CustomerType == filter.CustomerType.Value);
            if (filter.IsActive.HasValue)
                query = query.Where(c => c.IsActive == filter.IsActive.Value);
            return query;
        }

        private static IQueryable<Customer> ApplyCustomerDateFilter(
            IQueryable<Customer> query,
            IQueryable<Order> orders,
            CustomerManagementFilter filter)
        {
            if (!filter.DateFrom.HasValue && !filter.DateToExclusive.HasValue) return query;

            return query.Where(c => orders.Any(o =>
                o.CustomerId == c.Id &&
                (!filter.DateFrom.HasValue || o.CreatedAt >= filter.DateFrom.Value) &&
                (!filter.DateToExclusive.HasValue || o.CreatedAt < filter.DateToExclusive.Value)));
        }

        private static IQueryable<CustomerManagementListItemDto> ProjectCustomers(
            IQueryable<Customer> customers,
            IQueryable<Order> orders,
            bool isArabic)
            => customers.Select(c => new CustomerManagementListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                NameAr = c.NameAr,
                DisplayName = isArabic && c.NameAr != null && c.NameAr != string.Empty ? c.NameAr : c.Name,
                PhoneNumber = c.PhoneNumber,
                Email = c.Email,
                CustomerType = orders.Any(o =>
                    o.CustomerId == c.Id &&
                    (o.OrderSource == OrderSource.DeliveryPartner ||
                     o.OrderSource == OrderSource.Talabat ||
                     o.DeliveryPartnerId.HasValue))
                    ? CustomerManagementType.Partner
                    : CustomerManagementType.Direct,
                PartnerName = orders
                    .Where(o =>
                        o.CustomerId == c.Id &&
                        (o.OrderSource == OrderSource.DeliveryPartner ||
                         o.OrderSource == OrderSource.Talabat ||
                         o.DeliveryPartnerId.HasValue))
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => o.DeliveryPartnerName ??
                        (o.OrderSource == OrderSource.Talabat ? TalabatPartnerName : null))
                    .FirstOrDefault(),
                PartnerNameAr = orders
                    .Where(o =>
                        o.CustomerId == c.Id &&
                        (o.OrderSource == OrderSource.DeliveryPartner ||
                         o.OrderSource == OrderSource.Talabat ||
                         o.DeliveryPartnerId.HasValue))
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => o.DeliveryPartnerNameAr ?? o.DeliveryPartnerName ??
                        (o.OrderSource == OrderSource.Talabat ? TalabatPartnerName : null))
                    .FirstOrDefault(),
                TotalOrders = orders.Count(o => o.CustomerId == c.Id),
                LastOrderDate = orders
                    .Where(o => o.CustomerId == c.Id)
                    .Select(o => (DateTime?)o.CreatedAt)
                    .Max(),
                IsActive = c.DeletedAt == null
            });

        private static IQueryable<CustomerManagementListItemDto> ProjectOrderCustomers(
            IQueryable<Customer> customers,
            IQueryable<Order> orders,
            CustomerManagementFilter filter,
            bool isArabic)
        {
            var customerPhones = customers.Select(c => c.PhoneNumber.Trim());
            var snapshots = ProjectOrderCustomerSnapshots(orders)
                .Where(snapshot =>
                    snapshot.CustomerType == CustomerManagementType.Partner ||
                    !customerPhones.Contains(snapshot.PhoneNumber));
            var groups = snapshots.GroupBy(snapshot => new
            {
                snapshot.PhoneNumber,
                snapshot.CustomerType
            });

            if (filter.DateFrom.HasValue || filter.DateToExclusive.HasValue)
            {
                groups = groups.Where(group => group.Any(row =>
                    (!filter.DateFrom.HasValue || row.CreatedAt >= filter.DateFrom.Value) &&
                    (!filter.DateToExclusive.HasValue || row.CreatedAt < filter.DateToExclusive.Value)));
            }

            return groups.Select(group => new CustomerManagementListItemDto
            {
                Id = group.OrderByDescending(row => row.CreatedAt)
                    .Select(row => row.Id)
                    .First(),
                Name = group.OrderByDescending(row => row.CreatedAt)
                    .Select(row => row.Name)
                    .First(),
                NameAr = group.OrderByDescending(row => row.CreatedAt)
                    .Select(row => row.NameAr)
                    .First(),
                DisplayName = isArabic
                    ? group.OrderByDescending(row => row.CreatedAt)
                        .Select(row => row.NameAr ?? row.Name)
                        .First()
                    : group.OrderByDescending(row => row.CreatedAt)
                        .Select(row => row.Name)
                        .First(),
                PhoneNumber = group.Key.PhoneNumber,
                Email = null,
                CustomerType = group.Key.CustomerType,
                PartnerName = group.OrderByDescending(row => row.CreatedAt)
                    .Select(row => row.PartnerName)
                    .First(),
                PartnerNameAr = group.OrderByDescending(row => row.CreatedAt)
                    .Select(row => row.PartnerNameAr)
                    .First(),
                TotalOrders = group.Count(),
                LastOrderDate = group.Max(row => (DateTime?)row.CreatedAt),
                IsActive = true
            });
        }

        private static IQueryable<OrderCustomerSnapshot> ProjectOrderCustomerSnapshots(
            IQueryable<Order> orders)
            => orders
                .Where(order => order.CustomerId == null)
                .Select(order => new OrderCustomerSnapshot
                {
                    Id = order.Id,
                    CustomerType = order.OrderSource == OrderSource.DeliveryPartner ||
                        order.OrderSource == OrderSource.Talabat ||
                        order.DeliveryPartnerId.HasValue
                        ? CustomerManagementType.Partner
                        : CustomerManagementType.Direct,
                    Name = order.OrderSource == OrderSource.DeliveryPartner ||
                        order.OrderSource == OrderSource.Talabat ||
                        order.DeliveryPartnerId.HasValue
                        ? order.PartnerCustomerName ?? order.TalabatCustomerName ?? PartnerCustomerName
                        : DirectCustomerName,
                    NameAr = order.OrderSource == OrderSource.DeliveryPartner ||
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
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt
                })
                .Where(snapshot => snapshot.PhoneNumber != string.Empty);

        private static IOrderedQueryable<CustomerManagementListItemDto> ApplyCustomerSort(
            IQueryable<CustomerManagementListItemDto> query,
            CustomerManagementFilter filter)
        {
            var descending = filter.SortDirection == CustomerSortDirection.Descending;
            return filter.SortBy switch
            {
                CustomerSortField.CustomerId => Order(query, c => c.Id, descending),
                CustomerSortField.Name => Order(query, c => c.DisplayName, descending),
                CustomerSortField.Phone => Order(query, c => c.PhoneNumber, descending),
                CustomerSortField.CustomerType => Order(query, c => c.CustomerType, descending),
                CustomerSortField.PartnerName => Order(query, c => c.PartnerName, descending),
                CustomerSortField.TotalOrders => Order(query, c => c.TotalOrders, descending),
                CustomerSortField.Status => Order(query, c => c.IsActive, descending),
                _ => Order(query, c => c.LastOrderDate, descending)
            };
        }

        private IQueryable<CustomerManagementDetailsDto> ProjectCustomerDetails(
            Guid tenantId,
            Guid customerId,
            bool isArabic)
            => AllCustomers(tenantId)
                .Where(c => c.Id == customerId)
                .Select(c => new CustomerManagementDetailsDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    NameAr = c.NameAr,
                    DisplayName = isArabic && c.NameAr != null && c.NameAr != string.Empty ? c.NameAr : c.Name,
                    PhoneNumber = c.PhoneNumber,
                    Email = c.Email,
                    BirthDate = c.BirthDate,
                    IsActive = c.DeletedAt == null,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                });

        private static async Task<CustomerManagementDetailsDto?> GetOrderCustomerDetailsAsync(
            IQueryable<Order> orders,
            Guid customerId,
            bool isArabic,
            CancellationToken ct)
        {
            var snapshots = ProjectOrderCustomerSnapshots(orders);
            var anchor = await snapshots.FirstOrDefaultAsync(row => row.Id == customerId, ct);
            if (anchor is null) return null;

            var matchingSnapshots = snapshots.Where(row =>
                row.PhoneNumber == anchor.PhoneNumber &&
                row.CustomerType == anchor.CustomerType);
            var details = await matchingSnapshots
                .GroupBy(_ => 1)
                .Select(group => new CustomerManagementDetailsDto
                {
                    Id = customerId,
                    Name = group.OrderByDescending(row => row.CreatedAt)
                        .Select(row => row.Name)
                        .First(),
                    NameAr = group.OrderByDescending(row => row.CreatedAt)
                        .Select(row => row.NameAr)
                        .First(),
                    DisplayName = isArabic
                        ? group.OrderByDescending(row => row.CreatedAt)
                            .Select(row => row.NameAr ?? row.Name)
                            .First()
                        : group.OrderByDescending(row => row.CreatedAt)
                            .Select(row => row.Name)
                            .First(),
                    PhoneNumber = anchor.PhoneNumber,
                    Email = null,
                    BirthDate = null,
                    CustomerType = anchor.CustomerType,
                    IsActive = true,
                    CreatedAt = group.Min(row => row.CreatedAt),
                    UpdatedAt = group.Max(row => row.UpdatedAt)
                })
                .FirstAsync(ct);

            var orderIds = matchingSnapshots.Select(row => row.Id);
            var customerOrders = orders.Where(order => orderIds.Contains(order.Id));
            details.OrderStatistics = await GetOrderStatisticsAsync(customerOrders, ct);
            details.Partner = await GetLatestPartnerAsync(customerOrders, ct);
            return details;
        }

        private static async Task<CustomerOrderStatisticsDto> GetOrderStatisticsAsync(
            IQueryable<Order> orders,
            CancellationToken ct)
        {
            var statistics = await orders
                .GroupBy(_ => 1)
                .Select(g => new CustomerOrderStatisticsDto
                {
                    TotalOrders = g.Count(),
                    CompletedOrders = g.Count(o =>
                        o.Status == OrderStatus.Paid || o.Status == OrderStatus.Completed),
                    CancelledOrders = g.Count(o => o.Status == OrderStatus.Cancelled),
                    TotalSpend = g
                        .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Completed)
                        .Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                    AverageOrderValue = g
                        .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Completed)
                        .Average(o => (decimal?)o.TotalAmount) ?? 0m,
                    FirstOrderDate = g.Min(o => (DateTime?)o.CreatedAt),
                    LastOrderDate = g.Max(o => (DateTime?)o.CreatedAt)
                })
                .FirstOrDefaultAsync(ct);

            return statistics ?? new CustomerOrderStatisticsDto();
        }

        private static Task<CustomerPartnerInfoDto?> GetLatestPartnerAsync(
            IQueryable<Order> orders,
            CancellationToken ct)
            => orders
                .Where(o =>
                    o.OrderSource == OrderSource.DeliveryPartner ||
                    o.OrderSource == OrderSource.Talabat ||
                    o.DeliveryPartnerId.HasValue)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new CustomerPartnerInfoDto
                {
                    PartnerId = o.DeliveryPartnerId,
                    Name = o.DeliveryPartnerName ??
                        (o.OrderSource == OrderSource.Talabat ? TalabatPartnerName : string.Empty),
                    NameAr = o.DeliveryPartnerNameAr,
                    Code = o.DeliveryPartnerCode,
                    LastPartnerOrderNumber = o.PartnerOrderNumber ?? o.TalabatOrderNumber,
                    LastPartnerOrderDate = o.CreatedAt
                })
                .FirstOrDefaultAsync(ct);

        private async Task<IQueryable<Order>?> ResolveCustomerOrdersAsync(
            Guid tenantId,
            Guid customerId,
            Guid? branchId,
            CancellationToken ct)
        {
            var orders = ActiveOrders(tenantId, branchId);
            if (await AllCustomers(tenantId).AnyAsync(c => c.Id == customerId, ct))
                return orders.Where(order => order.CustomerId == customerId);

            var snapshots = ProjectOrderCustomerSnapshots(orders);
            var anchor = await snapshots.FirstOrDefaultAsync(row => row.Id == customerId, ct);
            if (anchor is null) return null;

            var orderIds = snapshots
                .Where(row => row.PhoneNumber == anchor.PhoneNumber &&
                    row.CustomerType == anchor.CustomerType)
                .Select(row => row.Id);
            return orders.Where(order => orderIds.Contains(order.Id));
        }

        private static IQueryable<Order> ApplyOrderFilters(
            IQueryable<Order> query,
            CustomerOrdersFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var pattern = ContainsPattern(filter.Search);
                query = query.Where(o =>
                    EF.Functions.ILike(o.OrderNumber, pattern) ||
                    (o.DisplayOrderNumber != null && EF.Functions.ILike(o.DisplayOrderNumber, pattern)) ||
                    (o.PublicOrderNumber != null && EF.Functions.ILike(o.PublicOrderNumber, pattern)));
            }

            if (filter.Status.HasValue)
                query = query.Where(o => o.Status == filter.Status.Value);
            if (filter.DateFrom.HasValue)
                query = query.Where(o => o.CreatedAt >= filter.DateFrom.Value);
            if (filter.DateToExclusive.HasValue)
                query = query.Where(o => o.CreatedAt < filter.DateToExclusive.Value);
            return query;
        }

        private static IOrderedQueryable<Order> ApplyOrderSort(
            IQueryable<Order> query,
            CustomerOrdersFilter filter)
        {
            var descending = filter.SortDirection == CustomerSortDirection.Descending;
            return filter.SortBy switch
            {
                CustomerOrderSortField.OrderNumber => Order(query, o => o.DisplayOrderNumber ??
                    o.PublicOrderNumber ?? o.OrderNumber, descending),
                CustomerOrderSortField.TotalAmount => Order(query, o => o.TotalAmount, descending),
                CustomerOrderSortField.Status => Order(query, o => o.Status, descending),
                _ => Order(query, o => o.CreatedAt, descending)
            };
        }

        private static IQueryable<CustomerManagementOrderDto> ProjectOrders(
            IQueryable<Order> orders,
            bool isArabic)
            => orders.Select(o => new CustomerManagementOrderDto
            {
                Id = o.Id,
                OrderNumber = o.DisplayOrderNumber ?? o.PublicOrderNumber ?? o.OrderNumber,
                OrderType = o.OrderType,
                OrderSource = o.OrderSource,
                PartnerName = isArabic && o.DeliveryPartnerNameAr != null &&
                    o.DeliveryPartnerNameAr != string.Empty
                    ? o.DeliveryPartnerNameAr
                    : o.DeliveryPartnerName ??
                        (o.OrderSource == OrderSource.Talabat ? TalabatPartnerName : null),
                PartnerNameAr = o.DeliveryPartnerNameAr,
                ItemCount = o.OrderItems.Select(i => (int?)i.Quantity).Sum() ?? 0,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                OrderDate = o.CreatedAt
            });

        private static string ContainsPattern(string value)
            => $"%{value.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

        private static IOrderedQueryable<T> Order<T, TKey>(
            IQueryable<T> query,
            System.Linq.Expressions.Expression<Func<T, TKey>> keySelector,
            bool descending)
            => descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);

        private sealed class OrderCustomerSnapshot
        {
            public Guid Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public string? NameAr { get; init; }
            public string PhoneNumber { get; init; } = string.Empty;
            public CustomerManagementType CustomerType { get; init; }
            public string? PartnerName { get; init; }
            public string? PartnerNameAr { get; init; }
            public DateTime CreatedAt { get; init; }
            public DateTime UpdatedAt { get; init; }
        }
    }
}
