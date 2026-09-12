using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Hubs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerOrderService : ICustomerOrderService
    {
        private const int MaxPageSize = 100;
        private const int MaxQuantity = 99;
        private const string PickupTableName = "MOBILE PICKUP";
        private const string DeliveryTableName = "MOBILE DELIVERY";

        private readonly PosDbContext _context;
        private readonly ICustomerMobileContext _mobileContext;
        private readonly IHubContext<KitchenHub> _hubContext;
        private readonly IMediator _mediator;
        private readonly INotificationService _notificationService;
        private readonly IOrderDisplayNumberService _displayNumberService;

        public CustomerOrderService(
            PosDbContext context,
            ICustomerMobileContext mobileContext,
            IHubContext<KitchenHub> hubContext,
            IMediator mediator,
            INotificationService notificationService,
            IOrderDisplayNumberService displayNumberService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _displayNumberService = displayNumberService ?? throw new ArgumentNullException(nameof(displayNumberService));
        }

        public async Task<CustomerOrderCreateResponse> CreateOrderAsync(CustomerOrderCreateRequest request, CancellationToken ct)
        {
            var mobileOrderType = ParseOrderType(request.OrderType);
            ValidateCreateRequest(request, mobileOrderType);

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            var customerAccount = await LoadCustomerAccountAsync(ct);
            var crmCustomer = await ResolveCrmCustomerAsync(customerAccount, ct);
            var address = await ResolveAddressAsync(request.DeliveryAddressId, mobileOrderType, ct);
            var products = await LoadProductsAsync(request.Items, ct);
            var branchId = await GetMainBranchIdAsync(customerAccount.TenantId, ct);
            var orderItems = BuildOrderItems(request.Items, products, customerAccount.TenantId, branchId);
            var totals = CalculateTotals(orderItems);
            var order = await BuildOrderAsync(request, customerAccount, crmCustomer, address, orderItems, totals, branchId, ct);

            _context.Orders.Add(order);
            _context.CustomerMobileOrders.Add(new CustomerMobileOrder
            {
                Id = Guid.NewGuid(),
                TenantId = customerAccount.TenantId,
                CustomerId = customerAccount.Id,
                OrderId = order.Id,
                OrderNumber = order.DisplayOrderNumber ?? order.OrderNumber,
                OrderType = mobileOrderType
            });

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            await NotifyKitchenAsync(order, ct);

            // Customer-facing OrderNumber uses the configured display value so
            // the mobile app shows the same identifier the
            // receipt / kitchen ticket prints. Falls back for legacy callers.
            return new CustomerOrderCreateResponse
            {
                OrderId = order.Id,
                OrderNumber = order.DisplayOrderNumber ?? order.OrderNumber,
                Status = order.Status.ToString()
            };
        }

        public async Task<PaginatedResponse<CustomerOrderListItemDto>> GetOrdersAsync(int page, int pageSize, CancellationToken ct)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            var customerId = _mobileContext.GetCustomerId();
            var query = _context.CustomerMobileOrders
                .AsNoTracking()
                .Where(m => m.CustomerId == customerId);

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new CustomerOrderListItemDto
                {
                    Id = m.Id,
                    OrderId = m.OrderId,
                    OrderNumber = m.Order.DisplayOrderNumber ?? m.OrderNumber,
                    OrderType = m.OrderType.ToString(),
                    Status = m.Order.Status.ToString(),
                    TotalAmount = m.Order.TotalAmount,
                    TotalItemsCount = m.Order.OrderItems.Select(i => (int?)i.Quantity).Sum() ?? 0,
                    CreatedAt = m.Order.CreatedAt
                })
                .ToListAsync(ct);

            return new PaginatedResponse<CustomerOrderListItemDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<CustomerOrderDetailsDto> GetOrderAsync(Guid id, CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();

            var header = await LoadOrderDetailsHeaderAsync(customerId, id, ct);
            var itemRows = await LoadOrderItemRowsAsync(header, ct);
            var modifiers = await LoadOrderModifiersAsync(
                header.TenantId,
                itemRows.Select(i => i.Id).ToList(),
                ct);

            return MapOrderDetails(header, itemRows, modifiers);
        }

        private async Task<CustomerOrderHeader> LoadOrderDetailsHeaderAsync(
            Guid customerId,
            Guid orderId,
            CancellationToken ct)
        {
            return await _context.CustomerMobileOrders
                .AsNoTracking()
                .Where(m => m.CustomerId == customerId && m.OrderId == orderId)
                .Select(m => new CustomerOrderHeader
                {
                    TenantId = m.TenantId,
                    Id = m.Id,
                    OrderId = m.OrderId,
                    OrderNumber = m.Order.DisplayOrderNumber ?? m.OrderNumber,
                    OrderType = m.OrderType.ToString(),
                    Status = m.Order.Status.ToString(),
                    TotalAmount = m.Order.TotalAmount,
                    TotalItemsCount = m.Order.OrderItems.Select(i => (int?)i.Quantity).Sum() ?? 0,
                    CreatedAt = m.Order.CreatedAt,
                    DeliveryAddress = m.Order.DeliveryAddress,
                    DeliveryNotes = m.Order.DeliveryNotes,
                    Subtotal = m.Order.Subtotal,
                    DiscountAmount = m.Order.DiscountAmount,
                    TaxAmount = m.Order.TaxAmount
                })
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Customer order was not found.");
        }

        private Task<List<CustomerOrderItemRow>> LoadOrderItemRowsAsync(
            CustomerOrderHeader header,
            CancellationToken ct)
        {
            var products = _context.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.TenantId == header.TenantId);
            var categories = _context.Categories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.TenantId == header.TenantId);

            return (
                from item in _context.OrderItems.AsNoTracking()
                where item.TenantId == header.TenantId && item.OrderId == header.OrderId
                join product in products on item.ProductId equals product.Id into productJoin
                from product in productJoin.DefaultIfEmpty()
                join category in categories on product.CategoryId equals category.Id into categoryJoin
                from category in categoryJoin.DefaultIfEmpty()
                orderby item.CreatedAt
                select new CustomerOrderItemRow
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName != null && item.ProductName != string.Empty
                        ? item.ProductName
                        : product == null ? string.Empty : product.Name,
                    ProductNameAr = item.OfferLineNameAr ?? (product == null ? null : product.NameAr),
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPriceSnapshot ?? item.Price,
                    LineTotal = item.LineTotalSnapshot ?? (item.IsComplimentary
                        ? 0m
                        : ((item.UnitPriceSnapshot ?? item.Price) +
                           (item.Modifiers.Select(m => (decimal?)(m.Price * m.Quantity)).Sum() ?? 0m)) * item.Quantity),
                    Notes = item.Notes,
                    ImageUrl = product == null ? null : product.ImageUrl,
                    CategoryId = category == null ? null : category.Id,
                    CategoryName = category == null ? null : category.Name,
                    CategoryNameAr = category == null ? null : category.NameAr,
                    SelectedOptionId = item.SelectedOptionId,
                    SelectedOptionName = item.SelectedOptionName,
                    SelectedOptionNameAr = item.SelectedOptionNameAr
                })
                .ToListAsync(ct);
        }

        private async Task<Dictionary<Guid, List<CustomerOrderItemModifierDto>>> LoadOrderModifiersAsync(
            Guid tenantId,
            List<Guid> orderItemIds,
            CancellationToken ct)
        {
            if (orderItemIds.Count == 0)
                return new Dictionary<Guid, List<CustomerOrderItemModifierDto>>();

            var rows = await _context.OrderItemModifiers
                .AsNoTracking()
                .Where(m => m.TenantId == tenantId && orderItemIds.Contains(m.OrderItemId))
                .OrderBy(m => m.CreatedAt)
                .Select(m => new CustomerOrderModifierRow
                {
                    OrderItemId = m.OrderItemId,
                    Id = m.ModifierId ?? m.Id,
                    Name = m.ModifierName,
                    NameAr = m.ModifierNameAr,
                    Price = m.Price,
                    Quantity = m.Quantity
                })
                .ToListAsync(ct);

            return rows
                .GroupBy(m => m.OrderItemId)
                .ToDictionary(g => g.Key, g => g.Select(MapModifier).ToList());
        }

        private static CustomerOrderDetailsDto MapOrderDetails(
            CustomerOrderHeader header,
            List<CustomerOrderItemRow> itemRows,
            IReadOnlyDictionary<Guid, List<CustomerOrderItemModifierDto>> modifiers)
        {
            return new CustomerOrderDetailsDto
            {
                Id = header.Id,
                OrderId = header.OrderId,
                OrderNumber = header.OrderNumber,
                OrderType = header.OrderType,
                Status = header.Status,
                TotalAmount = header.TotalAmount,
                TotalItemsCount = header.TotalItemsCount,
                CreatedAt = header.CreatedAt,
                DeliveryAddress = header.DeliveryAddress,
                DeliveryNotes = header.DeliveryNotes,
                Items = itemRows.Select(i => MapOrderItem(i, header, modifiers)).ToList()
            };
        }

        private static CustomerOrderItemDto MapOrderItem(
            CustomerOrderItemRow row,
            CustomerOrderHeader header,
            IReadOnlyDictionary<Guid, List<CustomerOrderItemModifierDto>> modifiers)
        {
            var itemModifiers = modifiers.TryGetValue(row.Id, out var values)
                ? values
                : new List<CustomerOrderItemModifierDto>();

            return new CustomerOrderItemDto
            {
                Id = row.Id,
                ProductId = row.ProductId,
                ProductName = row.ProductName,
                ProductNameEn = row.ProductName,
                ProductNameAr = row.ProductNameAr,
                Quantity = row.Quantity,
                UnitPrice = row.UnitPrice,
                LineTotal = row.LineTotal,
                DiscountAmount = CalculateProportionalAmount(row.LineTotal, header.Subtotal, header.DiscountAmount),
                TaxAmount = CalculateProportionalAmount(row.LineTotal, header.Subtotal, header.TaxAmount),
                Notes = row.Notes,
                ImageUrl = row.ImageUrl,
                CategoryId = row.CategoryId,
                CategoryName = row.CategoryName,
                CategoryNameAr = row.CategoryNameAr,
                SelectedOptionId = row.SelectedOptionId,
                SelectedOptionName = row.SelectedOptionName,
                SelectedOptionNameAr = row.SelectedOptionNameAr,
                VariantId = row.SelectedOptionId,
                VariantName = row.SelectedOptionName,
                VariantNameAr = row.SelectedOptionNameAr,
                Modifiers = itemModifiers
            };
        }

        private static CustomerOrderItemModifierDto MapModifier(CustomerOrderModifierRow row)
            => new()
            {
                Id = row.Id,
                Name = row.Name,
                NameAr = row.NameAr,
                Price = row.Price,
                Quantity = row.Quantity
            };

        private static decimal CalculateProportionalAmount(decimal lineTotal, decimal subtotal, decimal amount)
            => amount <= 0m || subtotal <= 0m || lineTotal <= 0m
                ? 0m
                : OrderPaymentHelper.RoundCurrency(amount * lineTotal / subtotal);

        private sealed class CustomerOrderHeader
        {
            public Guid TenantId { get; init; }
            public Guid Id { get; init; }
            public Guid OrderId { get; init; }
            public string OrderNumber { get; init; } = string.Empty;
            public string OrderType { get; init; } = string.Empty;
            public string Status { get; init; } = string.Empty;
            public decimal TotalAmount { get; init; }
            public int TotalItemsCount { get; init; }
            public DateTime CreatedAt { get; init; }
            public string? DeliveryAddress { get; init; }
            public string? DeliveryNotes { get; init; }
            public decimal Subtotal { get; init; }
            public decimal DiscountAmount { get; init; }
            public decimal TaxAmount { get; init; }
        }

        private sealed class CustomerOrderItemRow
        {
            public Guid Id { get; init; }
            public Guid ProductId { get; init; }
            public string ProductName { get; init; } = string.Empty;
            public string? ProductNameAr { get; init; }
            public int Quantity { get; init; }
            public decimal UnitPrice { get; init; }
            public decimal LineTotal { get; init; }
            public string? Notes { get; init; }
            public string? ImageUrl { get; init; }
            public Guid? CategoryId { get; init; }
            public string? CategoryName { get; init; }
            public string? CategoryNameAr { get; init; }
            public Guid? SelectedOptionId { get; init; }
            public string? SelectedOptionName { get; init; }
            public string? SelectedOptionNameAr { get; init; }
        }

        private sealed class CustomerOrderModifierRow
        {
            public Guid OrderItemId { get; init; }
            public Guid? Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public string? NameAr { get; init; }
            public decimal Price { get; init; }
            public int Quantity { get; init; }
        }

        private async Task<Order> BuildOrderAsync(
            CustomerOrderCreateRequest request,
            CustomerAccount account,
            Customer crmCustomer,
            CustomerAddress? address,
            List<OrderItem> orderItems,
            OrderTotals totals,
            Guid branchId,
            CancellationToken ct)
        {
            var orderType = address == null ? OrderType.Takeaway : OrderType.Delivery;
            var orderId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;
            // QR / Customer Mobile orders are always classified as the "QR" channel
            // for display numbering regardless of pickup vs delivery — see spec
            // requirement 3 ("QR Orders" is its own counter).
            var displayOrderNumber = await _displayNumberService.GenerateAsync(
                account.TenantId,
                orderType,
                OrderSource.Mobile,
                hasDeliveryPartner: false,
                createdAt,
                ct);

            var order = new Order
            {
                Id = orderId,
                TenantId = account.TenantId,
                BranchId = branchId,
                OrderNumber = GenerateOrderNumber(orderType, orderId, createdAt),
                DisplayOrderNumber = displayOrderNumber,
                OrderType = orderType,
                OrderSource = OrderSource.Mobile,
                TableName = orderType == OrderType.Delivery ? DeliveryTableName : PickupTableName,
                Status = OrderStatus.New,
                CreatedAt = createdAt,
                SyncedAt = createdAt,
                CustomerId = crmCustomer.Id,
                CustomerPhone = account.MobileNumber,
                PaymentMethod = request.PaymentMethod.Trim(),
                Subtotal = totals.Subtotal,
                DiscountAmount = 0m,
                ServiceChargeAmount = 0m,
                TaxAmount = 0m,
                TotalAmount = totals.Subtotal,
                DeliveryAddress = address == null ? null : FormatAddress(address),
                DeliveryNotes = NormalizeNullable(request.Notes),
                OrderItems = orderItems
            };

            if (address?.DeliveryZoneId.HasValue == true)
                await ApplyDeliveryZoneAsync(order, address.DeliveryZoneId.Value, ct);

            order.TotalAmount += order.DeliveryFee ?? 0m;
            DeliveryAccountingHelper.Apply(order);
            return order;
        }

        private async Task ApplyDeliveryZoneAsync(Order order, Guid deliveryZoneId, CancellationToken ct)
        {
            // Mobile orders always land on the Main Branch, so only Main-Branch zones apply.
            var zone = await _context.DeliveryZones
                .AsNoTracking()
                .FirstOrDefaultAsync(z => z.Id == deliveryZoneId && z.IsActive && z.Branch!.IsMainBranch, ct)
                ?? throw new ValidationException("Delivery zone is invalid or inactive.");

            order.DeliveryZoneId = zone.Id;
            order.DeliveryZoneName = zone.Name;
            order.DeliveryFee = zone.DeliveryFee;
            order.DeliveryCost = zone.DeliveryCost;
            order.DeliveryPaymentMode = zone.PaymentMode;
        }

        private async Task NotifyKitchenAsync(Order order, CancellationToken ct)
        {
            var dto = MapOrderForKitchen(order);
            await _hubContext.Clients.Group(KitchenHubGroups.Branch(order.BranchId)).SendAsync(KitchenHubEvents.ReceiveNewOrder, dto, ct);
            await _mediator.Publish(new OrderCreatedForPrintingEvent(order.Id, order.TenantId, null), ct);
            await _notificationService.SendAsync(
                NotificationType.NewOrder,
                $"New Mobile Order #{order.DisplayOrderNumber ?? order.OrderNumber}",
                order.TableName,
                order.Id.ToString(),
                "Kitchen",
                order.TenantId,
                order.BranchId);
        }

        private async Task<Guid> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct)
        {
            var branchId = await _context.Branches
                .IgnoreQueryFilters()
                .Where(b => b.TenantId == tenantId && b.IsMainBranch && b.IsActive)
                .Select(b => b.Id)
                .FirstOrDefaultAsync(ct);
            return branchId == Guid.Empty
                ? throw new ValidationException("Main branch is not configured.")
                : branchId;
        }

        private async Task<CustomerAccount> LoadCustomerAccountAsync(CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            return await _context.CustomerAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId && c.IsActive && !c.IsDeleted, ct)
                ?? throw new UnauthorizedException("Customer account is inactive.");
        }

        private async Task<Customer> ResolveCrmCustomerAsync(CustomerAccount account, CancellationToken ct)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.PhoneNumber == account.MobileNumber, ct);

            if (customer != null)
                return customer;

            customer = new Customer
            {
                Id = Guid.NewGuid(),
                TenantId = account.TenantId,
                Name = $"{account.FirstName} {account.LastName}".Trim(),
                PhoneNumber = account.MobileNumber,
                Email = account.Email,
                LastVisit = DateTime.UtcNow
            };
            _context.Customers.Add(customer);
            return customer;
        }

        private async Task<CustomerAddress?> ResolveAddressAsync(
            Guid? addressId,
            CustomerMobileOrderType orderType,
            CancellationToken ct)
        {
            if (orderType == CustomerMobileOrderType.Pickup)
                return null;

            if (!addressId.HasValue)
                throw new ValidationException("Delivery address is required.");

            var customerId = _mobileContext.GetCustomerId();
            var address = await _context.CustomerAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Id == addressId.Value, ct)
                ?? throw new ValidationException("Delivery address is invalid.");

            return address.DeliveryZoneId.HasValue
                ? address
                : throw new ValidationException("Delivery address must have a delivery zone.");
        }

        private async Task<Dictionary<Guid, Product>> LoadProductsAsync(
            List<CustomerOrderItemRequest> items,
            CancellationToken ct)
        {
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Include(p => p.RecipeItems)
                    .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(mg => mg.Modifiers)
                            .ThenInclude(m => m.RecipeItems)
                                .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.Options)
                    .ThenInclude(o => o.RecipeItems)
                        .ThenInclude(ri => ri.RawMaterial)
                .Where(p => productIds.Contains(p.Id) && p.IsActive)
                .ToDictionaryAsync(p => p.Id, ct);

            if (products.Count != productIds.Count)
                throw new ValidationException("One or more products are invalid or inactive.");

            return products;
        }

        private static List<OrderItem> BuildOrderItems(
            List<CustomerOrderItemRequest> items,
            Dictionary<Guid, Product> products,
            Guid tenantId,
            Guid branchId)
        {
            return items.Select(item =>
            {
                ValidateQuantity(item.Quantity);
                var product = products[item.ProductId];
                var modifiers = ResolveModifiers(product, item.Modifiers, tenantId);
                var option = ResolveOption(product, item.SelectedOptionId);
                var unitPrice = ResolveUnitPrice(product, option);

                return new OrderItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    Price = unitPrice,
                    UnitPriceSnapshot = unitPrice,
                    LineTotalSnapshot = CalculateLineTotal(unitPrice, modifiers, item.Quantity),
                    Notes = NormalizeNullable(item.Notes),
                    IsReady = false,
                    SelectedOptionId = option?.Id,
                    SelectedOptionName = option?.Name,
                    SelectedOptionNameAr = option?.NameAr,
                    Modifiers = modifiers,
                    RecipeSnapshotItems = BuildRecipeSnapshot(product, modifiers, option, item.Quantity, tenantId)
                };
            }).ToList();
        }

        private static List<OrderItemModifier> ResolveModifiers(
            Product product,
            List<CustomerCartItemModifierRequest> selected,
            Guid tenantId)
        {
            var lookup = product.ProductModifierGroups
                .SelectMany(pmg => pmg.ModifierGroup.Modifiers)
                .ToDictionary(m => m.Id, m => m);

            return selected.Select(s =>
            {
                if (!lookup.TryGetValue(s.ModifierId, out var modifier))
                    throw new ValidationException("One or more modifiers are invalid for this product.");

                return new OrderItemModifier
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ModifierId = modifier.Id,
                    ModifierName = modifier.Name,
                    ModifierNameAr = modifier.NameAr,
                    Price = modifier.PriceAdjustment,
                    Quantity = Math.Clamp(s.Quantity, 1, MaxQuantity)
                };
            }).ToList();
        }

        private static ProductOption? ResolveOption(Product product, Guid? selectedOptionId)
        {
            if (!selectedOptionId.HasValue)
                return null;

            return product.Options.FirstOrDefault(o => o.Id == selectedOptionId.Value && o.IsActive)
                ?? throw new ValidationException("Selected product option is invalid.");
        }

        private static List<OrderItemRecipeSnapshot> BuildRecipeSnapshot(
            Product product,
            List<OrderItemModifier> modifiers,
            ProductOption? option,
            int quantity,
            Guid tenantId)
        {
            if (option?.RecipeItems.Any() == true)
                return option.RecipeItems.Select(ri => BuildSnapshot(ri.RawMaterialId, ri.RawMaterial, ri.Amount, quantity, tenantId, null, option.Id)).ToList();

            var modifierRecipes = product.ProductModifierGroups
                .SelectMany(pmg => pmg.ModifierGroup.Modifiers)
                .Where(m => modifiers.Any(selected => selected.ModifierId == m.Id) && m.RecipeItems.Any())
                .SelectMany(m => m.RecipeItems.Select(ri => BuildSnapshot(ri.RawMaterialId, ri.RawMaterial, ri.Amount, quantity, tenantId, m.Id, null)))
                .ToList();

            return modifierRecipes.Count > 0
                ? modifierRecipes
                : product.RecipeItems.Select(ri => BuildSnapshot(ri.RawMaterialId, ri.RawMaterial, ri.Amount, quantity, tenantId, null, null)).ToList();
        }

        private static OrderItemRecipeSnapshot BuildSnapshot(
            Guid rawMaterialId,
            RawMaterial rawMaterial,
            decimal amount,
            int quantity,
            Guid tenantId,
            Guid? modifierId,
            Guid? optionId)
            => new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RawMaterialId = rawMaterialId,
                RawMaterialName = rawMaterial.Name,
                RawMaterialNameAr = rawMaterial.NameAr,
                Quantity = amount * quantity,
                SourceModifierId = modifierId,
                SourceOptionId = optionId
            };

        private static OrderDto MapOrderForKitchen(Order order)
            => new()
            {
                Id = order.Id,
                OrderNumber = order.DisplayOrderNumber ?? order.OrderNumber,
                DisplayOrderNumber = order.DisplayOrderNumber,
                SystemOrderNumber = order.OrderNumber,
                OrderType = order.OrderType.ToString(),
                OrderSource = order.OrderSource.ToString(),
                TableName = order.TableName,
                Status = order.Status.ToString(),
                TotalAmount = order.TotalAmount,
                CustomerId = order.CustomerId,
                CustomerPhone = order.CustomerPhone,
                CreatedAt = order.CreatedAt,
                Items = order.OrderItems.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.Price,
                    LineTotalAmount = i.LineTotalSnapshot ?? CalculateLineTotal(i),
                    Notes = i.Notes,
                    ItemStatus = "Prepared",
                    Modifiers = i.Modifiers.Select(m => new OrderItemModifierDto
                    {
                        ModifierId = m.ModifierId,
                        ModifierName = m.ModifierName,
                        ModifierNameAr = m.ModifierNameAr,
                        Price = m.Price,
                        Quantity = m.Quantity
                    }).ToList()
                }).ToList()
            };

        private static OrderTotals CalculateTotals(IEnumerable<OrderItem> items)
            => new(items.Sum(CalculateLineTotal));

        private static decimal CalculateLineTotal(OrderItem item)
            => CalculateLineTotal(item.Price, item.Modifiers, item.Quantity);

        private static decimal CalculateLineTotal(decimal unitPrice, IEnumerable<OrderItemModifier> modifiers, int quantity)
            => (unitPrice + modifiers.Sum(m => m.Price * m.Quantity)) * quantity;

        private static decimal ResolveUnitPrice(Product product, ProductOption? option)
            => option?.Price ?? product.DiscountedPrice ?? product.BasePrice;

        private static void ValidateCreateRequest(CustomerOrderCreateRequest request, CustomerMobileOrderType orderType)
        {
            if (request.Items.Count == 0)
                throw new ValidationException("At least one item is required.");

            if (orderType == CustomerMobileOrderType.Delivery && !request.DeliveryAddressId.HasValue)
                throw new ValidationException("Delivery address is required.");
        }

        private static void ValidateQuantity(int quantity)
        {
            if (quantity is < 1 or > MaxQuantity)
                throw new ValidationException($"Quantity must be between 1 and {MaxQuantity}.");
        }

        private static CustomerMobileOrderType ParseOrderType(string value)
        {
            return Enum.TryParse<CustomerMobileOrderType>(value, true, out var parsed)
                ? parsed
                : throw new ValidationException("Order type is invalid.");
        }

        private static string GenerateOrderNumber(OrderType orderType, Guid orderId, DateTime createdAt)
        {
            var prefix = orderType == OrderType.Delivery ? "MD" : "MP";
            return $"{prefix}-{createdAt.ToUniversalTime():yyyyMMddHHmmssfff}-{orderId:N}";
        }

        private static string FormatAddress(CustomerAddress address)
        {
            var parts = new[] { address.Area, address.Street, address.Building, address.Floor, address.Apartment }
                .Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(", ", parts);
        }

        private static string? NormalizeNullable(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private sealed record OrderTotals(decimal Subtotal);
    }
}
