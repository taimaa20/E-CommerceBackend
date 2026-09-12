using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Hubs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Services;
using MediatR;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Options;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services.Caching;
using RestaurantPos.Api.Services.Time;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly IHubContext<KitchenHub> _hubContext;
        private readonly IMediator _mediator;
        private readonly ILogger<OrdersController> _logger;
        private readonly IInventoryService _inventoryService;
        private readonly INotificationService _notificationService;
        private readonly ICacheService _cache;
        private readonly ITenantResolver _tenantResolver;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly ISettingsService _settingsService;
        private readonly IDeliveryZoneService _deliveryZoneService;
        private readonly IOptions<DeliveryPartnersOptions> _deliveryPartnersOptions;
        private readonly Modules.Dashboard.Services.Caching.IDashboardCacheService _dashboardCache;
        private readonly IOrderDisplayNumberService _displayNumberService;
        private readonly IBranchContext _branchContext;
        private readonly IBranchConfigurationService _branchConfigurationService;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ICurrentBranchProvider _currentBranchProvider;
        private readonly IProductStockLedger _productStockLedger;
        private readonly IOnlineShoppingService _onlineShoppingService;
        private static readonly TimeSpan ActiveTakeawayOrderWindow = TimeSpan.FromHours(24);
        private const int OrderCancelReasonMaxLength = 500;
        private const int CustomerPhoneMaxLength = 20;
        private const int DeliveryTextMaxLength = 500;
        private const int TalabatOrderNumberMaxLength = 64;
        private const int TalabatCustomerNameMaxLength = 120;
        private const int PartnerOrderNumberMaxLength = 64;
        private const int PartnerCustomerNameMaxLength = 120;
        private const int PaymentMethodMaxLength = 50;
        private const string TalabatTableName = "TALABAT";
        private const string DeliveryPartnerTableName = "DELIVERY PARTNER";
        private const string PublicOrderNumberExistsMessage = "Public order number already exists.";
        private const string ClientOrderReplayMismatchMessage = "Client order UUID was replayed with a different public order number.";
        private const string TalabatOrderNumberExistsMessage = "Talabat order number already exists.";
        private const string PartnerOrderNumberExistsMessage = "Partner order number already exists for this delivery partner.";
        private const string DeliveryPartnersDisabledMessage = "Delivery partners feature is disabled.";
        private const string ReasonCodePriceDifference = "PriceDifference";
        private const string CorrectionModePerItem = "PerItem";
        private const string CorrectionModeOrderTotal = "OrderTotal";
        private const string ReasonCodeNoDiscountApplied = "NoDiscountApplied";
        private const string ReasonCodeExtraDiscountApplied = "ExtraDiscountApplied";
        private const string ReasonCodeOther = "Other";
        private const string ReasonPriceDifference = "Price Difference";
        private const string ReasonNoDiscountApplied = "No Discount Applied";
        private const string ReasonExtraDiscountApplied = "Extra Discount Applied";
        private const string ReasonOther = "Other";

        public OrdersController(PosDbContext context, IHubContext<KitchenHub> hubContext, IMediator mediator, ILogger<OrdersController> logger, IInventoryService inventoryService, INotificationService notificationService, ICacheService cache, ITenantResolver tenantResolver, IPaymentService paymentService, IOrderService orderService, ISettingsService settingsService, IDeliveryZoneService deliveryZoneService, IOptions<DeliveryPartnersOptions> deliveryPartnersOptions, Modules.Dashboard.Services.Caching.IDashboardCacheService dashboardCache, IOrderDisplayNumberService displayNumberService, IBranchContext branchContext, IBranchConfigurationService branchConfigurationService, ICurrentUserAccessor currentUser, ICurrentBranchProvider currentBranchProvider, IOnlineShoppingService onlineShoppingService, IProductStockLedger productStockLedger)
        {
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
            _context = context;
            _hubContext = hubContext;
            _mediator = mediator;
            _logger = logger;
            _inventoryService = inventoryService;
            _notificationService = notificationService;
            _cache = cache;
            _tenantResolver = tenantResolver;
            _paymentService = paymentService;
            _orderService = orderService;
            _settingsService = settingsService;
            _deliveryZoneService = deliveryZoneService;
            _deliveryPartnersOptions = deliveryPartnersOptions ?? throw new ArgumentNullException(nameof(deliveryPartnersOptions));
            _dashboardCache = dashboardCache ?? throw new ArgumentNullException(nameof(dashboardCache));
            _displayNumberService = displayNumberService ?? throw new ArgumentNullException(nameof(displayNumberService));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _currentBranchProvider = currentBranchProvider ?? throw new ArgumentNullException(nameof(currentBranchProvider));
            _productStockLedger = productStockLedger ?? throw new ArgumentNullException(nameof(productStockLedger));
            _onlineShoppingService = onlineShoppingService ?? throw new ArgumentNullException(nameof(onlineShoppingService));
        }

        // GET: api/Orders?status=Paid&payment=Cash&type=DineIn&dateFrom=...&dateTo=...&search=...&page=1&pageSize=20&sort=newest
        [HttpGet]
        [Authorize(Roles = AppRoleGroups.CashierOperators)]
        public async Task<ActionResult<PaginatedResponse<OrderListItemDto>>> GetOrders(
            [FromQuery] OrderFilterRequest request,
            CancellationToken ct)
        {
            var branchId = await ResolveReadBranchIdAsync(request.BranchId, ct);
            var result = await _orderService.GetOrdersAsync(
                request,
                _tenantResolver.GetTenantId(),
                branchId,
                IsArabicRequested(Request),
                ct);

            return Ok(result);
        }

        // GET: api/Orders/summary?status=Paid&payment=Cash&type=DineIn&dateFrom=...&dateTo=...&search=...
        [HttpGet("summary")]
        [Authorize(Roles = AppRoleGroups.CashierOperators)]
        public async Task<ActionResult<OrderListSummaryDto>> GetOrdersSummary(
            [FromQuery] OrderFilterRequest request,
            CancellationToken ct)
        {
            var branchId = await ResolveReadBranchIdAsync(request.BranchId, ct);
            var result = await _orderService.GetOrdersSummaryAsync(
                request,
                _tenantResolver.GetTenantId(),
                branchId,
                ct);

            return Ok(result);
        }

        // GET: api/Orders/export?status=Paid&payment=Cash&type=DineIn&dateFrom=...&dateTo=...&search=...
        [HttpGet("export")]
        [Authorize(Roles = AppRoleGroups.CashierOperators)]
        public async Task<IActionResult> ExportOrders(
            [FromQuery] OrderFilterRequest request,
            CancellationToken ct)
        {
            var branchId = await ResolveReadBranchIdAsync(request.BranchId, ct);
            var csv = await _orderService.ExportOrdersCsvAsync(
                request,
                _tenantResolver.GetTenantId(),
                branchId,
                ct);

            var fileName = $"orders-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
        }

        // GET: api/Orders/active
        [HttpGet("active")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<ActionResult<List<OrderDto>>> GetActiveOrders(
            [FromQuery] bool includeCancelled = false,
            CancellationToken ct = default)
        {
            var isArabic = IsArabicRequested(Request);
            var branchId = await GetCurrentBranchIdAsync(ct);
            var todayUtc = DateTime.UtcNow.Date;
            var yesterdayUtc = todayUtc.AddDays(-1);
            var orders = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.PaidByUser)
                .Include(o => o.Customer)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product) // Include Product definition
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.RecipeSnapshotItems)
                .Include(o => o.Payments)
.Where(o =>
    o.BranchId == branchId &&
    (includeCancelled || o.Status != OrderStatus.Cancelled) &&
    o.Status != OrderStatus.Completed &&
    (
        o.CreatedAt >= todayUtc
        ||
        (
            o.CreatedAt >= yesterdayUtc &&
            o.CreatedAt < todayUtc &&
            o.OrderItems.Any(oi => !oi.IsReady)
        )
    )
).OrderByDescending(o => o.LastUpdatedAt ?? o.CreatedAt)
                .ToListAsync(ct);

            var orderDtos = orders.Select(o => MapOrderToDto(o, isArabic)).ToList();
            await ApplyPendingCancellationStateAsync(orderDtos, ct);

            return Ok(orderDtos);
        }

        // GET: api/Orders/kitchen?filter=today|all
        // KDS endpoint — returns orders relevant to the kitchen screen.
        //
        // filter=today (default):
        //   (CreatedAt >= todayStart)
        //   OR (CreatedAt < todayStart AND Status NOT IN (Ready, Served, Completed, Cancelled))
        //
        // filter=all:
        //   All non-cancelled orders from the last 7 days.
        [HttpGet("kitchen")]
        [Authorize(Roles = AppRoleGroups.KitchenOperators)]
        public async Task<ActionResult<List<OrderDto>>> GetKitchenOrders(
            [FromQuery] string filter = "today",
            CancellationToken ct = default)
        {
            var isArabic = IsArabicRequested(Request);
            var branchId = await GetCurrentBranchIdAsync(ct);
            var isToday = !string.Equals(filter, "all", StringComparison.OrdinalIgnoreCase);
            var cacheKey = isToday
                ? $"{CacheKeys.KitchenToday(isArabic)}:{branchId:N}"
                : $"{CacheKeys.KitchenAll(isArabic)}:{branchId:N}";

            // Cache List<OrderDto> directly — mapping runs only on a cache miss,
            // not on every request during the TTL window.
            var dtos = await _cache.GetOrCreateAsync<List<OrderDto>>(cacheKey, async () =>
            {
                var todayUtc = DateTime.UtcNow.Date;
                var lookbackUtc = todayUtc.AddDays(-3);

                // AsSplitQuery prevents cartesian explosion:
                // multiple collection Includes (OrderItems×Product, OrderItems×Modifiers)
                // emit separate SQL queries instead of one cross-joined mega-query.
                IQueryable<Order> query = _context.Orders
                    .Include(o => o.Table)
                    .Include(o => o.Customer)
                    .Include(o => o.Offer)
                        .ThenInclude(offer => offer!.OfferProducts)
                            .ThenInclude(op => op.Product)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Modifiers)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.RecipeSnapshotItems)
                    .Include(o => o.Payments)
                    .AsSplitQuery();

                if (isToday)
                {
                    // TODAY filter (DB-level):
                    //   1. CreatedAt >= todayStart  (all of today regardless of status)
                    //   2. CreatedAt >= lookbackUtc AND CreatedAt < todayStart
                    //      AND Status NOT IN (Ready, Served, Completed)
                    //      — previous-day orders that are still active, bounded by the same
                    //        3-day lookback to avoid pulling every historical Preparing order.
                    query = query.Where(o =>
                        o.BranchId == branchId &&
                        o.Status != OrderStatus.Cancelled &&
                        (
                            o.CreatedAt >= todayUtc
                            ||
                            (
                                o.CreatedAt >= lookbackUtc &&
                                o.CreatedAt < todayUtc &&
                                o.Status != OrderStatus.Ready &&
                                o.Status != OrderStatus.Served &&
                                o.Status != OrderStatus.Completed
                            )
                        ));
                }
                else
                {
                    // ALL filter: last 3 days, exclude cancelled
                    query = query.Where(o =>
                        o.BranchId == branchId &&
                        o.Status != OrderStatus.Cancelled &&
                        o.CreatedAt >= lookbackUtc);
                }

                var orders = await query
                    .OrderByDescending(o => o.LastUpdatedAt ?? o.CreatedAt)
                    .AsNoTracking()
                    .ToListAsync(ct);

                var orderDtos = orders
                    .Where(ShouldExposeOperationally)
                    .Select(o => MapOrderToDto(o, isArabic))
                    .ToList();

                await ApplyPendingCancellationStateAsync(orderDtos, ct);
                return orderDtos;
            }, absoluteExpiration: TimeSpan.FromSeconds(15), cancellationToken: ct);

            if (dtos is null)
                return Ok(new List<OrderDto>());

            return Ok(dtos);
        }

        // GET: api/Orders/tracker
        // Public endpoint for Order Tracker display (no sensitive data)
        [HttpGet("tracker")]
        [AllowAnonymous]
        public async Task<ActionResult<List<TrackerOrderDto>>> GetTrackerOrders(CancellationToken ct = default)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var branchId = await GetRequestBranchIdAsync(tenantId, ct);
            var todayUtc = DateTime.UtcNow.Date.AddDays(-1);
            var yesterdayUtc = todayUtc.AddDays(-2);

            // Takeaway: removed from tracker when staff clicks "Reserved" (Completed status = picked up).
            // Dine-In:  removed from tracker when Served (regardless of payment).
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payments)
                .Where(o =>
                    o.BranchId == branchId &&
                    o.Status != OrderStatus.Cancelled &&
                    o.Status != OrderStatus.Served &&
                    o.Status != OrderStatus.Completed &&
                    (
                        o.CreatedAt >= todayUtc
                        ||
                        (
                            o.CreatedAt >= yesterdayUtc &&
                            o.CreatedAt < todayUtc &&
                            o.OrderItems.Any(oi => !oi.IsReady)
                        )
                    )
                )
                .OrderBy(o => o.CreatedAt)
                .ToListAsync(ct);

            var trackerOrders = orders
                .Where(ShouldExposeOperationally)
                .Select(o => new TrackerOrderDto
                {
                    Id = o.Id,
                    OrderNumber = ResolveOrderNumber(o),
                    OrderType = o.OrderType.ToString(),
                    Status = o.OrderItems.All(oi => oi.IsReady) ? "Ready" : "Preparing",
                    IsPaid = OrderPaymentHelper.BuildSnapshot(o).IsPaid,
                    CreatedAt = o.CreatedAt
                })
                .ToList();

            return Ok(trackerOrders);
        }

        // POST: api/Orders
        [HttpPost]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<ActionResult<OrderDto>> CreateOrder(OrderCreateDto input)
        {
            LogCreateOrderRequest(input);
            return await ProcessCreateOrder(input);
        }

        // POST: api/Orders/public
        [HttpPost("public")]
        [AllowAnonymous]
        [EnableRateLimiting("api")]
        public async Task<ActionResult<OrderDto>> CreatePublicOrder(OrderCreateDto input)
        {
            if (!input.TableId.HasValue || input.TableId == Guid.Empty ||
                string.Equals(input.OrderSource, nameof(OrderSource.Online), StringComparison.OrdinalIgnoreCase))
            {
                var tenantId = _tenantResolver.GetTenantId();
                var ct = HttpContext.RequestAborted;
                var branchId = await GetRequestBranchIdAsync(tenantId, ct);
                input = await _onlineShoppingService.PrepareStoreOrderAsync(input, tenantId, branchId, ct);
            }
             return await ProcessCreateOrder(input);
        }

        [HttpPost("validate-price")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<ActionResult<ValidatePriceResponse>> ValidatePrice([FromBody] ValidatePriceRequest request)
        {
            var validation = await ValidatePriceInternalAsync(request);
            return Ok(validation);
        }

        // GET: api/Orders/takeaway/active
        [HttpGet("takeaway/active")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<ActionResult<List<OrderDto>>> GetActiveTakeawayOrders(CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var branchId = await GetCurrentBranchIdAsync(ct);
            var cacheKey = CacheKeys.TakeawayActiveOrders(tenantId, branchId);
            var cutoffUtc = DateTime.UtcNow.Subtract(ActiveTakeawayOrderWindow);

            // 8-second absolute TTL: short enough that staff see status changes quickly,
            // long enough to absorb rapid polling without hammering the DB.
            var orderDtos = await _cache.GetOrCreateAsync(
                cacheKey,
                factory: () => FetchActiveTakeawayOrdersFromDbAsync(tenantId, branchId, cutoffUtc, ct),
                absoluteExpiration: TimeSpan.FromSeconds(8),
                cancellationToken: ct);

            return Ok((orderDtos ?? new List<OrderDto>())
                .Where(o => o.CreatedAt >= cutoffUtc
                         && o.Status != OrderStatus.Cancelled.ToString()
                         && o.Status != OrderStatus.Completed.ToString())
                .ToList());
        }

        /// <summary>
        /// Executes the optimised DB query for the takeaway/delivery active board.
        /// Kept private so it can be called directly from cache factory without closing over the HTTP context.
        /// Optimisations vs. the original query:
        ///   1. AsNoTracking()          — skips EF change-tracker (read-only path).
        ///   2. No Include(oi.Product)  — was loading the full Product row per item just for one column.
        ///      Replaced with a single batch SELECT (Id, PreparationStation) using an IN (...) clause.
        ///   3. IX_Orders_TakeawayActive index on (TenantId, OrderType, Status, CreatedAt)
        ///      lets PostgreSQL satisfy the WHERE + ORDER BY in one index scan with no heap sort.
        /// </summary>
        private async Task<List<OrderDto>> FetchActiveTakeawayOrdersFromDbAsync(Guid tenantId, Guid branchId, DateTime cutoffUtc, CancellationToken ct)
        {
            // ── Step 1: load orders + offer chain + item modifiers ───────────────
            // No Include for oi.Product: we fetch only PreparationStation separately (step 2).
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.PaidByUser)
                .Include(o => o.Customer)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.RecipeSnapshotItems)
                .Include(o => o.Payments)
                .Where(o => o.TenantId == tenantId
                         && o.BranchId == branchId
                         && (o.OrderType == OrderType.Takeaway || o.OrderType == OrderType.Delivery)
                         && o.Status != OrderStatus.Cancelled
                         && o.Status != OrderStatus.Completed
                         && o.CreatedAt >= cutoffUtc)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync(ct);

            // ── Step 2: batch-load PreparationStation for all distinct products ──
            // One SELECT with IN (...) instead of a full JOIN that loads every Product column.
            var productIds = orders
                .SelectMany(o => o.OrderItems.Select(oi => oi.ProductId))
                .Distinct()
                .ToList();

            Dictionary<Guid, int> stationLookup;
            if (productIds.Count == 0)
            {
                stationLookup = new Dictionary<Guid, int>();
            }
            else
            {
                stationLookup = await _context.Products
                    .AsNoTracking()
                    .Where(p => productIds.Contains(p.Id))
                    .Select(p => new { p.Id, Station = (int)p.PreparationStation })
                    .ToDictionaryAsync(x => x.Id, x => x.Station, ct);
            }

            // ── Step 3: project to OrderDto in memory ────────────────────────────
            return orders
                .Select(o =>
                {
                    var paymentSnapshot = OrderPaymentHelper.BuildSnapshot(o);
                    if (!paymentSnapshot.IsPaid)
                    {
                        return null;
                    }

                    return new OrderDto
                    {
                        Id = o.Id,
                        OrderNumber = ResolveOrderNumber(o),
                        DisplayOrderNumber = o.DisplayOrderNumber,
                        SystemOrderNumber = o.OrderNumber,
                        ClientOrderUuid = o.ClientOrderUuid,
                        PublicOrderNumber = o.PublicOrderNumber,
                        OrderType = o.OrderType.ToString(),
                        OrderSource = o.OrderSource.ToString(),
                        TalabatOrderNumber = o.TalabatOrderNumber,
                        TalabatCustomerName = o.TalabatCustomerName,
                        TalabatCustomerPhone = o.TalabatCustomerPhone,
                        TalabatPaymentMethod = o.TalabatPaymentMethod,
                        TalabatPickupTime = o.TalabatPickupTime,
                        TalabatDeliveryFee = o.TalabatDeliveryFee,
                        TalabatServiceFee = o.TalabatServiceFee,
                        DeliveryPartnerId = o.DeliveryPartnerId,
                        DeliveryPartnerName = o.DeliveryPartnerName,
                        DeliveryPartnerNameAr = o.DeliveryPartnerNameAr,
                        DeliveryPartnerCode = o.DeliveryPartnerCode,
                        PartnerOrderNumber = o.PartnerOrderNumber,
                        PartnerCustomerName = o.PartnerCustomerName,
                        PartnerCustomerPhone = o.PartnerCustomerPhone,
                        PartnerPaymentMethod = o.PartnerPaymentMethod,
                        PartnerPickupTime = o.PartnerPickupTime,
                        PartnerDeliveryFee = o.PartnerDeliveryFee,
                        PartnerServiceFee = o.PartnerServiceFee,
                        OfferId = o.OfferId,
                        OfferName = o.Offer?.Name,
                        OfferNameAr = o.Offer?.NameAr,
                        OfferNote = o.OfferNote,
                        OfferProducts = MapOrderOfferProducts(o.Offer),
                        TicketId = o.TicketId,
                        TableName = ResolveTableDisplayName(o, isArabic: false),
                        Subtotal = o.Subtotal,
                        DiscountPercentage = o.DiscountPercentage,
                        DiscountAmount = o.DiscountAmount,
                        DiscountGroupId = o.DiscountGroupId,
                        DiscountGroupName = o.DiscountGroupName,
                        DiscountGroupType = o.DiscountGroupType,
                        DiscountGroupValue = o.DiscountGroupValue,
                        DiscountGroupAmount = o.DiscountGroupAmount,
                        ServiceChargeRate = o.ServiceChargeRate,
                        ServiceChargeAmount = o.ServiceChargeAmount,
                        TaxRate = o.TaxRate,
                        TaxAmount = o.TaxAmount,
                        IsVoucherApplied = o.IsVoucherApplied,
                        VoucherDiscountAmount = o.VoucherDiscountAmount,
                        VoucherAppliedAt = o.VoucherAppliedAt,
                        TotalAmount = o.TotalAmount,
                        FoodSubtotal = o.FoodSubtotal,
                        CustomerDeliveryFee = o.CustomerDeliveryFee,
                        ActualDeliveryCost = o.ActualDeliveryCost,
                        DeliveryMargin = o.DeliveryMargin,
                        MarketplaceDeliveryFee = o.MarketplaceDeliveryFee,
                        MarketplaceServiceFee = o.MarketplaceServiceFee,
                        NetRestaurantRevenue = o.NetRestaurantRevenue > 0m ? o.NetRestaurantRevenue : o.TotalAmount,
                        CostSharingTotalCommission = o.CostSharingTotalCommission,
                        CostSharingRestaurantShare = o.CostSharingRestaurantShare,
                        CostSharingCounterpartyShare = o.CostSharingCounterpartyShare,
                        CostSharingNetSettlement = o.CostSharingNetSettlement,
                        CostSharingCalculatedAt = o.CostSharingCalculatedAt,
                        DeliveryZoneId = o.DeliveryZoneId,
                        DeliveryZoneName = o.DeliveryZoneName,
                        DeliveryFee = o.DeliveryFee,
                        DeliveryCost = o.DeliveryCost,
                        DeliveryPaymentMode = o.DeliveryPaymentMode?.ToString(),
                        DeliveryAddress = o.DeliveryAddress,
                        DeliveryNotes = o.DeliveryNotes,
                        SubmittedPaymentReference = o.SubmittedPaymentReference,
                        Status = o.Status.ToString(),
                        IsPaid = paymentSnapshot.IsPaid,
                        PaymentStatus = paymentSnapshot.PaymentStatus,
                        PaymentMethod = paymentSnapshot.PaymentMethod,
                        CashierName = ResolveUserDisplayName(o.PaidByUser),
                        CreatedBy = o.WaiterId,
                        PaidAmount = paymentSnapshot.PaidAmount,
                        RemainingAmount = paymentSnapshot.RemainingAmount,
                        CustomerId = o.CustomerId,
                        CustomerName = o.Customer?.Name ?? o.PartnerCustomerName ?? o.TalabatCustomerName,
                        CustomerNameAr = o.Customer?.NameAr,
                        CustomerPhone = o.CustomerPhone ?? o.Customer?.PhoneNumber ?? o.PartnerCustomerPhone ?? o.TalabatCustomerPhone,
                        AmountTendered = paymentSnapshot.AmountTendered,
                        ChangeAmount = paymentSnapshot.ChangeAmount,
                        CreatedAt = o.CreatedAt,
                        DispatchedAt = o.DispatchedAt,
                        Payments = paymentSnapshot.Payments,
                        Items = o.OrderItems.Select(oi => new OrderItemDto
                        {
                            Id = oi.Id,
                            ProductId = oi.ProductId,
                            ProductName = oi.ProductName,
                            Quantity = oi.Quantity,
                            UnitPrice = oi.IsComplimentary ? 0 : Math.Round(oi.Price * (1 - o.DiscountPercentage / 100m), 2, MidpointRounding.AwayFromZero),
                            LineTotalAmount = CalcLineTotalAmount(oi),
                            UnitPriceSnapshot = oi.UnitPriceSnapshot,
                            LineTotalSnapshot = oi.LineTotalSnapshot,
                            PartnerPriceSnapshot = oi.PartnerPriceSnapshot,
                            HasPartnerDiscountOverride = oi.HasPartnerDiscountOverride,
                            PartnerOriginalUnitPrice = oi.PartnerOriginalUnitPrice,
                            PartnerDiscountedUnitPrice = oi.PartnerDiscountedUnitPrice,
                            PartnerDiscountReason = oi.PartnerDiscountReason,
                            PartnerDiscountUpdatedAt = oi.PartnerDiscountUpdatedAt,
                            PartnerDiscountUpdatedBy = oi.PartnerDiscountUpdatedBy,
                            PartnerDiscountUpdatedByName = ResolveUserDisplayName(oi.PartnerDiscountUpdatedByUser),
                            Notes = oi.Notes,
                            IsComplimentary = oi.IsComplimentary,
                            ItemStatus = oi.IsReady ? "Ready" : "Prepared",
                            IsNewlyAdded = oi.IsNewlyAdded,
                            PreparationStation = stationLookup.GetValueOrDefault(oi.ProductId, 0),
                            SelectedOptionName = oi.SelectedOptionName,
                            SelectedOptionNameAr = oi.SelectedOptionNameAr,
                            Modifiers = (oi.Modifiers ?? Enumerable.Empty<OrderItemModifier>()).Select(MapModifierDto).ToList(),
                            RecipeSnapshot = (oi.RecipeSnapshotItems ?? Enumerable.Empty<OrderItemRecipeSnapshot>()).Select(rs => new OrderItemRecipeSnapshotDto
                            {
                                RawMaterialId = rs.RawMaterialId,
                                SourceModifierId = rs.SourceModifierId,
                                SourceOptionId = rs.SourceOptionId,
                                SourceRecipeItemId = rs.SourceRecipeItemId,
                                SourceAlternativeId = rs.SourceAlternativeId,
                                SourceAlternativeName = rs.SourceAlternativeName,
                                SourceAlternativeNameAr = rs.SourceAlternativeNameAr,
                                RawMaterialName = rs.RawMaterialName,
                                RawMaterialNameAr = rs.RawMaterialNameAr,
                                Quantity = rs.Quantity
                            }).ToList()
                        }).ToList()
                    };
                })
                .Where(dto => dto != null)
                .Select(dto => dto!)
                .ToList();
        }

        private async Task<ActionResult<OrderDto>> ProcessCreateOrder(OrderCreateDto input)
        {
            // --- Input Validation ---
            var validationError = ValidateOrderInput(input, _deliveryPartnersOptions.Value.Enabled);
            if (validationError != null)
            {
                _logger.LogWarning(
                    "Order create validation failed for client order {ClientOrderUuid}: {ValidationError}",
                    input.ClientOrderUuid,
                    validationError);
                return BadRequest(validationError);
            }

            var orderType = (OrderType)input.OrderType;
            var orderSource = ResolveOrderSource(input.OrderSource);
            var talabatDetails = NormalizeTalabatDetails(input, orderSource);
            var tenantId = input.TenantId != Guid.Empty ? input.TenantId : _tenantResolver.GetTenantId();
            var branchId = await GetRequestBranchIdAsync(tenantId, HttpContext.RequestAborted);
            var caller = GetCallerIdentity();
            var priceDifference = ResolveCreatePriceDifference(input, orderSource, caller);
            if (priceDifference.Error != null)
                return BadRequest(priceDifference.Error);
            var partnerDetails = await ResolvePartnerDetailsAsync(input, orderSource, HttpContext.RequestAborted);
            if (partnerDetails.Error != null)
                return BadRequest(partnerDetails.Error);
            var clientOrderUuid = NormalizeClientOrderUuid(input.ClientOrderUuid);
            var publicOrderNumber = NormalizePublicOrderNumber(input.PublicOrderNumber);
            if (clientOrderUuid != null)
            {
                var existingOrder = await FindExistingClientOrderAsync(tenantId, clientOrderUuid, HttpContext.RequestAborted);
                if (existingOrder != null)
                {
                    _logger.LogInformation(
                        "Order replay reconciled for client order {ClientOrderUuid} to server order {OrderId}",
                        clientOrderUuid,
                        existingOrder.Id);
                    var reconciliationError = await ReconcilePublicOrderNumberAsync(
                        existingOrder,
                        publicOrderNumber,
                        HttpContext.RequestAborted);
                    if (reconciliationError != null)
                        return Conflict(new { message = reconciliationError });

                    return Ok(MapOrderToDto(existingOrder, IsArabicRequested(Request)));
                }
            }

            if (publicOrderNumber != null &&
                await PublicOrderNumberExistsAsync(tenantId, publicOrderNumber, HttpContext.RequestAborted))
            {
                return Conflict(new { message = PublicOrderNumberExistsMessage });
            }

            if (talabatDetails.OrderNumber != null &&
                await TalabatOrderNumberExistsAsync(tenantId, talabatDetails.OrderNumber, HttpContext.RequestAborted))
            {
                return Conflict(new { message = TalabatOrderNumberExistsMessage });
            }

            if (partnerDetails.PartnerId.HasValue &&
                partnerDetails.OrderNumber != null &&
                await PartnerOrderNumberExistsAsync(tenantId, partnerDetails.PartnerId.Value, partnerDetails.OrderNumber, HttpContext.RequestAborted))
            {
                return Conflict(new { message = PartnerOrderNumberExistsMessage });
            }

            var normalizedCustomerPhone = NormalizeCustomerPhone(input.CustomerPhone) ?? partnerDetails.CustomerPhone ?? talabatDetails.CustomerPhone;
            Customer? customer = null;
            if (orderSource == OrderSource.Online)
            {
                customer = await _onlineShoppingService.ResolveCustomerAsync(
                    tenantId,
                    input.CustomerName ?? string.Empty,
                    normalizedCustomerPhone ?? string.Empty,
                    input.CustomerEmail,
                    HttpContext.RequestAborted);
                normalizedCustomerPhone = customer.PhoneNumber;
            }
            else if (input.CustomerId.HasValue)
            {
                customer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == input.CustomerId.Value, HttpContext.RequestAborted);

                if (customer == null)
                    return BadRequest("Customer not found.");

                normalizedCustomerPhone ??= customer.PhoneNumber;
            }

            // Affiliation discount group: resolved entirely server-side. The client sends only
            // the Id; the tenant + soft-delete query filter scopes the lookup, and we reject any
            // group that is missing or inactive so a manipulated request cannot apply a discount.
            DiscountGroup? discountGroup = null;
            if (input.DiscountGroupId.HasValue)
            {
                discountGroup = await _context.DiscountGroups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == input.DiscountGroupId.Value, HttpContext.RequestAborted);

                if (discountGroup == null || !discountGroup.IsActive)
                    return BadRequest("Discount group not found or inactive.");
            }

            if (orderType == OrderType.Delivery && string.IsNullOrWhiteSpace(normalizedCustomerPhone))
                return BadRequest("Customer phone is required for delivery orders.");

            var priceValidation = await ValidatePriceInternalAsync(new ValidatePriceRequest
            {
                OfferId = input.OfferId,
                OfferQty = input.OfferQty,
                Items = input.Items.Select(item => new OrderLineValidationItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.Price
                }).ToList()
            });

            if (!priceValidation.IsValid && input.OfferId.HasValue)
            {
                return BadRequest(priceValidation);
            }

            if (priceValidation.UnavailableItems.Count > 0)
            {
                _logger.LogWarning(
                    "Order availability override applied for products: {UnavailableItems}",
                    string.Join(", ", priceValidation.UnavailableItems));
            }

            // 0. Fetch Products for Verification & Station Info (server-side prices)
            var productIds = input.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Include(p => p.RecipeItems)
                    .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.RecipeItems)
                    .ThenInclude(ri => ri.Alternatives)
                        .ThenInclude(a => a.RawMaterial)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(mg => mg.Modifiers)
                            .ThenInclude(m => m.RecipeItems)
                                .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(mg => mg.Modifiers)
                            .ThenInclude(m => m.LinkedProduct)
                                .ThenInclude(lp => lp!.RecipeItems)
                                    .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.Options)
                    .ThenInclude(o => o.RecipeItems)
                        .ThenInclude(ri => ri.RawMaterial)
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, HttpContext.RequestAborted);

            var partnerPricing = await BuildPartnerPricingContextAsync(
                orderSource,
                partnerDetails.PartnerId,
                products.Values,
                HttpContext.RequestAborted);
            if (partnerPricing.UnavailableProductNames.Count > 0)
            {
                return BadRequest($"Products unavailable for selected delivery partner: {string.Join(", ", partnerPricing.UnavailableProductNames)}.");
            }

            var offerUnitPrices = await BuildOfferUnitPriceOverridesAsync(input.Items, input.OfferId, input.OfferQty, products);

            var stockValidationError = ValidateInventoryAvailability(
                input.Items
                    .Where(i => products.ContainsKey(i.ProductId))
                    .Select(i => (products[i.ProductId], i.Quantity, i.Modifiers, i.SelectedOptionId, i.SelectedRecipeAlternatives))
                    .ToList());
            if (stockValidationError != null)
            {
                return BadRequest(stockValidationError);
            }

            if (orderSource == OrderSource.Online)
            {
                var onlineAvailabilityError = await _onlineShoppingService.ValidateAvailabilityAsync(
                    tenantId,
                    branchId,
                    input.Items,
                    HttpContext.RequestAborted);
                if (onlineAvailabilityError != null)
                    return BadRequest(onlineAvailabilityError);
            }

            // Finished goods are stocked as the product itself, so the recipe check above cannot
            // see them. Rejecting here — at the boundary, before anything is written — is what
            // keeps the ledger from ever having to represent negative stock.
            var finishedGoodsError = await _productStockLedger.ValidateAvailabilityAsync(
                branchId,
                input.Items.Select(i => new ProductStockRequest(i.ProductId, i.Quantity)).ToList(),
                IsArabicRequested(Request),
                HttpContext.RequestAborted);
            if (finishedGoodsError != null)
            {
                _logger.LogWarning(
                    "Order create rejected on finished-goods stock for branch {BranchId}: {Reason}",
                    branchId, finishedGoodsError);
                return BadRequest(finishedGoodsError);
            }

            // 1. Build in-memory items using SENT prices (includes discounts)
            var calcItems = input.Items
                .Where(i => products.ContainsKey(i.ProductId))
                .Select(i =>
                {
                    var product = products[i.ProductId];
                    var resolvedModifiers = ResolveOrderItemModifiers(product, i.Modifiers, tenantId);
                    var partnerOverride = partnerPricing.PriceRules.GetValueOrDefault(i.ProductId);
                    var baseUnitPrice = ResolveOrderItemUnitPrice(
                        product,
                        resolvedModifiers,
                        i.SelectedOptionId,
                        i.SelectedRecipeAlternatives,
                        ResolveOfferUnitPrice(i, offerUnitPrices),
                        partnerOverride);
                    return new OrderItem
                    {
                        ProductId = i.ProductId,
                        Price = ResolveCreateOrderUnitPrice(i, baseUnitPrice, priceDifference),
                        Quantity = i.Quantity,
                        IsComplimentary = i.IsComplimentary,
                        Modifiers = resolvedModifiers
                    };
                }).ToList();

            // 2. Single calculation source of truth (backend only). The affiliation discount
            // group type + value are taken from the server-loaded group (never trusted from the
            // client) and snapshotted onto the order so history is immune to later config edits.
            var groupDiscountType = discountGroup is { IsActive: true } ? discountGroup.DiscountType : DiscountValueType.Percentage;
            var groupDiscountValue = discountGroup is { IsActive: true } ? Math.Max(0m, discountGroup.DiscountValue) : 0m;
            var computedTotals = CalculateOrderTotals(calcItems, input.DiscountPercentage, groupDiscountType, groupDiscountValue, input.ServiceChargeRate, input.TaxRate);

            // P1 (offline financial preservation): When the order originated
            // offline (PublicOrderNumber stamped as OFF#…), the cashier
            // committed the customer to specific totals on the printed
            // receipt and against collected cash. Admin price / rate edits
            // during the outage must not silently move those numbers on
            // replay. If the DTO carries the full set of historical
            // amounts, treat them as authoritative; otherwise fall back to
            // the freshly computed values. Online orders always use the
            // freshly computed values — this branch is gated entirely on
            // PublicOrderNumber.
            var isOfflineReplay = !string.IsNullOrWhiteSpace(publicOrderNumber);
            var hasFullAmountSnapshot =
                isOfflineReplay &&
                input.Subtotal.HasValue &&
                input.DiscountAmount.HasValue &&
                input.ServiceChargeAmount.HasValue &&
                input.TaxAmount.HasValue &&
                input.TotalAmount.HasValue;

            var totals = hasFullAmountSnapshot
                ? new OrderTotals(
                    OrderPaymentHelper.RoundCurrency(Math.Max(0m, input.Subtotal!.Value)),
                    OrderPaymentHelper.RoundCurrency(Math.Max(0m, input.DiscountAmount!.Value)),
                    // Offline receipts predate this feature and bake any discount into the
                    // snapshot amounts; recompute the group-discount portion only for display.
                    computedTotals.DiscountGroupAmount,
                    OrderPaymentHelper.RoundCurrency(Math.Max(0m, input.ServiceChargeAmount!.Value)),
                    OrderPaymentHelper.RoundCurrency(Math.Max(0m, input.TaxAmount!.Value)),
                    // Total on the entity is "after discount" (service charge + tax
                    // are added at payment time). For offline preservation we want
                    // the same convention, so prefer the explicit Subtotal-Discount
                    // when both are present and fall back to the snapshot Total.
                    OrderPaymentHelper.RoundCurrency(Math.Max(0m,
                        input.Subtotal!.Value - input.DiscountAmount!.Value)))
                : computedTotals;

            // 2b. Delivery snapshot resolution (delivery orders only). Fee/cost/name/payment-mode are
            // resolved server-side from the active zone (cached, O(1)) and snapshotted onto the order so
            // historical orders are immune to later zone edits. Manual fee override is Admin/Manager-only.
            var deliveryInfo = await ResolveDeliveryForCreateAsync(input, branchId, HttpContext.RequestAborted);
            if (deliveryInfo.Error != null)
                return BadRequest(deliveryInfo.Error);
            var additionalFees = GetAdditionalFeeTotal(deliveryInfo, talabatDetails, partnerDetails);

            // Use a transaction to ensure atomic ticket session management
            await using var transaction = await _context.Database.BeginTransactionAsync(HttpContext.RequestAborted);

            // Session-based ticketing: one ticket per table session
            int ticketId = 0;
            Table? table = null;
            var isTakeaway = orderType == OrderType.Takeaway;
            var isDineIn = orderType == OrderType.DineIn;

            if (isDineIn && input.TableId.HasValue && input.TableId != Guid.Empty)
            {
                table = await _context.Tables
                    .Where(t => t.Id == input.TableId.Value)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);

                if (table != null)
                {
                    // Check if this table already has an active session ticket
                    if (table.CurrentTicketId.HasValue && table.CurrentTicketId > 0)
                    {
                        // Reuse existing session ticket
                        ticketId = table.CurrentTicketId.Value;
                    }
                    else
                    {
                        // Create new session: increment global counter and set as current session ticket
                        ticketId = table.NextTicketId;
                        table.NextTicketId++;
                        table.CurrentTicketId = ticketId; // Start new session with this ticket
                        _context.Tables.Update(table);
                        await _context.SaveChangesAsync(HttpContext.RequestAborted);
                    }
                }
            }

            // 2. Create Order Entity
            var orderId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;
            var orderNumber = GenerateOrderNumber(orderType, orderId, createdAt);
            var displayOrderNumber = await _displayNumberService.GenerateAsync(
                tenantId,
                orderType,
                orderSource,
                hasDeliveryPartner: partnerDetails.PartnerId.HasValue,
                createdAt,
                HttpContext.RequestAborted,
                partnerCode: partnerDetails.PartnerCode,
                isOffline: publicOrderNumber != null);

            var order = new Order
            {
                Id = orderId,
                TenantId = tenantId,
                BranchId = branchId,
                ClientOrderUuid = clientOrderUuid,
                PublicOrderNumber = publicOrderNumber,
                DisplayOrderNumber = displayOrderNumber,
                OrderType = orderType,
                OrderSource = orderSource,
                TableId = input.TableId,
                TableName = ResolveOrderTableName(orderSource, input.TableName, partnerDetails),
                TicketId = ticketId, // All orders in session share same ticketId
                OrderNumber = orderNumber,
                Status = OrderStatus.New,
                CreatedAt = createdAt,
                SyncedAt = DateTime.UtcNow,
                ScheduledFor = input.ScheduledFor?.ToUniversalTime(),
                TotalAmount = hasFullAmountSnapshot
                    ? OrderPaymentHelper.RoundCurrency(Math.Max(0m, input.TotalAmount!.Value))
                    : totals.Total + additionalFees,
                DiscountPercentage = input.DiscountPercentage,
                DiscountGroupId = discountGroup?.Id,
                DiscountGroupName = discountGroup?.Name,
                DiscountGroupType = groupDiscountType,
                DiscountGroupValue = groupDiscountValue,
                ServiceChargeRate = input.ServiceChargeRate,
                TaxRate = input.TaxRate,
                Subtotal = totals.Subtotal,
                DiscountAmount = totals.DiscountAmount,
                DiscountGroupAmount = totals.DiscountGroupAmount,
                ServiceChargeAmount = totals.ServiceChargeAmount,
                TaxAmount = totals.TaxAmount,
                OfferId = input.OfferId,
                OfferNote = input.OfferNote,

                DeliveryZoneId = deliveryInfo.ZoneId,
                DeliveryZoneName = deliveryInfo.ZoneName,
                DeliveryFee = deliveryInfo.IsDelivery ? deliveryInfo.Fee : (decimal?)null,
                DeliveryCost = deliveryInfo.Cost,
                DeliveryPaymentMode = deliveryInfo.PaymentMode,
                DeliveryAddress = deliveryInfo.Address,
                DeliveryNotes = orderSource == OrderSource.Online && !deliveryInfo.IsDelivery
                    ? string.IsNullOrWhiteSpace(input.DeliveryNotes) ? null : input.DeliveryNotes.Trim()
                    : deliveryInfo.Notes,

                TalabatOrderNumber = talabatDetails.OrderNumber,
                TalabatCustomerName = talabatDetails.CustomerName,
                TalabatCustomerPhone = talabatDetails.CustomerPhone,
                TalabatPaymentMethod = talabatDetails.PaymentMethod,
                TalabatPickupTime = talabatDetails.PickupTime,
                TalabatDeliveryFee = talabatDetails.DeliveryFee,
                TalabatServiceFee = talabatDetails.ServiceFee,

                DeliveryPartnerId = partnerDetails.PartnerId,
                DeliveryPartnerName = partnerDetails.PartnerName,
                DeliveryPartnerNameAr = partnerDetails.PartnerNameAr,
                DeliveryPartnerCode = partnerDetails.PartnerCode,
                PartnerOrderNumber = partnerDetails.OrderNumber,
                PartnerCustomerName = partnerDetails.CustomerName,
                PartnerCustomerPhone = partnerDetails.CustomerPhone,
                PartnerPaymentMethod = partnerDetails.PaymentMethod,
                PartnerPickupTime = partnerDetails.PickupTime,
                PartnerDeliveryFee = partnerDetails.DeliveryFee,
                PartnerServiceFee = partnerDetails.ServiceFee,
                ActualDeliveryCost = partnerDetails.ActualDeliveryCost ?? 0m,
                HasPriceDifference = priceDifference.HasFlag,
                PriceDifferenceCorrectionMode = priceDifference.CorrectionMode,
                OriginalPartnerTotal = priceDifference.CorrectionMode == CorrectionModeOrderTotal ? totals.Total + additionalFees : null,
                CorrectPartnerTotal = priceDifference.CorrectPartnerTotal,
                TotalDifferenceAmount = priceDifference.CorrectionMode == CorrectionModeOrderTotal
                    ? OrderPaymentHelper.RoundCurrency(priceDifference.CorrectPartnerTotal!.Value - (totals.Total + additionalFees))
                    : null,

                CustomerId = customer?.Id,
                CustomerPhone = normalizedCustomerPhone,
                CustomerName = orderSource == OrderSource.Online ? input.CustomerName?.Trim() : null,
                CustomerEmail = orderSource == OrderSource.Online ? input.CustomerEmail?.Trim() : null,
                PaymentMethod = TrimToNull(input.PaymentMethod),
                SubmittedPaymentReference = TrimToNull(input.PaymentReferenceNumber),
                WaiterId = caller.userId,

                // 3. Map Items
                OrderItems = input.Items
                    .Where(i => products.ContainsKey(i.ProductId))
                    .Select(i =>
                    {
                        var resolvedProduct = products[i.ProductId];
                        var resolvedModifiers = ResolveOrderItemModifiers(resolvedProduct, i.Modifiers, tenantId);
                        var selectedOption = i.SelectedOptionId.HasValue
                            ? resolvedProduct.Options.FirstOrDefault(o => o.Id == i.SelectedOptionId.Value)
                            : null;
                        var resolvedRecipeSnapshot = BuildRecipeSnapshot(resolvedProduct, resolvedModifiers, i.Quantity, tenantId, selectedOption, i.SelectedRecipeAlternatives);
                        var partnerOverride = partnerPricing.PriceRules.GetValueOrDefault(i.ProductId);
                        var baseUnitPrice = ResolveOrderItemUnitPrice(
                            resolvedProduct,
                            resolvedModifiers,
                            i.SelectedOptionId,
                            i.SelectedRecipeAlternatives,
                            ResolveOfferUnitPrice(i, offerUnitPrices),
                            partnerOverride);
                        var unitPrice = ResolveCreateOrderUnitPrice(i, baseUnitPrice, priceDifference);
                        var partnerPrice = ResolvePartnerPriceSnapshot(resolvedProduct, selectedOption, partnerOverride);
                        var lineTotal = CalculateLineTotalAmount(unitPrice, resolvedModifiers, i.Quantity, i.IsComplimentary);
                        var hasCorrectedPrice = priceDifference.HasFlag &&
                            priceDifference.CorrectionMode == CorrectionModePerItem &&
                            i.PartnerCorrectedUnitPrice.HasValue;

                        return new OrderItem
                        {
                            Id = Guid.NewGuid(),
                            OrderId = orderId,
                            TenantId = tenantId,
                            BranchId = branchId,
                            ProductId = i.ProductId,
                            ProductName = i.ProductName,
                            Quantity = i.Quantity,
                            Price = unitPrice,
                            UnitPriceSnapshot = unitPrice,
                            LineTotalSnapshot = lineTotal,
                            PartnerPriceSnapshot = partnerPrice,
                            HasPartnerDiscountOverride = hasCorrectedPrice,
                            PartnerOriginalUnitPrice = hasCorrectedPrice ? baseUnitPrice : null,
                            PartnerDiscountedUnitPrice = hasCorrectedPrice ? unitPrice : null,
                            PartnerDiscountReason = hasCorrectedPrice ? priceDifference.Reason : null,
                            PartnerDiscountUpdatedAt = hasCorrectedPrice ? createdAt : null,
                            PartnerDiscountUpdatedBy = hasCorrectedPrice ? caller.userId : null,
                            Notes = i.Notes,
                            IsComplimentary = i.IsComplimentary,
                            IsReady = false,
                            OfferLineId = i.OfferLineId,
                            OfferLineName = i.OfferLineName,
                            OfferLineNameAr = i.OfferLineNameAr,
                            SelectedOptionId = selectedOption?.Id,
                            SelectedOptionName = selectedOption?.Name,
                            SelectedOptionNameAr = selectedOption?.NameAr,
                            Modifiers = resolvedModifiers,
                            RecipeSnapshotItems = resolvedRecipeSnapshot
                        };
                    }).ToList()
            };
            DeliveryAccountingHelper.Apply(order);

            if (priceDifference.CorrectionMode == CorrectionModeOrderTotal)
                order.TotalAmount = priceDifference.CorrectPartnerTotal!.Value;

            // 4. Save Order to DB
            _context.Orders.Add(order);
            AddCreatePriceDifferenceAudits(order, priceDifference, caller.userId, createdAt);
            await _context.SaveChangesAsync(HttpContext.RequestAborted);

            // Update Table Status: Occupied if was Free, keep Occupied/Reserved if already in session
            if (table != null && table.Status == TableStatus.Free)
            {
                table.Status = TableStatus.Occupied;
                _context.Tables.Update(table);
                await _context.SaveChangesAsync(HttpContext.RequestAborted);
            }

            // Partner credit (prepaid) orders are already settled by the partner platform
            // (Talabat/Jahez/HungerStation Credit). Mark them Paid at creation — inside the
            // same transaction — so they surface in orders/kitchen/dashboards without a
            // separate cashier payment. Cash partner orders keep the payment-first flow.
            var settledByPartner = orderSource == OrderSource.DeliveryPartner
                && !OrderPaymentHelper.IsCashMethod(partnerDetails.PaymentMethod);
            if (settledByPartner)
            {
                SettleAsPartnerPrepaid(
                    order,
                    totals,
                    additionalFees,
                    isOfflineReplay,
                    isOfflineReplay ? input.OfflinePaidAt : null);
                await _context.SaveChangesAsync(HttpContext.RequestAborted);
            }

            // Commit transaction to ensure atomicity
            await transaction.CommitAsync(HttpContext.RequestAborted);

            // The order is durably persisted past this point. Cache
            // invalidation is a best-effort side effect — a failure here must
            // never surface as an HTTP 500 on an order the customer already
            // owns (the client would treat the 500 as a failure and retry,
            // creating a duplicate). Log and continue.
            try
            {
                await InvalidateOrderCachesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Order {OrderId} persisted but cache invalidation failed", order.Id);
            }

            Offer? orderOffer = null;
            try
            {
                orderOffer = await LoadOfferForReceiptAsync(order.OfferId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Order {OrderId} persisted but offer receipt details could not be loaded", order.Id);
            }
            // Snapshot payment totals so PaidAmount/RemainingAmount/PaymentStatus are populated
            // consistently with GET endpoints — otherwise the client sees remaining = 0 on a new order.
            var paymentSnapshot = OrderPaymentHelper.BuildSnapshot(order);

            // 5. Response
            var responseDto = new OrderDto
            {
                Id = order.Id,
                OrderNumber = ResolveOrderNumber(order),
                DisplayOrderNumber = order.DisplayOrderNumber,
                SystemOrderNumber = order.OrderNumber,
                ClientOrderUuid = order.ClientOrderUuid,
                PublicOrderNumber = order.PublicOrderNumber,
                OrderType = order.OrderType.ToString(),
                OfferId = order.OfferId,
                OfferName = orderOffer?.Name,
                OfferNameAr = orderOffer?.NameAr,
                OfferNote = order.OfferNote,
                OfferProducts = MapOrderOfferProducts(orderOffer),
                TicketId = order.TicketId,
                TableName = ResolveDisplayName(table?.Name ?? order.TableName, table?.NameAr, IsArabicRequested(Request)),
                Subtotal = totals.Subtotal,
                DiscountPercentage = order.DiscountPercentage,
                DiscountAmount = totals.DiscountAmount,
                DiscountGroupId = order.DiscountGroupId,
                DiscountGroupName = order.DiscountGroupName,
                DiscountGroupType = order.DiscountGroupType,
                DiscountGroupValue = order.DiscountGroupValue,
                DiscountGroupAmount = totals.DiscountGroupAmount,
                ServiceChargeRate = order.ServiceChargeRate,
                ServiceChargeAmount = totals.ServiceChargeAmount,
                TaxRate = order.TaxRate,
                TaxAmount = totals.TaxAmount,
                TotalAmount = order.TotalAmount,
                FoodSubtotal = order.FoodSubtotal,
                CustomerDeliveryFee = order.CustomerDeliveryFee,
                ActualDeliveryCost = order.ActualDeliveryCost,
                DeliveryMargin = order.DeliveryMargin,
                MarketplaceDeliveryFee = order.MarketplaceDeliveryFee,
                MarketplaceServiceFee = order.MarketplaceServiceFee,
                NetRestaurantRevenue = order.NetRestaurantRevenue,
                CostSharingTotalCommission = order.CostSharingTotalCommission,
                CostSharingRestaurantShare = order.CostSharingRestaurantShare,
                CostSharingCounterpartyShare = order.CostSharingCounterpartyShare,
                CostSharingNetSettlement = order.CostSharingNetSettlement,
                CostSharingCalculatedAt = order.CostSharingCalculatedAt,
                OrderSource = order.OrderSource.ToString(),
                TalabatOrderNumber = order.TalabatOrderNumber,
                TalabatCustomerName = order.TalabatCustomerName,
                TalabatCustomerPhone = order.TalabatCustomerPhone,
                TalabatPaymentMethod = order.TalabatPaymentMethod,
                TalabatPickupTime = order.TalabatPickupTime,
                TalabatDeliveryFee = order.TalabatDeliveryFee,
                TalabatServiceFee = order.TalabatServiceFee,
                DeliveryPartnerId = order.DeliveryPartnerId,
                DeliveryPartnerName = order.DeliveryPartnerName,
                DeliveryPartnerNameAr = order.DeliveryPartnerNameAr,
                DeliveryPartnerCode = order.DeliveryPartnerCode,
                PartnerOrderNumber = order.PartnerOrderNumber,
                PartnerCustomerName = order.PartnerCustomerName,
                PartnerCustomerPhone = order.PartnerCustomerPhone,
                PartnerPaymentMethod = order.PartnerPaymentMethod,
                PartnerPickupTime = order.PartnerPickupTime,
                PartnerDeliveryFee = order.PartnerDeliveryFee,
                PartnerServiceFee = order.PartnerServiceFee,
                DeliveryZoneId = order.DeliveryZoneId,
                DeliveryZoneName = order.DeliveryZoneName,
                DeliveryFee = order.DeliveryFee,
                DeliveryCost = order.DeliveryCost,
                DeliveryPaymentMode = order.DeliveryPaymentMode != null ? order.DeliveryPaymentMode.ToString() : null,
                DeliveryAddress = order.DeliveryAddress,
                DeliveryNotes = order.DeliveryNotes,
                SubmittedPaymentReference = order.SubmittedPaymentReference,
                Status = order.Status.ToString(),
                IsPaid = paymentSnapshot.IsPaid,
                PaymentStatus = paymentSnapshot.PaymentStatus,
                PaymentMethod = paymentSnapshot.PaymentMethod,
                CreatedBy = order.WaiterId,
                PaidAmount = paymentSnapshot.PaidAmount,
                RemainingAmount = paymentSnapshot.RemainingAmount,
                CustomerId = order.CustomerId,
                CustomerName = order.OrderSource == OrderSource.Online ? order.CustomerName ?? customer?.Name : customer?.Name ?? order.PartnerCustomerName ?? order.TalabatCustomerName,
                CustomerNameAr = customer?.NameAr,
                CustomerPhone = order.CustomerPhone ?? customer?.PhoneNumber ?? order.PartnerCustomerPhone ?? order.TalabatCustomerPhone,
                CreatedAt = order.CreatedAt,
                LastUpdatedAt = order.LastUpdatedAt,
                DispatchedAt = order.DispatchedAt,
                SyncedAt = order.SyncedAt,
                ScheduledFor = order.ScheduledFor,
                Payments = paymentSnapshot.Payments,
                Items = CreateDisplayItemsWithOfferBundleStatic(order, orderOffer),
                HasPriceDifference = order.HasPriceDifference,
                PriceDifferenceCorrectionMode = order.PriceDifferenceCorrectionMode,
                OriginalPartnerTotal = order.OriginalPartnerTotal,
                CorrectPartnerTotal = order.CorrectPartnerTotal,
                TotalDifferenceAmount = order.TotalDifferenceAmount,
                HasPartnerDiscountOverride = order.HasPriceDifference ||
                    (order.OrderItems?.Any(i => i.HasPartnerDiscountOverride) ?? false)
            };

            // 6. Notify Kitchen (Real-time).
            // Takeaway/Delivery orders follow a payment-first flow: the kitchen is notified at checkout,
            // not at order creation, so items are only prepared after payment is confirmed.
            //
            // The whole notification block is post-commit and purely a side
            // effect: the order is already saved. An awaited SignalR SendAsync
            // (KitchenHub) or any downstream publish that throws here would
            // otherwise return HTTP 500 for an order that was created
            // successfully — the exact partner-order symptom (created + visible
            // in the list, yet 500). Guard so the response always reflects the
            // committed order; the kitchen feed self-heals on its next refresh.
            try
            {
                if (!IsPaymentFirstOrder(order))
                {
                    await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.ReceiveNewOrder, responseDto);

                    // 7. Persist notification → Kitchen only
                    var channelLabel = ResolveOrderChannelLabel(order);
                    _ = _notificationService.SendAsync(
                        NotificationType.NewOrder,
                        $"New Order #{order.OrderNumber}",
                        $"{channelLabel} — {order.TableName}",
                        order.Id.ToString(),
                        "Kitchen");

                    // 8. Direct-print pipeline (per-kitchen tickets + counter receipt).
                    // Fire-and-forget: the handler creates its own DI scope, so it
                    // survives this request's scope being disposed. Awaiting here
                    // would add ticket-build + INSERT latency to the order response,
                    // breaking the "no order delay" rule.
                    // Cashier id = order creator (WaiterId is stamped from the
                    // calling identity for both waiter-style and cashier-style
                    // POS flows). PrintDispatcher resolves their assigned receipt
                    // printer with default-fallback chain.
                    _ = _mediator.Publish(new OrderCreatedForPrintingEvent(order.Id, order.TenantId, order.WaiterId));
                }
                else if (settledByPartner)
                {
                    // Settled-at-creation partner orders are payment-first but already Paid, so the
                    // creation-time notification was skipped above. Fire the same kitchen + paid
                    // side-effects the payment flow would have produced.
                    await PublishPartnerPrepaidPaidAsync(order, responseDto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Order {OrderId} persisted but post-commit kitchen/notification side effects failed", order.Id);
            }

            return CreatedAtAction(nameof(CreateOrder), new { id = order.Id }, responseDto);
        }

        private void LogCreateOrderRequest(OrderCreateDto input)
        {
            _logger.LogInformation(
                "Incoming order create request: {@OrderCreateRequest}",
                new
                {
                    input.TenantId,
                    input.TableId,
                    input.TableName,
                    input.OrderType,
                    input.OrderSource,
                    input.ClientOrderUuid,
                    input.PublicOrderNumber,
                    input.DeliveryZoneId,
                    input.DeliveryPartnerId,
                    ItemCount = input.Items?.Count ?? 0
                });
        }

        private void SettleAsPartnerPrepaid(
            Order order,
            OrderTotals totals,
            decimal additionalFees,
            bool preserveOfflineTotal,
            DateTime? offlinePaidAt)
        {
            var fullTotal = preserveOfflineTotal
                ? order.TotalAmount
                : OrderPaymentHelper.RoundCurrency(
                    totals.Total + totals.ServiceChargeAmount + totals.TaxAmount + additionalFees);

            order.TotalAmount = fullTotal;
            order.Status = OrderStatus.Paid;
            var paidAt = NormalizeUtcTimestamp(offlinePaidAt);
            order.PaidAt = paidAt;
            order.PaidByUserId = order.WaiterId;
            order.PaymentMethod = order.PartnerPaymentMethod;
            order.SyncedAt = DateTime.UtcNow;

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                TenantId = order.TenantId,
                BranchId = order.BranchId,
                OrderId = order.Id,
                Amount = fullTotal,
                Method = order.PartnerPaymentMethod!,
                ReferenceNumber = order.PartnerOrderNumber,
                CreatedAt = paidAt,
                CreatedByUserId = order.WaiterId
            };

            order.Payments = new List<Payment> { payment };
            _context.Payments.Add(payment);
        }

        private static DateTime NormalizeUtcTimestamp(DateTime? timestamp)
        {
            if (!timestamp.HasValue)
                return DateTime.UtcNow;

            return timestamp.Value.Kind switch
            {
                DateTimeKind.Utc => timestamp.Value,
                DateTimeKind.Local => timestamp.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(timestamp.Value, DateTimeKind.Utc)
            };
        }

        private async Task PublishPartnerPrepaidPaidAsync(Order order, OrderDto responseDto)
        {
            await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.ReceiveNewOrder, responseDto);

            var channelLabel = ResolveOrderChannelLabel(order);
            _ = _notificationService.SendAsync(
                NotificationType.NewOrder,
                $"New Order #{order.OrderNumber}",
                $"{channelLabel} — {order.TableName}",
                order.Id.ToString(),
                "Kitchen");

            _ = _mediator.Publish(new OrderPaidEvent(order.Id, order.TotalAmount, order.PaymentMethod!, order.PaidByUserId));
            _ = _mediator.Publish(new OrderCreatedForPrintingEvent(order.Id, order.TenantId, order.WaiterId));
        }

        private sealed record DeliverySnapshot(
            Guid? ZoneId, string? ZoneName, decimal Fee, decimal? Cost,
            DeliveryPaymentMode? PaymentMode, string? Address, string? Notes, bool IsDelivery, string? Error);

        private sealed record TalabatSnapshot(
            string? OrderNumber, string? CustomerName, string? CustomerPhone,
            string? PaymentMethod, DateTime? PickupTime, decimal? DeliveryFee, decimal? ServiceFee);

        private sealed record PartnerSnapshot(
            Guid? PartnerId,
            string? PartnerName,
            string? PartnerNameAr,
            string? PartnerCode,
            string? OrderNumber,
            string? CustomerName,
            string? CustomerPhone,
            string? PaymentMethod,
            DateTime? PickupTime,
            decimal? DeliveryFee,
            decimal? ServiceFee,
            decimal? ActualDeliveryCost,
            string? Error);

        private sealed record PartnerPricingContext(
            Dictionary<Guid, PartnerPricingRule> PriceRules,
            List<string> UnavailableProductNames)
        {
            public static PartnerPricingContext Empty { get; } = new(new Dictionary<Guid, PartnerPricingRule>(), new List<string>());
        }

        private sealed record PartnerPricingRule(
            DeliveryPartnerPricingRuleType Rule,
            decimal Value,
            decimal? CustomPrice)
        {
            public decimal Apply(decimal basePrice) => DeliveryPartnerPricingHelper.CalculatePrice(basePrice, Rule, Value, CustomPrice);
        }

        private sealed record PriceDifferenceCreateValues(
            bool HasFlag,
            string? ReasonCode,
            string? Reason,
            string? Note,
            string? CorrectionMode,
            decimal? CorrectPartnerTotal,
            string? Error)
        {
            public static PriceDifferenceCreateValues Empty { get; } = new(false, null, null, null, null, null, null);

            public static PriceDifferenceCreateValues Invalid(string error) => new(false, null, null, null, null, null, error);
        }

        private static TalabatSnapshot NormalizeTalabatDetails(OrderCreateDto input, OrderSource orderSource)
        {
            if (orderSource != OrderSource.Talabat)
                return new TalabatSnapshot(null, null, null, null, null, null, null);

            return new TalabatSnapshot(
                TrimToNull(input.TalabatOrderNumber),
                TrimToNull(input.TalabatCustomerName),
                NormalizeCustomerPhone(input.TalabatCustomerPhone),
                TrimToNull(input.TalabatPaymentMethod),
                NormalizeTalabatPickupTime(input.TalabatPickupTime),
                OrderPaymentHelper.RoundCurrency(input.TalabatDeliveryFee ?? 0m),
                OrderPaymentHelper.RoundCurrency(input.TalabatServiceFee ?? 0m));
        }

        private async Task<PartnerSnapshot> ResolvePartnerDetailsAsync(
            OrderCreateDto input,
            OrderSource orderSource,
            CancellationToken ct)
        {
            if (orderSource != OrderSource.DeliveryPartner)
                return new PartnerSnapshot(null, null, null, null, null, null, null, null, null, null, null, null, null);

            if (!input.DeliveryPartnerId.HasValue || input.DeliveryPartnerId.Value == Guid.Empty)
                return new PartnerSnapshot(null, null, null, null, null, null, null, null, null, null, null, null, "Delivery partner is required.");

            var partner = await _context.DeliveryPartners
                .AsNoTracking()
                .Where(p => p.Id == input.DeliveryPartnerId.Value && p.Status == DeliveryPartnerStatus.Active)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.NameAr,
                    p.Code
                })
                .FirstOrDefaultAsync(ct);

            if (partner == null)
                return new PartnerSnapshot(null, null, null, null, null, null, null, null, null, null, null, null, "Selected delivery partner is invalid or inactive.");

            // P2 (offline financial preservation): partner-side fee + cost
            // fields are historically zeroed out at this layer. For offline
            // replays the cashier-captured values are the only source of
            // truth (the receipt was printed using them), so when an OFF#
            // PublicOrderNumber is present we promote the DTO values
            // instead of dropping them. Online behavior is unchanged.
            var isOfflineReplay = !string.IsNullOrWhiteSpace(input.PublicOrderNumber);
            var partnerDeliveryFee = isOfflineReplay
                ? OrderPaymentHelper.RoundCurrency(Math.Max(0m, input.PartnerDeliveryFee ?? 0m))
                : 0m;
            var partnerServiceFee = isOfflineReplay
                ? OrderPaymentHelper.RoundCurrency(Math.Max(0m, input.PartnerServiceFee ?? 0m))
                : 0m;
            var actualDeliveryCost = isOfflineReplay
                ? OrderPaymentHelper.RoundCurrency(Math.Max(0m, input.ActualDeliveryCost ?? 0m))
                : 0m;

            return new PartnerSnapshot(
                partner.Id,
                partner.Name,
                partner.NameAr,
                partner.Code,
                TrimToNull(input.PartnerOrderNumber),
                TrimToNull(input.PartnerCustomerName),
                NormalizeCustomerPhone(input.PartnerCustomerPhone),
                TrimToNull(input.PartnerPaymentMethod),
                NormalizeTalabatPickupTime(input.PartnerPickupTime),
                partnerDeliveryFee,
                partnerServiceFee,
                actualDeliveryCost,
                null);
        }

        private async Task<PartnerPricingContext> BuildPartnerPricingContextAsync(
            OrderSource orderSource,
            Guid? partnerId,
            IEnumerable<Product> products,
            CancellationToken ct)
        {
            if (orderSource != OrderSource.DeliveryPartner || !partnerId.HasValue)
                return PartnerPricingContext.Empty;

            var productList = products.ToList();
            if (productList.Count == 0)
                return PartnerPricingContext.Empty;

            // Branch platform: the partner must be enabled for the active branch.
            // Without this, only the per-partner product mapping gated intake and a
            // branch-disabled partner could still receive orders in that branch.
            var currentBranchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
            var enabledPartnerIds = await _branchConfigurationService.GetEnabledDeliveryPartnerIdsAsync(currentBranchId, ct);
            if (!enabledPartnerIds.Contains(partnerId.Value))
                throw new RestaurantPos.Api.Exceptions.ValidationException(
                    "The selected delivery partner is not enabled for the current branch.");

            var partner = await _context.DeliveryPartners
                .AsNoTracking()
                .Where(p => p.Id == partnerId.Value && p.Status == DeliveryPartnerStatus.Active)
                .Select(p => new { p.DefaultPricingRuleType, p.DefaultPricingRuleValue })
                .FirstOrDefaultAsync(ct);

            if (partner == null)
                return new PartnerPricingContext(new Dictionary<Guid, PartnerPricingRule>(), productList.Select(p => p.Name).ToList());

            var productIds = productList.Select(p => p.Id).ToList();
            var mappings = await _context.DeliveryPartnerProducts
                .AsNoTracking()
                .Where(m => m.DeliveryPartnerId == partnerId.Value && productIds.Contains(m.ProductId))
                .Select(m => new
                {
                    m.ProductId,
                    m.IsEnabled,
                    m.IsAvailable,
                    m.PricingRuleType,
                    m.PricingRuleValue,
                    m.CustomPrice,
                    m.AvailableStartDate,
                    m.AvailableEndDate,
                    m.AvailableFrom,
                    m.AvailableTo
                })
                .ToDictionaryAsync(m => m.ProductId, ct);

            var unavailable = new List<string>();
            var rules = new Dictionary<Guid, PartnerPricingRule>();
            foreach (var product in productList)
            {
                mappings.TryGetValue(product.Id, out var mapping);
                if (mapping == null ||
                    !mapping.IsEnabled ||
                    !mapping.IsAvailable ||
                    !AvailabilityHelper.IsAvailableNow(
                        mapping.AvailableStartDate,
                        mapping.AvailableEndDate,
                        mapping.AvailableFrom,
                        mapping.AvailableTo))
                {
                    unavailable.Add(product.Name);
                    continue;
                }

                var rule = mapping?.PricingRuleType ?? partner.DefaultPricingRuleType;
                var value = mapping?.PricingRuleType.HasValue == true
                    ? mapping.PricingRuleValue ?? 0m
                    : partner.DefaultPricingRuleValue;
                rules[product.Id] = new PartnerPricingRule(
                    rule,
                    value,
                    rule == DeliveryPartnerPricingRuleType.CustomPrice ? mapping?.CustomPrice : null);
            }

            return new PartnerPricingContext(rules, unavailable);
        }

        private static decimal GetAdditionalFeeTotal(
            DeliverySnapshot deliveryInfo,
            TalabatSnapshot talabatDetails,
            PartnerSnapshot partnerDetails)
            => deliveryInfo.Fee
                + (talabatDetails.DeliveryFee ?? 0m)
                + (talabatDetails.ServiceFee ?? 0m);

        private static decimal GetAdditionalFeeTotal(Order order)
            => (order.DeliveryFee ?? 0m)
                + (order.TalabatDeliveryFee ?? 0m)
                + (order.TalabatServiceFee ?? 0m);

        /// <summary>
        /// Resolves the chargeable delivery snapshot for a new order. Non-delivery orders return an
        /// empty snapshot (fee 0). Fee/cost/name/mode come from the active zone of <paramref name="branchId"/>
        /// (cached); the cashier cannot set them. A manual fee override is honored only for Admin/Manager.
        /// </summary>
        private async Task<DeliverySnapshot> ResolveDeliveryForCreateAsync(
            OrderCreateDto input,
            Guid branchId,
            CancellationToken ct)
        {
            if ((OrderType)input.OrderType != OrderType.Delivery)
                return new DeliverySnapshot(null, null, 0m, null, null, null, null, false, null);

            var address = string.IsNullOrWhiteSpace(input.DeliveryAddress) ? null : input.DeliveryAddress.Trim();
            var notes = string.IsNullOrWhiteSpace(input.DeliveryNotes) ? null : input.DeliveryNotes.Trim();
            Guid? zoneId = null;
            string? zoneName = null;
            decimal? cost = null;
            DeliveryPaymentMode? mode = null;
            decimal fee = 0m;

            if (!input.DeliveryZoneId.HasValue)
                return new DeliverySnapshot(null, null, 0m, null, null, address, notes, true, "Delivery zone is required for delivery orders.");

            // Resolved against the branch this order is being created for, which the caller has
            // already established. A public storefront order has no session to derive a branch
            // from, and asking for one here would 401 every delivery a shopper places.
            var zone = await _deliveryZoneService.ResolveActiveForBranchAsync(
                input.DeliveryZoneId.Value, branchId, ct);
            if (zone == null)
                return new DeliverySnapshot(null, null, 0m, null, null, address, notes, true, "Selected delivery zone is invalid or inactive.");

            zoneId = zone.Id;
            zoneName = zone.NameEn;
            cost = zone.DeliveryCost;
            mode = zone.PaymentMode;
            fee = zone.DeliveryFee;

            if (input.DeliveryFeeOverride.HasValue)
            {
                if (!User.IsInRole(AppRoleNames.Admin) && !User.IsInRole(AppRoleNames.Manager))
                    return new DeliverySnapshot(zoneId, zoneName, fee, cost, mode, address, notes, true, "Only Admin or Manager can override the delivery fee.");

                fee = input.DeliveryFeeOverride.Value < 0 ? 0m : input.DeliveryFeeOverride.Value;
            }
            else if (!string.IsNullOrWhiteSpace(input.PublicOrderNumber))
            {
                // P4 (production fix): Offline orders carry the fee/cost
                // snapshot taken at the moment the cashier wrote the
                // receipt. When the queue replays the create-order action
                // minutes-or-hours later, admin may have changed the zone
                // pricing — recomputing here would silently move the
                // recorded total away from the cash the customer paid
                // against the printed receipt. Treat the DTO values as
                // historical truth whenever the order originated offline
                // (PublicOrderNumber is the OFF# stamped at creation).
                if (input.DeliveryFee.HasValue && input.DeliveryFee.Value >= 0m)
                    fee = input.DeliveryFee.Value;
                if (input.DeliveryCost.HasValue && input.DeliveryCost.Value >= 0m)
                    cost = input.DeliveryCost.Value;
            }

            return new DeliverySnapshot(zoneId, zoneName, fee, cost, mode, address, notes, true, null);
        }

        private async Task<Order?> FindExistingClientOrderAsync(Guid tenantId, string clientOrderUuid, CancellationToken ct)
        {
            return await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.PaidByUser)
                .Include(o => o.Customer)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.RecipeSnapshotItems)
                .Include(o => o.Payments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o =>
                    o.TenantId == tenantId &&
                    o.ClientOrderUuid == clientOrderUuid,
                    ct);
        }

        private async Task<string?> ReconcilePublicOrderNumberAsync(
            Order existingOrder,
            string? publicOrderNumber,
            CancellationToken ct)
        {
            if (publicOrderNumber == null)
                return null;

            if (await PublicOrderNumberBelongsToAnotherOrderAsync(existingOrder, publicOrderNumber, ct))
                return PublicOrderNumberExistsMessage;

            if (!string.IsNullOrWhiteSpace(existingOrder.PublicOrderNumber) &&
                existingOrder.PublicOrderNumber != publicOrderNumber)
            {
                return ClientOrderReplayMismatchMessage;
            }

            if (existingOrder.PublicOrderNumber == publicOrderNumber)
            {
                return null;
            }

            await StampPublicOrderNumberAsync(existingOrder, publicOrderNumber, ct);
            return null;
        }

        private async Task StampPublicOrderNumberAsync(Order order, string publicOrderNumber, CancellationToken ct)
        {
            order.PublicOrderNumber = publicOrderNumber;
            if (string.IsNullOrWhiteSpace(order.OrderNumber) ||
                string.Equals(order.OrderNumber, publicOrderNumber, StringComparison.Ordinal))
            {
                order.OrderNumber = GenerateOrderNumber(order.OrderType, order.Id, order.CreatedAt);
            }
            order.SyncedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            try
            {
                await InvalidateOrderCachesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Order {OrderId} replay was reconciled but cache invalidation failed",
                    order.Id);
            }
        }

        private Task<bool> PublicOrderNumberBelongsToAnotherOrderAsync(
            Order existingOrder,
            string publicOrderNumber,
            CancellationToken ct)
        {
            return _context.Orders.AnyAsync(o =>
                o.TenantId == existingOrder.TenantId &&
                o.PublicOrderNumber == publicOrderNumber &&
                o.Id != existingOrder.Id,
                ct);
        }

        private Task<bool> PublicOrderNumberExistsAsync(Guid tenantId, string publicOrderNumber, CancellationToken ct)
        {
            return _context.Orders.AnyAsync(o =>
                o.TenantId == tenantId &&
                o.PublicOrderNumber == publicOrderNumber,
                ct);
        }

        private Task<bool> TalabatOrderNumberExistsAsync(Guid tenantId, string talabatOrderNumber, CancellationToken ct)
        {
            return _context.Orders.AnyAsync(o =>
                o.TenantId == tenantId &&
                o.TalabatOrderNumber == talabatOrderNumber,
                ct);
        }

        private Task<bool> PartnerOrderNumberExistsAsync(
            Guid tenantId,
            Guid partnerId,
            string partnerOrderNumber,
            CancellationToken ct)
        {
            return _context.Orders.AnyAsync(o =>
                o.TenantId == tenantId &&
                o.DeliveryPartnerId == partnerId &&
                o.PartnerOrderNumber == partnerOrderNumber,
                ct);
        }

        private static string ResolveOrderTableName(
            OrderSource orderSource,
            string? tableName,
            PartnerSnapshot partnerDetails)
        {
            if (orderSource == OrderSource.Talabat)
                return TalabatTableName;

            if (orderSource == OrderSource.DeliveryPartner)
                return TrimToNull(partnerDetails.PartnerCode)?.ToUpperInvariant()
                    ?? TrimToNull(partnerDetails.PartnerName)
                    ?? DeliveryPartnerTableName;

            return tableName ?? string.Empty;
        }

        private static string GenerateOrderNumber(OrderType orderType, Guid orderId, DateTime createdAt)
        {
            var prefix = orderType switch
            {
                OrderType.Takeaway => "T-",
                OrderType.Delivery => "D-",
                _ => string.Empty
            };
            return $"{prefix}{createdAt.ToUniversalTime():yyyyMMddHHmmssfff}-{orderId:N}";
        }

        private static string? NormalizeClientOrderUuid(string? value)
            => NormalizeOptionalIdentifier(value, 64, "Client order UUID");

        private static string? NormalizePublicOrderNumber(string? value)
            => NormalizeOptionalIdentifier(value, 64, "Public order number");

        private static OrderSource ResolveOrderSource(string? value)
            => string.IsNullOrWhiteSpace(value)
                ? OrderSource.Pos
                : Enum.Parse<OrderSource>(value.Trim(), ignoreCase: true);

        private static bool TryResolveOrderSource(string? value, out OrderSource orderSource)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                orderSource = OrderSource.Pos;
                return true;
            }

            return Enum.TryParse(value.Trim(), ignoreCase: true, out orderSource);
        }

        private static string? TrimToNull(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static DateTime? NormalizeTalabatPickupTime(DateTime? value)
            => value.HasValue ? value.Value.ToUniversalTime() : null;

        private static string? NormalizeOptionalIdentifier(string? value, int maxLength, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim();
            if (normalized.Length > maxLength)
                throw new ValidationException($"{fieldName} is too long.");

            return normalized;
        }

        private async Task<ValidatePriceResponse> ValidatePriceInternalAsync(ValidatePriceRequest request)
        {
            if (request.Items == null || request.Items.Count == 0)
            {
                return new ValidatePriceResponse
                {
                    IsValid = false,
                    Message = "At least one item is required."
                };
            }

            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Include(p => p.RecipeItems)
                    .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(mg => mg.Modifiers)
                            .ThenInclude(m => m.RecipeItems)
                                .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(mg => mg.Modifiers)
                            .ThenInclude(m => m.LinkedProduct)
                                .ThenInclude(lp => lp!.RecipeItems)
                                    .ThenInclude(ri => ri.RawMaterial)
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var unavailableItems = new List<string>();
            decimal expectedTotal = 0;

            foreach (var item in request.Items)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                {
                    unavailableItems.Add(item.ProductId.ToString());
                    continue;
                }

                if (!AvailabilityHelper.IsAvailableNow(
                        product.AvailableStartDate,
                        product.AvailableEndDate,
                        product.AvailableFrom,
                        product.AvailableTo))
                {
                    unavailableItems.Add(product.Name);
                }

                if (!request.OfferId.HasValue)
                {
                    expectedTotal += (product.DiscountedPrice ?? product.BasePrice) * item.Quantity;
                }
            }

            if (request.OfferId.HasValue)
            {
                var localNow = await GetRestaurantLocalNowAsync();
                var offer = await _context.Offers
                    .Include(o => o.OfferProducts)
                    .FirstOrDefaultAsync(o => o.Id == request.OfferId.Value);
                var offerAvailable = offer != null
                    && offer.IsActive
                    && AvailabilityHelper.IsAvailableAt(
                        offer.StartDate,
                        offer.EndDate,
                        offer.StartTime,
                        offer.EndTime,
                        localNow);

                if (!offerAvailable)
                {
                    return new ValidatePriceResponse
                    {
                        IsValid = false,
                        ExpectedTotal = offer?.FinalPrice ?? 0,
                        SubmittedTotal = request.Items.Sum(item => item.UnitPrice * item.Quantity),
                        Discrepancy = Math.Abs((offer?.FinalPrice ?? 0) - request.Items.Sum(item => item.UnitPrice * item.Quantity)),
                        Message = "هذا العرض غير متاح حاليًا / Offer is not currently active",
                        UnavailableItems = unavailableItems
                    };
                }

                expectedTotal = offer!.FinalPrice * (request.OfferQty ?? 1);

                var coveredQuantities = offer.OfferProducts
                    .GroupBy(op => op.ProductId)
                    .ToDictionary(group => group.Key, group => group.Sum(op => op.Quantity) * (request.OfferQty ?? 1));

                foreach (var item in request.Items)
                {
                    if (!products.TryGetValue(item.ProductId, out var product))
                    {
                        continue;
                    }

                    var includedQuantity = 0;
                    if (coveredQuantities.TryGetValue(item.ProductId, out var remainingCoveredQuantity) && remainingCoveredQuantity > 0)
                    {
                        includedQuantity = Math.Min(remainingCoveredQuantity, item.Quantity);
                        coveredQuantities[item.ProductId] = remainingCoveredQuantity - includedQuantity;
                    }

                    var extraQuantity = item.Quantity - includedQuantity;
                    if (extraQuantity > 0)
                    {
                        expectedTotal += (product.DiscountedPrice ?? product.BasePrice) * extraQuantity;
                    }
                }
            }

            var submittedTotal = request.Items.Sum(item => item.UnitPrice * item.Quantity);
            var discrepancy = Math.Abs(expectedTotal - submittedTotal);
            var isValid = discrepancy <= 0.01m && unavailableItems.Count == 0;

            var message = string.Empty;
            if (unavailableItems.Count > 0)
            {
                message = $"Unavailable items: {string.Join(", ", unavailableItems)}";
            }
            else if (discrepancy > 0.01m)
            {
                message = $"Expected total {expectedTotal:F2}, submitted total {submittedTotal:F2}.";
            }

            return new ValidatePriceResponse
            {
                IsValid = isValid,
                ExpectedTotal = expectedTotal,
                SubmittedTotal = submittedTotal,
                Discrepancy = discrepancy,
                Message = message,
                UnavailableItems = unavailableItems
            };
        }

        // GET: api/Orders/table/{tableId}/active?ticketId={n}
        // `ticketId` scopes the result to a single session — when provided,
        // orders from prior sessions of the same table are dropped at the DB
        // level so the dine-in panel can never show a stale ticket.
        [HttpGet("table/{tableId}/active")]
        [AllowAnonymous]
        public async Task<ActionResult<List<OrderDto>>> GetActiveOrdersByTable(
            Guid tableId,
            [FromQuery] int? ticketId = null,
            CancellationToken ct = default)
        {
            var isArabic = IsArabicRequested(Request);
            var branchId = await GetRequestBranchIdAsync(_tenantResolver.GetTenantId(), ct);
            var query = _context.Orders
                .Include(o => o.Table)
                .Include(o => o.PaidByUser)
                .Include(o => o.Customer)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.RecipeSnapshotItems)
                .Include(o => o.Payments)
                .Where(o => o.TableId == tableId
                         && o.BranchId == branchId
                         && o.Status != OrderStatus.Paid
                         && o.Status != OrderStatus.Cancelled);

            if (ticketId.HasValue && ticketId.Value > 0)
                query = query.Where(o => o.TicketId == ticketId.Value);

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync(ct);

            var dtos = orders.Select(order => MapOrderToDto(order, isArabic)).ToList();
            await ApplyPendingCancellationStateAsync(dtos, ct);

            return Ok(dtos);
        }

        // GET: api/Orders/{id}
        [HttpGet("{id:guid}")]
        [Authorize]
        public async Task<ActionResult<OrderDto>> GetOrderById(Guid id, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.PaidByUser)
                .Include(o => o.Customer)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.PartnerDiscountUpdatedByUser)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.RecipeSnapshotItems)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct);

            if (order == null)
                return NotFound();

            var (userId, _, role) = GetCallerIdentity();
            if (userId == null)
                return Unauthorized();

            if (!CanViewOrder(order, userId.Value, role))
                return Forbid();

            var dto = MapOrderToDto(order, IsArabicRequested(Request));
            await ApplyPendingCancellationStateAsync(new List<OrderDto> { dto }, ct);

            return Ok(dto);
        }

        // GET: api/Orders/table/{tableId}/all?ticketId={n}
        // Returns all orders including paid ones (for table selection screen to
        // show reserved tables). When `ticketId` is supplied, scopes to a single
        // session so dine-in panels never display history from prior sessions.
        [HttpGet("table/{tableId}/all")]
        [AllowAnonymous]
        public async Task<ActionResult<List<OrderDto>>> GetAllOrdersByTable(
            Guid tableId,
            [FromQuery] int? ticketId = null,
            CancellationToken ct = default)
        {
            var isArabic = IsArabicRequested(Request);
            var branchId = await GetRequestBranchIdAsync(_tenantResolver.GetTenantId(), ct);
            var query = _context.Orders
                .Include(o => o.Table)
                .Include(o => o.PaidByUser)
                .Include(o => o.Customer)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.RecipeSnapshotItems)
                .Include(o => o.Payments)
                .Where(o => o.TableId == tableId
                         && o.BranchId == branchId
                         && o.Status != OrderStatus.Cancelled);

            if (ticketId.HasValue && ticketId.Value > 0)
                query = query.Where(o => o.TicketId == ticketId.Value);

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync(ct);

            var dtos = orders.Select(order => MapOrderToDto(order, isArabic)).ToList();
            await ApplyPendingCancellationStateAsync(dtos, ct);

            return Ok(dtos);
        }

        // POST: api/Orders/{orderId}/items
        [HttpPost("{orderId}/items")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<IActionResult> AddItemsToOrder(Guid orderId, [FromBody] List<OrderItemCreateDto> newItems)
        {
            if (newItems == null || newItems.Count == 0)
                return BadRequest("At least one item is required.");

            var branchId = await GetCurrentBranchIdAsync(HttpContext.RequestAborted);
            var orderHeader = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == orderId && o.BranchId == branchId)
                .Select(o => new
                {
                    o.Id,
                    o.TenantId,
                    o.BranchId,
                    o.OrderNumber,
                    o.TableName,
                    o.Status,
                    o.DiscountPercentage,
                    o.DiscountGroupType,
                    o.DiscountGroupValue,
                    o.ServiceChargeRate,
                    o.TaxRate,
                    o.DeliveryFee,
                    o.TalabatDeliveryFee,
                    o.TalabatServiceFee,
                    o.OrderSource,
                    o.DeliveryPartnerId
                })
                .FirstOrDefaultAsync();

            if (orderHeader == null)
                return NotFound();

            if (orderHeader.Status == OrderStatus.Paid || orderHeader.Status == OrderStatus.Cancelled || await HasRecordedPaymentsAsync(orderId))
                return BadRequest("Cannot add items to a closed order.");

            var productIds = newItems.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Include(p => p.RecipeItems)
                    .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.RecipeItems)
                    .ThenInclude(ri => ri.Alternatives)
                        .ThenInclude(a => a.RawMaterial)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(mg => mg.Modifiers)
                            .ThenInclude(m => m.RecipeItems)
                                .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(mg => mg.Modifiers)
                            .ThenInclude(m => m.LinkedProduct)
                                .ThenInclude(lp => lp!.RecipeItems)
                                    .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.Options)
                    .ThenInclude(o => o.RecipeItems)
                        .ThenInclude(ri => ri.RawMaterial)
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var offerUnitPrices = await BuildOfferUnitPriceOverridesAsync(newItems, null, null, products);
            var partnerPricing = await BuildPartnerPricingContextAsync(
                orderHeader.OrderSource,
                orderHeader.DeliveryPartnerId,
                products.Values,
                HttpContext.RequestAborted);
            if (partnerPricing.UnavailableProductNames.Count > 0)
            {
                return BadRequest($"Products unavailable for selected delivery partner: {string.Join(", ", partnerPricing.UnavailableProductNames)}.");
            }

            var addItemsStockValidationError = ValidateInventoryAvailability(
                newItems
                    .Where(i => products.ContainsKey(i.ProductId))
                    .Select(i => (products[i.ProductId], i.Quantity, i.Modifiers, i.SelectedOptionId, i.SelectedRecipeAlternatives))
                    .ToList());
            if (addItemsStockValidationError != null)
            {
                return BadRequest(addItemsStockValidationError);
            }

            // Load existing non-ready items (tracked) to enable delta quantity merging
            var existingNonReadyItems = await _context.OrderItems
                .Include(oi => oi.Modifiers)
                .Where(oi => oi.OrderId == orderId && !oi.IsReady)
                .ToListAsync();

            decimal additionalTotal = 0;
            var newItemsToCreate = new List<OrderItem>();

            foreach (var item in newItems)
            {
                var resolvedProduct = products[item.ProductId];
                var resolvedModifiers = ResolveOrderItemModifiers(resolvedProduct, item.Modifiers, orderHeader.TenantId);
                var selectedOption = item.SelectedOptionId.HasValue
                    ? resolvedProduct.Options.FirstOrDefault(o => o.Id == item.SelectedOptionId.Value)
                    : null;
                var resolvedRecipeSnapshot = BuildRecipeSnapshot(resolvedProduct, resolvedModifiers, item.Quantity, orderHeader.TenantId, selectedOption, item.SelectedRecipeAlternatives);
                var unitPrice = ResolveOrderItemUnitPrice(
                    resolvedProduct,
                    resolvedModifiers,
                    item.SelectedOptionId,
                    item.SelectedRecipeAlternatives,
                    ResolveOfferUnitPrice(item, offerUnitPrices),
                    partnerPricing.PriceRules.GetValueOrDefault(item.ProductId));
                var partnerPrice = ResolvePartnerPriceSnapshot(
                    resolvedProduct,
                    selectedOption,
                    partnerPricing.PriceRules.GetValueOrDefault(item.ProductId));
                var lineTotal = CalculateLineTotalAmount(unitPrice, resolvedModifiers, item.Quantity, item.IsComplimentary);

                // Delta merge: find existing non-ready item with same product, same modifier set, same option, and same notes
                // Note: items with different notes must NOT be merged — they are distinct by intention
                var incomingModNames = item.Modifiers
                    .Select(m => m.ModifierName.Trim().ToLower())
                    .OrderBy(s => s)
                    .ToList();

                var incomingNotes = (item.Notes ?? "").Trim().ToLower();

                var match = existingNonReadyItems.FirstOrDefault(e =>
                    e.ProductId == item.ProductId &&
                    e.SelectedOptionId == item.SelectedOptionId &&
                    Math.Abs(e.Price - unitPrice) <= 0.01m &&
                    (e.Notes ?? "").Trim().ToLower() == incomingNotes &&
                    e.OfferLineId == item.OfferLineId &&
                    e.Modifiers
                        .Select(m => m.ModifierName.Trim().ToLower())
                        .OrderBy(s => s)
                        .SequenceEqual(incomingModNames));

                if (match != null)
                {
                    // Increase quantity on existing row — EF change tracking will persist this
                    match.Quantity += item.Quantity;
                    match.LineTotalSnapshot = CalculateLineTotalAmount(match.Price, match.Modifiers, match.Quantity, match.IsComplimentary);
                }
                else
                {
                    newItemsToCreate.Add(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = orderHeader.Id,
                        TenantId = orderHeader.TenantId,
                        BranchId = orderHeader.BranchId,
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        Price = unitPrice,
                        UnitPriceSnapshot = unitPrice,
                        LineTotalSnapshot = lineTotal,
                        PartnerPriceSnapshot = partnerPrice,
                        Notes = item.Notes,
                        IsComplimentary = item.IsComplimentary,
                        IsReady = false,
                        IsNewlyAdded = true,
                        OfferLineId = item.OfferLineId,
                        OfferLineName = item.OfferLineName,
                        OfferLineNameAr = item.OfferLineNameAr,
                        SelectedOptionId = selectedOption?.Id,
                        SelectedOptionName = selectedOption?.Name,
                        SelectedOptionNameAr = selectedOption?.NameAr,
                        Modifiers = resolvedModifiers,
                        RecipeSnapshotItems = resolvedRecipeSnapshot
                    });
                }

                if (!item.IsComplimentary)
                {
                    var modifiersTotal = item.Modifiers.Sum(m => m.Price * Math.Max(1, m.Quantity)) * item.Quantity;
                    additionalTotal += (unitPrice * item.Quantity) + modifiersTotal;
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            if (newItemsToCreate.Any())
                _context.OrderItems.AddRange(newItemsToCreate);

            await _context.SaveChangesAsync(); // persists both new items and quantity updates

            // Full recalculation using all current items (respects discount, service, tax)
            var allCurrentItems = await _context.OrderItems
                .Include(oi => oi.Modifiers)
                .Where(oi => oi.OrderId == orderId)
                .AsNoTracking()
                .ToListAsync();

            var totals = CalculateOrderTotals(
                allCurrentItems,
                orderHeader.DiscountPercentage,
                orderHeader.DiscountGroupType,
                orderHeader.DiscountGroupValue,
                orderHeader.ServiceChargeRate,
                orderHeader.TaxRate);
            var additionalFees = (orderHeader.DeliveryFee ?? 0m)
                + (orderHeader.TalabatDeliveryFee ?? 0m)
                + (orderHeader.TalabatServiceFee ?? 0m);

            await _context.Orders
                .Where(o => o.Id == orderId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.TotalAmount, totals.Total + additionalFees)
                    .SetProperty(o => o.Subtotal, totals.Subtotal)
                    .SetProperty(o => o.DiscountAmount, totals.DiscountAmount)
                    .SetProperty(o => o.DiscountGroupAmount, totals.DiscountGroupAmount)
                    .SetProperty(o => o.ServiceChargeAmount, totals.ServiceChargeAmount)
                    .SetProperty(o => o.TaxAmount, totals.TaxAmount)
                    .SetProperty(o => o.Status, OrderStatus.Preparing)
                    .SetProperty(o => o.LastUpdatedAt, DateTime.UtcNow));

            await transaction.CommitAsync();
            await InvalidateOrderCachesAsync();

            var updatedOrder = await _context.Orders.AsNoTracking()
                .Include(o => o.Table)
                .Include(o => o.PaidByUser)
                .Include(o => o.Customer)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .FirstAsync(o => o.Id == orderId && o.BranchId == branchId);

            var responseDto = new OrderDto
            {
                Id = updatedOrder.Id,
                OrderNumber = ResolveOrderNumber(updatedOrder),
                DisplayOrderNumber = updatedOrder.DisplayOrderNumber,
                SystemOrderNumber = updatedOrder.OrderNumber,
                ClientOrderUuid = updatedOrder.ClientOrderUuid,
                PublicOrderNumber = updatedOrder.PublicOrderNumber,
                OrderType = updatedOrder.OrderType.ToString(),
                OrderSource = updatedOrder.OrderSource.ToString(),
                TalabatOrderNumber = updatedOrder.TalabatOrderNumber,
                TalabatCustomerName = updatedOrder.TalabatCustomerName,
                TalabatCustomerPhone = updatedOrder.TalabatCustomerPhone,
                TalabatPaymentMethod = updatedOrder.TalabatPaymentMethod,
                TalabatPickupTime = updatedOrder.TalabatPickupTime,
                TalabatDeliveryFee = updatedOrder.TalabatDeliveryFee,
                TalabatServiceFee = updatedOrder.TalabatServiceFee,
                DeliveryPartnerId = updatedOrder.DeliveryPartnerId,
                DeliveryPartnerName = updatedOrder.DeliveryPartnerName,
                DeliveryPartnerNameAr = updatedOrder.DeliveryPartnerNameAr,
                DeliveryPartnerCode = updatedOrder.DeliveryPartnerCode,
                PartnerOrderNumber = updatedOrder.PartnerOrderNumber,
                PartnerCustomerName = updatedOrder.PartnerCustomerName,
                PartnerCustomerPhone = updatedOrder.PartnerCustomerPhone,
                PartnerPaymentMethod = updatedOrder.PartnerPaymentMethod,
                PartnerPickupTime = updatedOrder.PartnerPickupTime,
                PartnerDeliveryFee = updatedOrder.PartnerDeliveryFee,
                PartnerServiceFee = updatedOrder.PartnerServiceFee,
                OfferId = updatedOrder.OfferId,
                OfferName = updatedOrder.Offer?.Name,
                OfferNameAr = updatedOrder.Offer?.NameAr,
                OfferNote = updatedOrder.OfferNote,
                OfferProducts = MapOrderOfferProducts(updatedOrder.Offer),
                TableName = ResolveTableDisplayName(updatedOrder, IsArabicRequested(Request)),
                Subtotal = totals.Subtotal,
                DiscountPercentage = updatedOrder.DiscountPercentage,
                DiscountAmount = totals.DiscountAmount,
                DiscountGroupId = updatedOrder.DiscountGroupId,
                DiscountGroupName = updatedOrder.DiscountGroupName,
                DiscountGroupType = updatedOrder.DiscountGroupType,
                DiscountGroupValue = updatedOrder.DiscountGroupValue,
                DiscountGroupAmount = totals.DiscountGroupAmount,
                ServiceChargeRate = updatedOrder.ServiceChargeRate,
                ServiceChargeAmount = totals.ServiceChargeAmount,
                TaxRate = updatedOrder.TaxRate,
                TaxAmount = totals.TaxAmount,
                TotalAmount = totals.Total + GetAdditionalFeeTotal(updatedOrder),
                DeliveryZoneId = updatedOrder.DeliveryZoneId,
                DeliveryZoneName = updatedOrder.DeliveryZoneName,
                DeliveryFee = updatedOrder.DeliveryFee,
                DeliveryCost = updatedOrder.DeliveryCost,
                DeliveryPaymentMode = updatedOrder.DeliveryPaymentMode != null ? updatedOrder.DeliveryPaymentMode.ToString() : null,
                DeliveryAddress = updatedOrder.DeliveryAddress,
                DeliveryNotes = updatedOrder.DeliveryNotes,
                SubmittedPaymentReference = updatedOrder.SubmittedPaymentReference,
                Status = updatedOrder.Status.ToString(),
                CashierName = ResolveUserDisplayName(updatedOrder.PaidByUser),
                CreatedBy = updatedOrder.WaiterId,
                CreatedAt = updatedOrder.CreatedAt,
                LastUpdatedAt = updatedOrder.LastUpdatedAt,
                DispatchedAt = updatedOrder.DispatchedAt,
                SyncedAt = updatedOrder.SyncedAt,
                Items = CreateDisplayItemsWithOfferBundleStatic(updatedOrder, updatedOrder.Offer)
            };

            // Existing broadcast — keeps current kitchen flow intact
            await BranchClients(updatedOrder.BranchId).SendAsync(KitchenHubEvents.ReceiveNewOrder, responseDto);

            // NEW: Item-level SignalR event with only the newly added items
            var addedItemDtos = newItemsToCreate.Select(ni => new
            {
                Id = ni.Id,
                ProductName = ni.ProductName,
                Quantity = ni.Quantity,
                Notes = ni.Notes
            }).ToList();

            if (addedItemDtos.Any())
            {
                await BranchClients(updatedOrder.BranchId).SendAsync(KitchenHubEvents.NewItemsAdded, new
                {
                    OrderId = updatedOrder.Id,
                    OrderNumber = updatedOrder.OrderNumber,
                    TableName = updatedOrder.TableName,
                    OrderType = updatedOrder.OrderType.ToString(),
                    NewItems = addedItemDtos
                });

                // Persist notification → Kitchen
                _ = _notificationService.SendAsync(
                    NotificationType.NewItemAdded,
                    $"New items added to Order #{updatedOrder.OrderNumber}",
                    $"{ResolveOrderChannelLabel(updatedOrder)} — {addedItemDtos.Count} new item(s)",
                    updatedOrder.Id.ToString(),
                    "Kitchen");

                // Direct-print: only the newly created OrderItem rows are routed.
                // Quantity-merged matches stay on existing tickets (already printed).
                // Fire-and-forget for the same latency reason as create flow.
                var newlyCreatedIds = newItemsToCreate.Select(ni => ni.Id).ToList();
                if (newlyCreatedIds.Count > 0)
                {
                    _ = _mediator.Publish(new OrderItemsAddedForPrintingEvent(
                        updatedOrder.Id,
                        updatedOrder.TenantId,
                        newlyCreatedIds));
                }
            }

            return Ok(responseDto);
        }

        // PUT: api/Orders/{orderId}/items/{orderItemId}
        [HttpPut("{orderId}/items/{orderItemId}")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<ActionResult<OrderDto>> UpdateOrderItem(Guid orderId, Guid orderItemId, [FromBody] OrderItemUpdateDto input)
        {
            if (input == null || input.Quantity <= 0)
                return BadRequest("Quantity must be greater than 0.");

            var branchId = await GetCurrentBranchIdAsync(HttpContext.RequestAborted);
            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.RecipeSnapshotItems)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId);

            if (order == null)
                return NotFound();

            if (order.Status == OrderStatus.Paid || order.Status == OrderStatus.Cancelled || await HasRecordedPaymentsAsync(order.Id))
                return BadRequest("Cannot edit items in a closed order.");

            var orderItem = order.OrderItems.FirstOrDefault(oi => oi.Id == orderItemId);
            if (orderItem == null)
                return NotFound("Order item not found.");

            if (orderItem.IsReady)
                return BadRequest("Cannot edit an item that is already ready.");

            var originalQuantity = orderItem.Quantity;

            var product = await _context.Products
                .Include(p => p.RecipeItems)
                    .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.RecipeItems)
                    .ThenInclude(ri => ri.Alternatives)
                        .ThenInclude(a => a.RawMaterial)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(mg => mg.Modifiers)
                            .ThenInclude(m => m.RecipeItems)
                                .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(mg => mg.Modifiers)
                            .ThenInclude(m => m.LinkedProduct)
                                .ThenInclude(lp => lp!.RecipeItems)
                                    .ThenInclude(ri => ri.RawMaterial)
                .Include(p => p.Options)
                    .ThenInclude(o => o.RecipeItems)
                        .ThenInclude(ri => ri.RawMaterial)
                .FirstOrDefaultAsync(p => p.Id == orderItem.ProductId);

            var selectedOption = (product != null && orderItem.SelectedOptionId.HasValue)
                ? product.Options.FirstOrDefault(o => o.Id == orderItem.SelectedOptionId.Value)
                : null;
            var partnerPricing = product != null
                ? await BuildPartnerPricingContextAsync(
                    order.OrderSource,
                    order.DeliveryPartnerId,
                    new[] { product },
                    HttpContext.RequestAborted)
                : PartnerPricingContext.Empty;
            if (partnerPricing.UnavailableProductNames.Count > 0)
            {
                return BadRequest($"Products unavailable for selected delivery partner: {string.Join(", ", partnerPricing.UnavailableProductNames)}.");
            }

            // Preserve the original alternative picks when the client doesn't send a new list
            // (older clients that don't know about alternatives should not silently drop the swap).
            var preservedAlternatives = (orderItem.RecipeSnapshotItems ?? Enumerable.Empty<OrderItemRecipeSnapshot>())
                .Where(s => s.SourceRecipeItemId.HasValue && s.SourceAlternativeId.HasValue)
                .Select(s => new SelectedRecipeAlternativeDto { RecipeItemId = s.SourceRecipeItemId!.Value, AlternativeId = s.SourceAlternativeId!.Value })
                .ToList();
            var effectiveAlternatives = input.SelectedRecipeAlternatives is { Count: > 0 }
                ? input.SelectedRecipeAlternatives
                : preservedAlternatives;

            var resolvedModifiers = product != null
                ? ResolveOrderItemModifiers(product, input.Modifiers, orderItem.TenantId)
                : input.Modifiers.Select(m => MapCreateModifier(m, orderItem.TenantId)).ToList();
            var resolvedPrice = product != null
                ? ResolveOrderItemUnitPrice(
                    product,
                    resolvedModifiers,
                    orderItem.SelectedOptionId,
                    effectiveAlternatives,
                    null,
                    partnerPricing.PriceRules.GetValueOrDefault(orderItem.ProductId))
                : orderItem.Price;
            var resolvedRecipeSnapshot = product != null
                ? BuildRecipeSnapshot(product, resolvedModifiers, input.Quantity, orderItem.TenantId, selectedOption, effectiveAlternatives)
                : new List<OrderItemRecipeSnapshot>();

            if (product != null)
            {
                var updateStockValidationError = ValidateInventoryAvailability(new List<(Product Product, int Quantity, List<OrderItemModifierCreateDto> Modifiers, Guid? SelectedOptionId, List<SelectedRecipeAlternativeDto> SelectedAlternatives)>
                {
                    (product, input.Quantity, input.Modifiers, orderItem.SelectedOptionId, effectiveAlternatives)
                });
                if (updateStockValidationError != null)
                {
                    return BadRequest(updateStockValidationError);
                }
            }

            orderItem.Quantity = input.Quantity;
            orderItem.Notes = input.Notes;
            orderItem.IsComplimentary = input.IsComplimentary;
            orderItem.Price = resolvedPrice;
            orderItem.UnitPriceSnapshot = resolvedPrice;
            orderItem.LineTotalSnapshot = CalculateLineTotalAmount(resolvedPrice, resolvedModifiers, input.Quantity, input.IsComplimentary);
            orderItem.PartnerPriceSnapshot = product != null
                ? ResolvePartnerPriceSnapshot(product, selectedOption, partnerPricing.PriceRules.GetValueOrDefault(orderItem.ProductId))
                : orderItem.PartnerPriceSnapshot;
            order.LastUpdatedAt = DateTime.UtcNow;
            order.SyncedAt = DateTime.UtcNow;

            _context.OrderItemModifiers.RemoveRange(orderItem.Modifiers);
            _context.OrderItemRecipeSnapshots.RemoveRange(orderItem.RecipeSnapshotItems ?? Enumerable.Empty<OrderItemRecipeSnapshot>());
            orderItem.Modifiers = resolvedModifiers;
            orderItem.RecipeSnapshotItems = resolvedRecipeSnapshot;

            // Full recalculation — single source of truth, respects discount + service + tax
            var recalcTotals = CalculateOrderTotals(
                order.OrderItems,
                order.DiscountPercentage,
                order.DiscountGroupType,
                order.DiscountGroupValue,
                order.ServiceChargeRate,
                order.TaxRate);
            order.TotalAmount = recalcTotals.Total + GetAdditionalFeeTotal(order);
            order.Subtotal = recalcTotals.Subtotal;
            order.DiscountAmount = recalcTotals.DiscountAmount;
            order.DiscountGroupAmount = recalcTotals.DiscountGroupAmount;
            order.ServiceChargeAmount = recalcTotals.ServiceChargeAmount;
            order.TaxAmount = recalcTotals.TaxAmount;
            DeliveryAccountingHelper.Apply(order);

            await _context.SaveChangesAsync();
            await InvalidateOrderCachesAsync();

            var isArabic = IsArabicRequested(Request);
            var responseDto = new OrderDto
            {
                Id = order.Id,
                OrderNumber = ResolveOrderNumber(order),
                DisplayOrderNumber = order.DisplayOrderNumber,
                SystemOrderNumber = order.OrderNumber,
                ClientOrderUuid = order.ClientOrderUuid,
                PublicOrderNumber = order.PublicOrderNumber,
                OrderType = order.OrderType.ToString(),
                OrderSource = order.OrderSource.ToString(),
                TalabatOrderNumber = order.TalabatOrderNumber,
                TalabatCustomerName = order.TalabatCustomerName,
                TalabatCustomerPhone = order.TalabatCustomerPhone,
                TalabatPaymentMethod = order.TalabatPaymentMethod,
                TalabatPickupTime = order.TalabatPickupTime,
                TalabatDeliveryFee = order.TalabatDeliveryFee,
                TalabatServiceFee = order.TalabatServiceFee,
                DeliveryPartnerId = order.DeliveryPartnerId,
                DeliveryPartnerName = order.DeliveryPartnerName,
                DeliveryPartnerNameAr = order.DeliveryPartnerNameAr,
                DeliveryPartnerCode = order.DeliveryPartnerCode,
                PartnerOrderNumber = order.PartnerOrderNumber,
                PartnerCustomerName = order.PartnerCustomerName,
                PartnerCustomerPhone = order.PartnerCustomerPhone,
                PartnerPaymentMethod = order.PartnerPaymentMethod,
                PartnerPickupTime = order.PartnerPickupTime,
                PartnerDeliveryFee = order.PartnerDeliveryFee,
                PartnerServiceFee = order.PartnerServiceFee,
                OfferId = order.OfferId,
                OfferName = order.Offer?.Name,
                OfferNameAr = order.Offer?.NameAr,
                OfferNote = order.OfferNote,
                OfferProducts = MapOrderOfferProducts(order.Offer),
                TableName = ResolveTableDisplayName(order, isArabic),
                Subtotal = recalcTotals.Subtotal,
                DiscountPercentage = order.DiscountPercentage,
                DiscountAmount = recalcTotals.DiscountAmount,
                DiscountGroupId = order.DiscountGroupId,
                DiscountGroupName = order.DiscountGroupName,
                DiscountGroupType = order.DiscountGroupType,
                DiscountGroupValue = order.DiscountGroupValue,
                DiscountGroupAmount = recalcTotals.DiscountGroupAmount,
                ServiceChargeRate = order.ServiceChargeRate,
                ServiceChargeAmount = recalcTotals.ServiceChargeAmount,
                TaxRate = order.TaxRate,
                TaxAmount = recalcTotals.TaxAmount,
                TotalAmount = recalcTotals.Total + GetAdditionalFeeTotal(order),
                DeliveryZoneId = order.DeliveryZoneId,
                DeliveryZoneName = order.DeliveryZoneName,
                DeliveryFee = order.DeliveryFee,
                DeliveryCost = order.DeliveryCost,
                DeliveryPaymentMode = order.DeliveryPaymentMode != null ? order.DeliveryPaymentMode.ToString() : null,
                DeliveryAddress = order.DeliveryAddress,
                DeliveryNotes = order.DeliveryNotes,
                SubmittedPaymentReference = order.SubmittedPaymentReference,
                Status = order.Status.ToString(),
                CreatedBy = order.WaiterId,
                CustomerPhone = order.CustomerPhone,
                CreatedAt = order.CreatedAt,
                LastUpdatedAt = order.LastUpdatedAt,
                DispatchedAt = order.DispatchedAt,
                SyncedAt = order.SyncedAt,
                Items = CreateDisplayItemsWithOfferBundleStatic(order, order.Offer)
            };

            // Only notify kitchen when quantity increases — notes/modifier changes are internal edits
            if (input.Quantity > originalQuantity)
            {
                await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.ReceiveNewOrder, responseDto);
            }
            return Ok(responseDto);
        }

        // GET: api/Orders/{orderId}/partner-discount/audits
        [HttpGet("{orderId:guid}/partner-discount/audits")]
        [Authorize(Roles = AppRoleGroups.DeliveryPartnerPriceAdjusters)]
        public async Task<ActionResult<List<PartnerPriceOverrideAuditDto>>> GetPartnerDiscountOverrideAudits(
            Guid orderId,
            CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var result = await _orderService.GetPartnerPriceOverrideAuditsAsync(
                orderId,
                _tenantResolver.GetTenantId(),
                branchId,
                ct);

            return Ok(result);
        }

        // PATCH: api/Orders/{orderId}/items/{orderItemId}/partner-discount
        [HttpPatch("{orderId:guid}/items/{orderItemId:guid}/partner-discount")]
        [Authorize(Roles = AppRoleGroups.PartnerDiscountOverrideManagers)]
        public async Task<ActionResult<PartnerDiscountOverrideDto>> UpdatePartnerDiscountOverride(
            Guid orderId,
            Guid orderItemId,
            [FromBody] PartnerDiscountOverrideRequest request,
            CancellationToken ct)
        {
            var (userId, _, _) = GetCallerIdentity();
            if (userId == null)
                return Unauthorized();

            var result = await _orderService.UpdatePartnerDiscountOverrideAsync(
                orderId,
                orderItemId,
                request,
                _tenantResolver.GetTenantId(),
                await GetCurrentBranchIdAsync(ct),
                userId.Value,
                ct);

            await InvalidateOrderCachesAsync();
            return Ok(result);
        }

        // PATCH: api/Orders/{orderId}/delivery-partner-price-adjustment
        [HttpPatch("{orderId:guid}/delivery-partner-price-adjustment")]
        [Authorize(Roles = AppRoleGroups.DeliveryPartnerPriceAdjusters)]
        public async Task<ActionResult<OrderDto>> UpdateDeliveryPartnerPriceAdjustment(
            Guid orderId,
            [FromBody] DeliveryPartnerPriceAdjustmentRequest request,
            CancellationToken ct)
        {
            var (userId, _, _) = GetCallerIdentity();
            if (userId == null)
                return Unauthorized();

            await _orderService.UpdateDeliveryPartnerPriceAdjustmentAsync(
                orderId,
                request,
                _tenantResolver.GetTenantId(),
                await GetCurrentBranchIdAsync(ct),
                userId.Value,
                ct);

            await InvalidateOrderCachesAsync();

            var order = await LoadOrderForResponseAsync(orderId, ct);
            if (order == null)
                return NotFound();

            var dto = MapOrderToDto(order, IsArabicRequested(Request));
            await ApplyPendingCancellationStateAsync(new List<OrderDto> { dto }, ct);
            await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.ReceiveNewOrder, dto, ct);

            return Ok(dto);
        }

        // PATCH: api/Orders/{orderId}/price-override
        [HttpPatch("{orderId:guid}/price-override")]
        [Authorize(Roles = AppRoleGroups.PartnerDiscountOverrideManagers)]
        public async Task<ActionResult<OrderTotalOverrideDto>> UpdateOrderTotalOverride(
            Guid orderId,
            [FromBody] OrderTotalOverrideRequest request,
            CancellationToken ct)
        {
            var (userId, _, _) = GetCallerIdentity();
            if (userId == null)
                return Unauthorized();

            var result = await _orderService.UpdateOrderTotalOverrideAsync(
                orderId,
                request,
                _tenantResolver.GetTenantId(),
                await GetCurrentBranchIdAsync(ct),
                userId.Value,
                ct);

            await InvalidateOrderCachesAsync();
            return Ok(result);
        }

        // PATCH: api/Orders/{orderId}/offer-note — Update the note attached to the offer on an order
        [HttpPatch("{orderId}/offer-note")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<IActionResult> UpdateOrderOfferNote(Guid orderId, [FromBody] UpdateOfferNoteRequest request)
        {
            var branchId = await GetCurrentBranchIdAsync(HttpContext.RequestAborted);
            var order = await _context.Orders
                .Include(o => o.Offer)
                    .ThenInclude(o => o!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId);

            if (order == null)
                return NotFound();

            if (order.Status == OrderStatus.Paid || order.Status == OrderStatus.Cancelled || await HasRecordedPaymentsAsync(order.Id))
                return BadRequest("Cannot update note on a closed order.");

            if (!order.OfferId.HasValue)
                return BadRequest("This order does not have an offer.");

            order.OfferNote = request.Note?.Trim();
            order.LastUpdatedAt = DateTime.UtcNow;
            order.SyncedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await InvalidateOrderCachesAsync();

            var responseDto = MapOrderToDto(order, IsArabicRequested(Request));
            await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.ReceiveNewOrder, responseDto);

            return Ok(responseDto);
        }

        // PATCH: api/Orders/{orderId}/offer — Set or update the offer on an existing order
        [HttpPatch("{orderId}/offer")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<IActionResult> SetOrderOffer(Guid orderId, [FromBody] SetOrderOfferRequest request)
        {
            var branchId = await GetCurrentBranchIdAsync(HttpContext.RequestAborted);
            var order = await _context.Orders
                .Include(o => o.Offer)
                    .ThenInclude(o => o!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId);

            if (order == null)
                return NotFound();

            if (order.Status == OrderStatus.Paid || order.Status == OrderStatus.Cancelled || await HasRecordedPaymentsAsync(order.Id))
                return BadRequest("Cannot modify a closed order.");

            if (request.OfferId.HasValue)
            {
                var offer = await _context.Offers
                    .Include(o => o.OfferProducts)
                    .FirstOrDefaultAsync(o => o.Id == request.OfferId.Value);

                if (offer == null)
                    return BadRequest("Offer not found.");

                order.OfferId = offer.Id;
                order.OfferNote = request.OfferNote?.Trim();
            }
            else
            {
                order.OfferId = null;
                order.OfferNote = null;
            }

            order.LastUpdatedAt = DateTime.UtcNow;
            order.SyncedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await InvalidateOrderCachesAsync();

            // Reload with full includes for the response
            var updatedOrder = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .FirstAsync(o => o.Id == orderId && o.BranchId == branchId);

            var responseDto = MapOrderToDto(updatedOrder, IsArabicRequested(Request));

            await BranchClients(updatedOrder.BranchId).SendAsync(KitchenHubEvents.ReceiveNewOrder, responseDto);

            return Ok(responseDto);
        }

        // DELETE: api/Orders/{orderId}/offer — Remove the offer bundle from an order
        [HttpDelete("{orderId}/offer")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<IActionResult> RemoveOfferFromOrder(Guid orderId)
        {
            var branchId = await GetCurrentBranchIdAsync(HttpContext.RequestAborted);
            var order = await _context.Orders
                .Include(o => o.Offer)
                    .ThenInclude(o => o!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId);

            if (order == null)
                return NotFound();

            if (order.Status == OrderStatus.Paid || order.Status == OrderStatus.Cancelled || await HasRecordedPaymentsAsync(order.Id))
                return BadRequest("Cannot modify a closed order.");

            if (!order.OfferId.HasValue)
                return BadRequest("This order does not have an offer.");

            // Collect product IDs belonging to the offer
            var offerProductIds = order.Offer?.OfferProducts?
                .Select(op => op.ProductId)
                .ToHashSet() ?? new HashSet<Guid>();

            // Find and remove offer items
            var offerItems = order.OrderItems
                .Where(oi => offerProductIds.Contains(oi.ProductId))
                .ToList();

            foreach (var item in offerItems)
            {
                _context.OrderItemModifiers.RemoveRange(item.Modifiers);
                _context.OrderItems.Remove(item);
            }

            // Clear offer reference and note
            order.OfferId = null;
            order.OfferNote = null;

            // Recalculate totals using remaining items
            var remainingItems = order.OrderItems
                .Where(oi => !offerProductIds.Contains(oi.ProductId))
                .ToList();

            var totals = CalculateOrderTotals(
                remainingItems,
                order.DiscountPercentage,
                order.DiscountGroupType,
                order.DiscountGroupValue,
                order.ServiceChargeRate,
                order.TaxRate);

            order.TotalAmount = totals.Total + GetAdditionalFeeTotal(order);
            order.Subtotal = totals.Subtotal;
            order.DiscountAmount = totals.DiscountAmount;
            order.DiscountGroupAmount = totals.DiscountGroupAmount;
            order.ServiceChargeAmount = totals.ServiceChargeAmount;
            order.TaxAmount = totals.TaxAmount;
            DeliveryAccountingHelper.Apply(order);
            order.LastUpdatedAt = DateTime.UtcNow;
            order.SyncedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await InvalidateOrderCachesAsync();

            // Reload to get clean state (offer is now null)
            var updatedOrder = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId);

            var responseDto = MapOrderToDto(updatedOrder!, IsArabicRequested(Request));

            // Notify kitchen of the change
            await BranchClients(updatedOrder!.BranchId).SendAsync(KitchenHubEvents.ReceiveNewOrder, responseDto);

            return Ok(responseDto);
        }

        // DELETE: api/Orders/{orderId}/offer-line/{offerLineId} — Remove one bundled offer line from an order
        [HttpDelete("{orderId}/offer-line/{offerLineId:int}")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<IActionResult> RemoveOfferLineFromOrder(Guid orderId, int offerLineId)
        {
            var branchId = await GetCurrentBranchIdAsync(HttpContext.RequestAborted);
            var order = await _context.Orders
                .Include(o => o.Offer)
                    .ThenInclude(o => o!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId);

            if (order == null)
                return NotFound();

            if (order.Status == OrderStatus.Paid || order.Status == OrderStatus.Cancelled || await HasRecordedPaymentsAsync(order.Id))
                return BadRequest("Cannot modify a closed order.");

            var offerLineItems = order.OrderItems
                .Where(oi => oi.OfferLineId == offerLineId)
                .ToList();

            if (offerLineItems.Count == 0)
                return BadRequest("Offer bundle not found on this order.");

            foreach (var item in offerLineItems)
            {
                _context.OrderItemModifiers.RemoveRange(item.Modifiers);
                _context.OrderItems.Remove(item);
            }

            var remainingItems = order.OrderItems
                .Where(oi => oi.OfferLineId != offerLineId)
                .ToList();

            var totals = CalculateOrderTotals(
                remainingItems,
                order.DiscountPercentage,
                order.DiscountGroupType,
                order.DiscountGroupValue,
                order.ServiceChargeRate,
                order.TaxRate);

            order.TotalAmount = totals.Total + GetAdditionalFeeTotal(order);
            order.Subtotal = totals.Subtotal;
            order.DiscountAmount = totals.DiscountAmount;
            order.DiscountGroupAmount = totals.DiscountGroupAmount;
            order.ServiceChargeAmount = totals.ServiceChargeAmount;
            order.TaxAmount = totals.TaxAmount;
            DeliveryAccountingHelper.Apply(order);
            order.LastUpdatedAt = DateTime.UtcNow;
            order.SyncedAt = DateTime.UtcNow;

            if (!remainingItems.Any(oi => oi.OfferLineId.HasValue))
            {
                order.OfferId = null;
                order.OfferNote = null;
            }

            await _context.SaveChangesAsync();
            await InvalidateOrderCachesAsync();

            var updatedOrder = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId);

            if (updatedOrder == null)
                return NotFound();

            var responseDto = MapOrderToDto(updatedOrder, IsArabicRequested(Request));

            await BranchClients(updatedOrder.BranchId).SendAsync(KitchenHubEvents.ReceiveNewOrder, responseDto);

            return Ok(responseDto);
        }

        // DELETE: api/Orders/{orderId}/items/{orderItemId}
        [HttpDelete("{orderId}/items/{orderItemId}")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<IActionResult> DeleteOrderItem(Guid orderId, Guid orderItemId)
        {
            var branchId = await GetCurrentBranchIdAsync(HttpContext.RequestAborted);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId);

            if (order == null)
                return NotFound();

            if (order.Status == OrderStatus.Paid || order.Status == OrderStatus.Cancelled || await HasRecordedPaymentsAsync(order.Id))
                return BadRequest("Cannot delete items from a closed order.");

            var orderItem = order.OrderItems.FirstOrDefault(oi => oi.Id == orderItemId);
            if (orderItem == null)
                return NotFound("Order item not found.");

            if (orderItem.IsReady)
                return BadRequest("Cannot delete an item that is already ready.");

            // Full recalculation after removing item — respects discount, service & tax
            var remainingItems = order.OrderItems.Where(oi => oi.Id != orderItemId).ToList();
            var deleteTotals = CalculateOrderTotals(
                remainingItems,
                order.DiscountPercentage,
                order.DiscountGroupType,
                order.DiscountGroupValue,
                order.ServiceChargeRate,
                order.TaxRate);
            order.TotalAmount = deleteTotals.Total + GetAdditionalFeeTotal(order);
            order.Subtotal = deleteTotals.Subtotal;
            order.DiscountAmount = deleteTotals.DiscountAmount;
            order.DiscountGroupAmount = deleteTotals.DiscountGroupAmount;
            order.ServiceChargeAmount = deleteTotals.ServiceChargeAmount;
            order.TaxAmount = deleteTotals.TaxAmount;
            DeliveryAccountingHelper.Apply(order);

            _context.OrderItemModifiers.RemoveRange(orderItem.Modifiers);
            _context.OrderItems.Remove(orderItem);
            order.LastUpdatedAt = DateTime.UtcNow;
            order.SyncedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await InvalidateOrderCachesAsync();
            return NoContent();
        }

        // DELETE: api/Orders/{orderId}
        [HttpDelete("{orderId}")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<IActionResult> DeleteOrder(Guid orderId)
        {
            var branchId = await GetCurrentBranchIdAsync(HttpContext.RequestAborted);
            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId);

            if (order == null)
                return NotFound();

            if (order.OrderType == OrderType.Takeaway)
                return BadRequest("Takeaway orders cannot be cancelled.");

            if (order.OrderItems.Any())
                return BadRequest("Use the kitchen approval cancellation endpoint for orders with items.");

            if (order.Status == OrderStatus.Paid || await HasRecordedPaymentsAsync(order.Id))
                return BadRequest("Paid orders cannot be deleted.");

            if (order.OrderItems.Any(oi => oi.IsReady))
                return BadRequest("Orders with ready items cannot be deleted.");

            order.Status = OrderStatus.Cancelled;

            if (order.TableId.HasValue)
            {
                var hasOtherActiveOrders = await _context.Orders.AnyAsync(o =>
                    o.TableId == order.TableId
                    && o.Id != order.Id
                    && o.BranchId == branchId
                    && o.Status != OrderStatus.Paid
                    && o.Status != OrderStatus.Cancelled);

                if (!hasOtherActiveOrders)
                {
                    var table = await _context.Tables.FindAsync(order.TableId.Value);
                    if (table != null)
                    {
                        table.Status = TableStatus.Free;
                    }
                }
            }

            await _context.SaveChangesAsync();
            await InvalidateOrderCachesAsync();
            return NoContent();
        }

        // PUT: api/Orders/{orderId}/items/{orderItemId}/ready
        [HttpPut("{orderId}/items/{orderItemId}/ready")]
        [AllowAnonymous]
        public async Task<IActionResult> MarkOrderItemAsReady(Guid orderId, Guid orderItemId)
        {
            var branchId = await GetRequestBranchIdAsync(_tenantResolver.GetTenantId(), HttpContext.RequestAborted);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BranchId == branchId);

            if (order == null)
                return NotFound();

            if (order.Status == OrderStatus.Cancelled)
                return BadRequest("Cannot modify items on a cancelled order.");

            var orderItem = order.OrderItems.FirstOrDefault(oi => oi.Id == orderItemId);
            if (orderItem == null)
                return NotFound("Order item not found.");

            // Idempotent: if already ready, skip entirely (prevents duplicate deduction)
            if (orderItem.IsReady)
                return Ok(new { message = "Item already ready." });

            orderItem.IsReady = true;
            orderItem.IsNewlyAdded = false; // Clear the NEW flag once item is ready

            var allReady = order.OrderItems.All(oi => oi.IsReady);
            OrderCompletion.ApplyStatus(order);

            await _context.SaveChangesAsync();
            await InvalidateOrderCachesAsync();

            // Immediately deduct stock for THIS item (FEFO, transaction-safe)
            try
            {
                await _inventoryService.ProcessOrderItemStockAsync(orderId, orderItemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Controller] Stock deduction failed for OrderItem {orderItemId}. Item is marked ready but stock not deducted.");
            }

            // NEW: Per-item ready notification via SignalR
            await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.ItemReady, new
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderItemId = orderItem.Id,
                ProductName = orderItem.ProductName,
                TableId = order.TableId,
                TableName = order.TableName,
                OrderType = order.OrderType.ToString(),
                AllReady = allReady
            });

            var orderChannelLabel = ResolveOrderChannelLabel(order);
            var itemReadyTarget = order.OrderType is OrderType.Takeaway or OrderType.Delivery ? "Cashier" : "Waiter";
            _ = _notificationService.SendAsync(
                NotificationType.ItemReady,
                $"Item ready from Order #{order.OrderNumber}",
                $"{orderItem.ProductName} — {orderChannelLabel}",
                order.Id.ToString(),
                itemReadyTarget);

            // If all items ready → notify POS via SignalR (existing logic preserved)
            if (allReady)
            {
                await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.OrderReady, new
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    TableId = order.TableId,
                    TableName = order.TableName
                });
                _logger.LogInformation($"[Controller] All items ready for Order {order.OrderNumber}. SignalR notified.");

                // Persist notification → Cashier + Waiter
                _ = _notificationService.SendAsync(
                    NotificationType.OrderReady,
                    $"Order #{order.OrderNumber} Ready",
                    $"{orderChannelLabel} — All items ready",
                    order.Id.ToString(),
                    "Cashier,Waiter");
            }

            return Ok(new { allReady, orderStatus = order.Status.ToString() });
        }

        // PUT: api/Orders/{id}/ready
        [HttpPut("{id}/ready")]
        [Authorize(Roles = AppRoleGroups.KitchenOperators + "," + AppRoleNames.Cashier)]
        public async Task<IActionResult> MarkOrderAsReady(Guid id)
        {
            var branchId = await GetCurrentBranchIdAsync(HttpContext.RequestAborted);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId);

            if (order == null)
            {
                return NotFound();
            }

            if (order.Status == OrderStatus.Completed)
            {
                return NoContent();
            }

            // Collect items that need stock deduction (newly marked ready)
            var newlyReadyItemIds = new List<Guid>();
            foreach (var item in order.OrderItems)
            {
                if (!item.IsReady)
                {
                    item.IsReady = true;
                    newlyReadyItemIds.Add(item.Id);
                }
            }

            OrderCompletion.ApplyStatus(order);
            var updatedAt = DateTime.UtcNow;
            order.LastUpdatedAt = updatedAt;
            order.SyncedAt = updatedAt;
            await _context.SaveChangesAsync();
            await InvalidateOrderCachesAsync();

            // Deduct stock for each newly-ready item (FEFO, per-item, transaction-safe)
            foreach (var itemId in newlyReadyItemIds)
            {
                try
                {
                    await _inventoryService.ProcessOrderItemStockAsync(id, itemId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[Controller] Stock deduction failed for OrderItem {itemId} in bulk-ready.");
                }
            }

            // Notify Waiters/POS (Real-time)
            await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.OrderReady, new
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                TableId = order.TableId,
                TableName = order.TableName
            });

            _logger.LogInformation($"[Controller] Order {order.OrderNumber} marked Ready. {newlyReadyItemIds.Count} items had stock deducted.");

            // Persist notification → Cashier + Waiter
            _ = _notificationService.SendAsync(
                NotificationType.OrderReady,
                $"Order #{order.OrderNumber} Ready",
                $"{ResolveOrderChannelLabel(order)} — All items ready",
                order.Id.ToString(),
                "Cashier,Waiter");

            return Ok();
        }

        [HttpPut("{id}/preparing")]
        [Authorize(Roles = AppRoleGroups.KitchenOperators + "," + AppRoleNames.Cashier)]
        public async Task<IActionResult> MarkOnlineOrderAsPreparing(Guid id, CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var branchId = await GetCurrentBranchIdAsync(ct);
            await _orderService.MarkOnlineOrderPreparingAsync(id, tenantId, branchId, ct);
            await InvalidateOrderCachesAsync();
            return NoContent();
        }

        [HttpPut("{id}/dispatch")]
        [Authorize(Roles = AppRoleGroups.TrackerOperators)]
        public async Task<IActionResult> MarkOnlineOrderAsDispatched(Guid id, CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var branchId = await GetCurrentBranchIdAsync(ct);
            await _orderService.MarkOnlineOrderDispatchedAsync(id, tenantId, branchId, ct);
            await InvalidateOrderCachesAsync();
            return NoContent();
        }

        // PUT: api/Orders/{id}/served
        // Records the handoff independently from payment and kitchen readiness.
        [HttpPut("{id}/served")]
        [Authorize(Roles = AppRoleGroups.TrackerOperators)]
        public async Task<IActionResult> MarkOrderAsServed(Guid id)
        {
            var branchId = await GetCurrentBranchIdAsync(HttpContext.RequestAborted);
            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId);

            if (order == null)
                return NotFound("Order not found.");

            if (order.Status == OrderStatus.Completed)
            {
                return NoContent();
            }

            if (order.Status == OrderStatus.Cancelled)
                return BadRequest("Cannot mark a cancelled order as Served.");

            if (order.OrderSource == OrderSource.Online &&
                order.OrderItems.Any(item => !item.IsReady))
            {
                return BadRequest("The Online order must be ready before handoff.");
            }

            if (order.OrderSource == OrderSource.Online &&
                order.OrderType == OrderType.Delivery &&
                !order.DispatchedAt.HasValue)
            {
                return BadRequest("The Online delivery order must be dispatched before delivery.");
            }

            order.Status = OrderStatus.Served;
            OrderCompletion.ApplyStatus(order);

            var updatedAt = DateTime.UtcNow;
            order.LastUpdatedAt = updatedAt;
            order.SyncedAt = updatedAt;
            await _context.SaveChangesAsync();
            await InvalidateOrderCachesAsync();

            // Broadcast to all connected clients (tracker, kitchen, cashier)
            await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.OrderServed, new
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                TableId = order.TableId,
                TableName = order.TableName,
                OrderType = order.OrderType.ToString(),
                Status = order.Status.ToString()
            });

            _logger.LogInformation($"[Controller] Order {order.OrderNumber} marked as Served (picked up / delivered).");

            return Ok();
        }

        // PUT: api/Orders/{id}/pickup
        // Records the takeaway handoff. The centralized evaluator promotes the
        // order to Completed when payment and readiness are also satisfied.
        [HttpPut("{id}/pickup")]
        [Authorize(Roles = AppRoleGroups.TrackerOperators)]
        public async Task<IActionResult> MarkTakeawayPickedUp(Guid id)
        {
            var branchId = await GetCurrentBranchIdAsync(HttpContext.RequestAborted);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId);

            if (order == null)
                return NotFound("Order not found.");

            if (order.OrderType != OrderType.Takeaway)
                return BadRequest("Only Takeaway orders can be picked up via this action.");

            // Idempotency: already completed → treat as success
            if (order.Status == OrderStatus.Completed)
                return NoContent();

            if (order.Status == OrderStatus.Cancelled)
                return BadRequest("Cannot mark a cancelled order as Picked Up.");

            if (order.OrderSource == OrderSource.Online &&
                order.OrderItems.Any(item => !item.IsReady))
            {
                return BadRequest("The Online pickup order must be ready before pickup.");
            }

            order.Status = OrderStatus.Served;
            OrderCompletion.ApplyStatus(order);
            var updatedAt = DateTime.UtcNow;
            order.LastUpdatedAt = updatedAt;
            order.SyncedAt = updatedAt;
            await _context.SaveChangesAsync();
            await InvalidateOrderCachesAsync();

            // Broadcast so all tracker screens immediately remove the card
            await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.OrderPickedUp, new
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderType = order.OrderType.ToString(),
                Status = order.Status.ToString()
            });

            _logger.LogInformation($"[Controller] Takeaway Order {order.OrderNumber} marked as Picked Up (Completed).");

            return NoContent();
        }

        // GET: api/Orders/paginated?pageNumber=1&pageSize=10&search=...&status=...
        [HttpGet("paginated")]
        [Authorize(Roles = AppRoleGroups.CashierOperators)]
        public async Task<ActionResult<PaginatedResponse<OrderDto>>> GetOrdersPaginated(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            CancellationToken ct = default)
        {
            var isArabic = IsArabicRequested(Request);
            var branchId = await GetCurrentBranchIdAsync(ct);

            var query = _context.Orders
                .Include(o => o.Table)
                .Include(o => o.PaidByUser)
                .Include(o => o.Customer)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.Payments)
                .Where(o => o.BranchId == branchId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(o =>
                    o.OrderNumber.ToLower().Contains(s) ||
                    (o.DisplayOrderNumber != null && o.DisplayOrderNumber.ToLower().Contains(s)) ||
                    (o.PublicOrderNumber != null && o.PublicOrderNumber.ToLower().Contains(s)) ||
                    (o.TableName != null && o.TableName.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
                    query = query.Where(o => o.Status == parsedStatus);
            }

            var totalCount = await query.CountAsync(ct);

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var items = orders.Select(o => MapOrderToDto(o, isArabic)).ToList();
            await ApplyPendingCancellationStateAsync(items, ct);

            return Ok(new PaginatedResponse<OrderDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        // GET: api/Orders/cashier?dateFilter=today|active|week|month|recent|all&page=1&pageSize=200
        [HttpGet("cashier")]
        [Authorize(Roles = AppRoleGroups.CashierOperators)]
        public async Task<ActionResult<List<OrderDto>>> GetCashierOrders(
            [FromQuery] string? dateFilter = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 200,
            CancellationToken ct = default)
        {
            var isArabic = IsArabicRequested(Request);
            var branchId = await GetCurrentBranchIdAsync(ct);
            var nowUtc = DateTime.UtcNow;
            var normalizedFilter = NormalizeCashierDateFilter(dateFilter);
            var normalizedPage = Math.Max(page, 1);
            var normalizedPageSize = Math.Clamp(pageSize, 1, 200);
            var window = ResolveCashierWindow(normalizedFilter, nowUtc);

            var tenantId = _tenantResolver.GetTenantId();
            var useLegacyCache = normalizedPage == 1
                && normalizedPageSize == 200
                && (normalizedFilter == "today" || normalizedFilter == "all");

            var cacheKey = normalizedFilter == "today" || normalizedFilter == "active"
                ? $"{CacheKeys.OrdersCashierToday(tenantId)}:{branchId:N}"
                : $"{CacheKeys.OrdersCashierAll(tenantId)}:{branchId:N}";
            if (useLegacyCache)
            {
                var cached = await _cache.GetAsync<List<OrderDto>>(cacheKey, ct);
                if (cached != null)
                    return Ok(cached);
            }

            var query = _context.Orders
                .Include(o => o.Table)
                .Include(o => o.PaidByUser)
                .Include(o => o.Customer)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.Payments)
                .Where(o => o.Status != OrderStatus.Cancelled &&
                            o.BranchId == branchId &&
                            o.CreatedAt <= nowUtc);

            if (window.FromUtc.HasValue)
                query = query.Where(o => o.CreatedAt >= window.FromUtc.Value);

            if (window.ActiveOnly)
                query = query.Where(o => o.Status == OrderStatus.New
                                      || o.Status == OrderStatus.Preparing
                                      || o.Status == OrderStatus.Ready);

            var orders = await query
                .AsSplitQuery()
                .OrderByDescending(o => o.CreatedAt)
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync(ct);
            var orderDtos = orders
                .Where(ShouldExposeOperationally)
                .Select(o => MapOrderToDto(o, isArabic))
                .ToList();
            await ApplyPendingCancellationStateAsync(orderDtos, ct);

            if (useLegacyCache)
            {
                await _cache.SetAsync(cacheKey, orderDtos,
                    slidingExpiration: null,
                    absoluteExpiration: TimeSpan.FromSeconds(normalizedFilter == "today" ? 15 : 20),
                    cancellationToken: ct);
            }

            return Ok(orderDtos);
        }

        private static string NormalizeCashierDateFilter(string? dateFilter)
        {
            if (string.IsNullOrWhiteSpace(dateFilter))
                return "today";

            return dateFilter.Trim().ToLowerInvariant() switch
            {
                "active" => "active",
                "today" => "today",
                "week" => "week",
                "month" => "month",
                "recent" => "recent",
                "all" => "all",
                _ => "today"
            };
        }

        private static (DateTime? FromUtc, bool ActiveOnly) ResolveCashierWindow(
            string normalizedFilter,
            DateTime nowUtc)
        {
            var today = nowUtc.Date;
            return normalizedFilter switch
            {
                "active" => (today, true),
                "week" => (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), false),
                "month" => (new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc), false),
                "recent" => (today.AddDays(-1), false),
                "all" => (null, false),
                _ => (today, false)
            };
        }

        // PUT: api/Orders/{id}/checkout
        [HttpPut("{id}/checkout")]
        [Authorize(Roles = AppRoleGroups.TakeawayCheckoutOperators)]
        public async Task<IActionResult> CheckoutOrder(
            Guid id,
            [FromQuery] string paymentMethod,
            [FromQuery] decimal? serviceChargeRate = null,
            [FromQuery] decimal? taxRate = null,
            [FromQuery] string? customerPhone = null,
            [FromQuery] decimal? amountTendered = null,
            [FromQuery] bool applyVoucher = false,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return BadRequest("Payment method is required.");

            if (serviceChargeRate.HasValue && (serviceChargeRate.Value < 0 || serviceChargeRate.Value > 100))
                return BadRequest("serviceChargeRate must be between 0 and 100.");
            if (taxRate.HasValue && (taxRate.Value < 0 || taxRate.Value > 100))
                return BadRequest("taxRate must be between 0 and 100.");

            try
            {
                var (paidByUserId, _, role) = GetCallerIdentity();
                var request = new CreatePaymentRequest
                {
                    OrderId = id,
                    Method = paymentMethod,
                    Amount = null,
                    ServiceChargeRate = serviceChargeRate,
                    TaxRate = taxRate,
                    CustomerPhone = customerPhone,
                    AmountTendered = amountTendered,
                    ApplyVoucher = applyVoucher
                };

                await _paymentService.AddPaymentAsync(request, paidByUserId, role, ct);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout error for Order {OrderId}", id);
                return StatusCode(500, $"Ödeme işlemi sırasında hata oluştu: {ex.Message}");
            }

            return Ok();
        }

        private static bool IsArabicRequested(HttpRequest request)
        {
            var language = request.Headers["Accept-Language"].ToString();
            return language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct = default)
        {
            var context = await _branchContext.GetCurrentAsync(ct);
            return context.CurrentBranch.Id;
        }

        private async Task<Guid?> ResolveReadBranchIdAsync(Guid? requestedBranchId, CancellationToken ct = default)
        {
            if (requestedBranchId == Guid.Empty)
            {
                if (!_currentUser.IsAdminOrManager)
                    throw new ForbiddenException("All branches order scope requires administrator or manager access.");
                return null;
            }

            var context = await _branchContext.GetCurrentAsync(ct);
            if (!requestedBranchId.HasValue)
                return context.CurrentBranch.Id;

            var selected = context.AssignedBranches.FirstOrDefault(b => b.Id == requestedBranchId.Value);
            if (selected is null)
                throw new ForbiddenException("You are not assigned to the selected branch.");
            if (!selected.IsActive)
                throw new ValidationException("Inactive branches cannot be used as order scope.");

            return requestedBranchId.Value;
        }

        private async Task<Guid> GetRequestBranchIdAsync(Guid tenantId, CancellationToken ct = default)
        {
            if (User.Identity?.IsAuthenticated == true)
                return await GetCurrentBranchIdAsync(ct);

            var selectedBranchId = _currentBranchProvider.GetSelectedBranchId();
            var branchId = await _context.Branches
                .Where(b =>
                    b.TenantId == tenantId &&
                    b.IsActive &&
                    (selectedBranchId.HasValue ? b.Id == selectedBranchId.Value : b.IsMainBranch))
                .Select(b => b.Id)
                .FirstOrDefaultAsync(ct);
            return branchId == Guid.Empty
                ? throw new ValidationException("The selected branch is invalid or inactive.")
                : branchId;
        }

        private IClientProxy BranchClients(Guid branchId)
            => _hubContext.Clients.Group(KitchenHubGroups.Branch(branchId));

        private static string ResolveDisplayName(string name, string? nameAr, bool isArabic)
        {
            return isArabic && !string.IsNullOrWhiteSpace(nameAr) ? nameAr : name;
        }

        private static string ResolveTableDisplayName(Order order, bool isArabic)
        {
            if (order.OrderSource == OrderSource.DeliveryPartner)
            {
                return ResolvePartnerDisplayName(order, isArabic);
            }

            if (order.OrderSource == OrderSource.Talabat)
            {
                return TalabatTableName;
            }

            if (order.TableId == null && order.OrderType == OrderType.Takeaway)
            {
                return "TAKEAWAY";
            }

            if (order.TableId == null && order.OrderType == OrderType.Delivery)
            {
                return "DELIVERY";
            }

            if (order.Table != null)
            {
                return ResolveDisplayName(order.Table.Name, order.Table.NameAr, isArabic);
            }

            return order.TableName;
        }

        private static bool ShouldExposeOperationally(Order order)
        {
            return !IsPaymentFirstOrder(order) || OrderPaymentHelper.BuildSnapshot(order).IsPaid;
        }

        /// <summary>
        /// Takeaway/Delivery normally follow a payment-first flow: the kitchen only sees the
        /// order once payment is confirmed. Public storefront orders are the exception — they
        /// are collected on fulfilment (at the counter, or from the driver) whichever channel
        /// the shopper picked, because online payment processing is not enabled
        /// (see <c>OnlineShoppingCheckoutService</c>). Holding them back would leave a card
        /// order invisible to staff forever. When a gateway is wired up, prepaid orders must be
        /// held by that flow rather than by re-reading the method snapshot here.
        /// </summary>
        private static bool IsPaymentFirstOrder(Order order)
            => order.OrderType is OrderType.Takeaway or OrderType.Delivery
                && order.OrderSource != OrderSource.Online;

        private static string ResolveOrderChannelLabel(Order order)
            => order.OrderSource switch
            {
                OrderSource.Talabat => "Talabat",
                OrderSource.DeliveryPartner => ResolvePartnerDisplayName(order, isArabic: false),
                OrderSource.Online => "Online",
                _ => order.OrderType switch
            {
                OrderType.Delivery => "Delivery",
                OrderType.Takeaway => "Takeaway",
                _ => "Dine-in"
            }
            };

        private static string ResolvePartnerDisplayName(Order order, bool isArabic)
        {
            var localizedName = isArabic && !string.IsNullOrWhiteSpace(order.DeliveryPartnerNameAr)
                ? order.DeliveryPartnerNameAr
                : order.DeliveryPartnerName;

            return TrimToNull(localizedName)
                ?? TrimToNull(order.DeliveryPartnerCode)
                ?? TrimToNull(order.TableName)
                ?? DeliveryPartnerTableName;
        }

        private async Task ApplyPendingCancellationStateAsync(List<OrderDto> orderDtos, CancellationToken ct)
        {
            if (orderDtos.Count == 0)
                return;

            var orderIds = orderDtos.Select(o => o.Id).ToList();
            var pendingLogs = await _context.CancelLogs
                .AsNoTracking()
                .Where(c => orderIds.Contains(c.OrderId) &&
                            c.RequiresKitchenApproval &&
                            c.ApprovalStatus == CancellationApprovalStatus.PendingKitchenApproval)
                .Select(c => new
                {
                    c.Id,
                    c.OrderId,
                    c.OrderItemId,
                    c.ItemName,
                    c.CancelledByName,
                    c.CancelledAt,
                    c.CancelReasonCode,
                    c.CancelReasonNote,
                    c.ApprovalStatus
                })
                .ToListAsync(ct);

            if (pendingLogs.Count == 0)
                return;

            var dtosById = orderDtos.ToDictionary(o => o.Id);
            foreach (var group in pendingLogs.GroupBy(c => c.OrderId))
            {
                if (!dtosById.TryGetValue(group.Key, out var dto))
                    continue;

                dto.HasPendingCancellation = true;
                dto.HasPendingFullCancellation = group.Any(c => !c.OrderItemId.HasValue);
                dto.PendingCancellationItemIds = group
                    .Where(c => c.OrderItemId.HasValue)
                    .Select(c => c.OrderItemId!.Value)
                    .Distinct()
                    .ToList();

                // Surface the request to the admin approval popup. Prefer the full-order log;
                // fall back to the earliest item-level log so the popup always has a target.
                var requestLog = group.FirstOrDefault(c => !c.OrderItemId.HasValue)
                    ?? group.OrderBy(c => c.CancelledAt).First();
                dto.CancellationInfo = new CancellationInfoDto
                {
                    CancelLogId = requestLog.Id,
                    OrderItemId = requestLog.OrderItemId,
                    ItemName = requestLog.ItemName,
                    RequestedBy = requestLog.CancelledByName,
                    RequestDate = requestLog.CancelledAt,
                    CancellationReason = requestLog.CancelReasonCode,
                    Notes = requestLog.CancelReasonNote,
                    ApprovalStatus = requestLog.ApprovalStatus.ToString(),
                    PreviousStatus = dto.Status
                };
            }
        }

        // Display preference, highest-priority first:
        //   1. DisplayOrderNumber  — new monthly per-channel number (TA-YYYYMM-N etc.)
        //   2. PublicOrderNumber   — legacy offline OFF# / public-share identifier
        //   3. OrderNumber         — internal raw number (back-compat for pre-feature rows)
        // Keeps historical orders rendering exactly as before because both new
        // fallbacks are NULL for them.
        private static string ResolveOrderNumber(Order order)
        {
            if (!string.IsNullOrWhiteSpace(order.DisplayOrderNumber))
                return order.DisplayOrderNumber;
            if (!string.IsNullOrWhiteSpace(order.PublicOrderNumber))
                return order.PublicOrderNumber;
            return order.OrderNumber;
        }

        private static OrderDto MapOrderToDto(Order order, bool isArabic)
        {
            var paymentSnapshot = OrderPaymentHelper.BuildSnapshot(order);

            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = ResolveOrderNumber(order),
                DisplayOrderNumber = order.DisplayOrderNumber,
                SystemOrderNumber = order.OrderNumber,
                ClientOrderUuid = order.ClientOrderUuid,
                PublicOrderNumber = order.PublicOrderNumber,
                OrderType = order.OrderType.ToString(),
                OrderSource = order.OrderSource.ToString(),
                TalabatOrderNumber = order.TalabatOrderNumber,
                TalabatCustomerName = order.TalabatCustomerName,
                TalabatCustomerPhone = order.TalabatCustomerPhone,
                TalabatPaymentMethod = order.TalabatPaymentMethod,
                TalabatPickupTime = order.TalabatPickupTime,
                TalabatDeliveryFee = order.TalabatDeliveryFee,
                TalabatServiceFee = order.TalabatServiceFee,
                DeliveryPartnerId = order.DeliveryPartnerId,
                DeliveryPartnerName = order.DeliveryPartnerName,
                DeliveryPartnerNameAr = order.DeliveryPartnerNameAr,
                DeliveryPartnerCode = order.DeliveryPartnerCode,
                PartnerOrderNumber = order.PartnerOrderNumber,
                PartnerCustomerName = order.PartnerCustomerName,
                PartnerCustomerPhone = order.PartnerCustomerPhone,
                PartnerPaymentMethod = order.PartnerPaymentMethod,
                PartnerPickupTime = order.PartnerPickupTime,
                PartnerDeliveryFee = order.PartnerDeliveryFee,
                PartnerServiceFee = order.PartnerServiceFee,
                OfferId = order.OfferId,
                OfferName = order.Offer?.Name,
                OfferNameAr = order.Offer?.NameAr,
                OfferNote = order.OfferNote,
                OfferProducts = MapOrderOfferProducts(order.Offer),
                TicketId = order.TicketId,
                TableName = ResolveTableDisplayName(order, isArabic),
                Subtotal = order.Subtotal,
                DiscountPercentage = order.DiscountPercentage,
                DiscountAmount = order.DiscountAmount,
                DiscountGroupId = order.DiscountGroupId,
                DiscountGroupName = order.DiscountGroupName,
                DiscountGroupType = order.DiscountGroupType,
                DiscountGroupValue = order.DiscountGroupValue,
                DiscountGroupAmount = order.DiscountGroupAmount,
                ServiceChargeRate = order.ServiceChargeRate,
                ServiceChargeAmount = order.ServiceChargeAmount,
                TaxRate = order.TaxRate,
                TaxAmount = order.TaxAmount,
                IsVoucherApplied = order.IsVoucherApplied,
                VoucherDiscountAmount = order.VoucherDiscountAmount,
                VoucherAppliedAt = order.VoucherAppliedAt,
                TotalAmount = order.TotalAmount,
                FoodSubtotal = order.FoodSubtotal,
                CustomerDeliveryFee = order.CustomerDeliveryFee,
                ActualDeliveryCost = order.ActualDeliveryCost,
                DeliveryMargin = order.DeliveryMargin,
                MarketplaceDeliveryFee = order.MarketplaceDeliveryFee,
                MarketplaceServiceFee = order.MarketplaceServiceFee,
                NetRestaurantRevenue = order.NetRestaurantRevenue > 0m ? order.NetRestaurantRevenue : order.TotalAmount,
                CostSharingTotalCommission = order.CostSharingTotalCommission,
                CostSharingRestaurantShare = order.CostSharingRestaurantShare,
                CostSharingCounterpartyShare = order.CostSharingCounterpartyShare,
                CostSharingNetSettlement = order.CostSharingNetSettlement,
                CostSharingCalculatedAt = order.CostSharingCalculatedAt,
                DeliveryZoneId = order.DeliveryZoneId,
                DeliveryZoneName = order.DeliveryZoneName,
                DeliveryFee = order.DeliveryFee,
                DeliveryCost = order.DeliveryCost,
                DeliveryPaymentMode = order.DeliveryPaymentMode != null ? order.DeliveryPaymentMode.ToString() : null,
                DeliveryAddress = order.DeliveryAddress,
                DeliveryNotes = order.DeliveryNotes,
                SubmittedPaymentReference = order.SubmittedPaymentReference,
                Status = order.Status.ToString(),
                IsPaid = paymentSnapshot.IsPaid,
                PaymentStatus = paymentSnapshot.PaymentStatus,
                PaymentMethod = paymentSnapshot.PaymentMethod,
                CashierName = ResolveUserDisplayName(order.PaidByUser),
                CreatedBy = order.WaiterId,
                PaidAmount = paymentSnapshot.PaidAmount,
                RemainingAmount = paymentSnapshot.RemainingAmount,
                CustomerId = order.CustomerId,
                CustomerName = order.Customer?.Name ?? order.PartnerCustomerName ?? order.TalabatCustomerName,
                CustomerNameAr = order.Customer?.NameAr,
                CustomerPhone = order.CustomerPhone ?? order.Customer?.PhoneNumber ?? order.PartnerCustomerPhone ?? order.TalabatCustomerPhone,
                AmountTendered = paymentSnapshot.AmountTendered,
                ChangeAmount = paymentSnapshot.ChangeAmount,
                CreatedAt = order.CreatedAt,
                CancelMode = order.CancelMode,
                CanceledById = order.CanceledById,
                CanceledByName = order.CanceledBy?.FullName,
                CanceledAt = order.CanceledAt,
                CancelReason = order.CancelReason,
                LastUpdatedAt = order.LastUpdatedAt,
                DispatchedAt = order.DispatchedAt,
                SyncedAt = order.SyncedAt,
                Payments = paymentSnapshot.Payments,
                Items = CreateDisplayItemsWithOfferBundleStatic(order, order.Offer),
                TotalItemsCount = order.OrderItems?.Sum(i => i.Quantity) ?? 0,
                HasPriceDifference = order.HasPriceDifference,
                PriceDifferenceCorrectionMode = order.PriceDifferenceCorrectionMode,
                OriginalPartnerTotal = order.OriginalPartnerTotal,
                CorrectPartnerTotal = order.CorrectPartnerTotal,
                TotalDifferenceAmount = order.TotalDifferenceAmount,
                HasPartnerDiscountOverride = order.HasPriceDifference ||
                    (order.OrderItems?.Any(i => i.HasPartnerDiscountOverride) ?? false)
            };
        }

        private Task<Order?> LoadOrderForResponseAsync(Guid orderId, CancellationToken ct)
            => _context.Orders
                .Include(o => o.Table)
                .Include(o => o.PaidByUser)
                .Include(o => o.Customer)
                .Include(o => o.Offer)
                    .ThenInclude(offer => offer!.OfferProducts)
                        .ThenInclude(op => op.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.PartnerDiscountUpdatedByUser)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.RecipeSnapshotItems)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        private static string? ResolveUserDisplayName(User? user)
        {
            if (user == null)
                return null;

            return !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Username;
        }

        private static CancelledByRole ResolveCancelledByRole(UserRole role)
        {
            return role switch
            {
                UserRole.Cashier => CancelledByRole.Cashier,
                UserRole.Manager => CancelledByRole.Manager,
                UserRole.Admin => CancelledByRole.Manager,
                _ => CancelledByRole.Waiter
            };
        }

        private static bool CanCancelOrder(Order order, Guid userId, UserRole role)
        {
            if (role == UserRole.Admin || role == UserRole.Manager)
                return true;

            // Cashier handles payment for ALL orders — can cancel any Ready order at the till
            if (role == UserRole.Cashier)
                return true;

            // Waiter (Garçon) can only cancel their own orders
            if (role == UserRole.Waiter)
                return order.WaiterId.HasValue && order.WaiterId.Value == userId;

            return false;
        }

        private static bool CanViewOrder(Order order, Guid userId, UserRole role)
        {
            if (role == UserRole.Admin || role == UserRole.Manager || role == UserRole.Kitchen)
                return true;

            if (role == UserRole.Waiter || role == UserRole.Cashier)
                return order.WaiterId.HasValue && order.WaiterId.Value == userId;

            return false;
        }

        private async Task<Offer?> LoadOfferForReceiptAsync(int? offerId)
        {
            if (!offerId.HasValue)
            {
                return null;
            }

            return await _context.Offers
                .AsNoTracking()
                .Include(o => o.OfferProducts)
                    .ThenInclude(op => op.Product)
                .FirstOrDefaultAsync(o => o.Id == offerId.Value);
        }

        private static List<OrderOfferProductDto> MapOrderOfferProducts(Offer? offer)
        {
            if (offer?.OfferProducts == null)
            {
                return new List<OrderOfferProductDto>();
            }

            return offer.OfferProducts
                .Select(op => new OrderOfferProductDto
                {
                    ProductId = op.ProductId,
                    Name = op.Product?.Name ?? string.Empty,
                    NameAr = op.Product?.NameAr ?? string.Empty,
                    Quantity = op.Quantity,
                })
                .ToList();
        }

        // ============================================================
        // Calculation Engine — Single Source of Truth (Backend Only)
        // ============================================================

        // ── Shared DTO Mappers ──────────────────────────────────────────
        private static OrderItemModifierDto MapModifierDto(OrderItemModifier m) => new()
        {
            ModifierId = m.ModifierId,
            ModifierName = m.ModifierName,
            ModifierNameAr = m.ModifierNameAr,
            Price = m.Price,
            Quantity = m.Quantity
        };

        private static decimal CalcLineTotalAmount(OrderItem oi)
            => oi.IsComplimentary ? 0 : (oi.Price + (oi.Modifiers ?? Enumerable.Empty<OrderItemModifier>()).Sum(m => m.Price * m.Quantity)) * oi.Quantity;

        private static OrderItemModifier MapCreateModifier(OrderItemModifierCreateDto m, Guid tenantId) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModifierId = m.ModifierId,
            ModifierName = m.ModifierName,
            ModifierNameAr = m.ModifierNameAr,
            Price = m.Price,
            Quantity = Math.Max(1, m.Quantity)
        };

        private static List<OrderItemModifier> ResolveOrderItemModifiers(Product product, List<OrderItemModifierCreateDto> modifiers, Guid tenantId)
        {
            var modifierLookup = product.ProductModifierGroups
                .SelectMany(pmg => pmg.ModifierGroup.Modifiers)
                .ToDictionary(m => m.Id, m => m);

            return modifiers.Select(m =>
            {
                Modifier? definition = null;
                if (m.ModifierId.HasValue)
                {
                    modifierLookup.TryGetValue(m.ModifierId.Value, out definition);
                }

                if (definition == null && !string.IsNullOrWhiteSpace(m.ModifierName))
                {
                    definition = modifierLookup.Values.FirstOrDefault(existing =>
                        string.Equals(existing.Name, m.ModifierName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(existing.NameAr, m.ModifierNameAr, StringComparison.OrdinalIgnoreCase));
                }

                return new OrderItemModifier
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ModifierId = definition?.Id ?? m.ModifierId,
                    ModifierName = definition?.Name ?? m.ModifierName,
                    ModifierNameAr = definition?.NameAr ?? m.ModifierNameAr,
                    Price = definition == null
                        ? m.Price
                        : ResolveModifierUnitPrice(product, definition, Math.Max(1, m.Quantity)),
                    Quantity = Math.Max(1, m.Quantity)
                };
            }).ToList();
        }

        private static decimal ResolveModifierUnitPrice(Product product, Modifier definition, int quantity)
        {
            if (definition.IsFree)
            {
                var freeQuantityLimit = Math.Max(0, definition.FreeQuantityLimit);
                if (freeQuantityLimit == 0 || quantity <= freeQuantityLimit)
                    return 0m;
            }

            return definition.PricingType switch
            {
                PricingType.Included => 0m,
                PricingType.Percentage => OrderPaymentHelper.RoundCurrency(
                    GetEffectiveProductPrice(product) * definition.PriceAdjustment / 100m),
                _ => definition.PriceAdjustment
            };
        }

        private async Task<Dictionary<(int OfferId, Guid ProductId), decimal>> BuildOfferUnitPriceOverridesAsync(
            List<OrderItemCreateDto> items,
            int? requestOfferId,
            int? requestOfferQty,
            Dictionary<Guid, Product> products)
        {
            var offerIds = items
                .Where(i => i.OfferLineId.HasValue)
                .Select(i => i.OfferLineId!.Value)
                .Distinct()
                .ToList();

            if (offerIds.Count == 0)
                return new Dictionary<(int OfferId, Guid ProductId), decimal>();

            // Branch platform: offers must be enabled for the order's branch (staff →
            // current branch, anonymous/public → Main Branch) or intake rejects them.
            var offerBranchId = await GetRequestBranchIdAsync(_tenantResolver.GetTenantId(), HttpContext.RequestAborted);
            var enabledOfferIds = await _branchConfigurationService.GetEnabledOfferIdsAsync(offerBranchId, HttpContext.RequestAborted);
            var blockedOfferIds = offerIds.Where(id => !enabledOfferIds.Contains(id)).ToList();
            if (blockedOfferIds.Count > 0)
                throw new RestaurantPos.Api.Exceptions.ValidationException(
                    "One or more selected offers are not available in the current branch.");

            var offers = await _context.Offers
                .Include(o => o.OfferProducts)
                    .ThenInclude(op => op.Product)
                .Where(o => offerIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id);

            var overrides = new Dictionary<(int OfferId, Guid ProductId), decimal>();

            foreach (var group in items.Where(i => i.OfferLineId.HasValue).GroupBy(i => i.OfferLineId!.Value))
            {
                if (!offers.TryGetValue(group.Key, out var offer))
                    continue;

                var groupItems = group.Where(i => products.ContainsKey(i.ProductId)).ToList();
                if (groupItems.Count == 0)
                    continue;

                var offerQty = ResolveOfferQuantity(group.Key, groupItems, offer, requestOfferId, requestOfferQty);
                var allocation = OfferPricingHelper.Allocate(
                    groupItems
                        .Select(i => new OfferAllocationLine(
                            i.ProductId,
                            i.Quantity,
                            GetEffectiveProductPrice(products[i.ProductId])))
                        .ToList(),
                    offer.FinalPrice * offerQty);

                foreach (var line in allocation)
                    overrides[(group.Key, line.ProductId)] = line.UnitPrice;
            }

            return overrides;
        }

        private static int ResolveOfferQuantity(
            int offerId,
            List<OrderItemCreateDto> items,
            Offer offer,
            int? requestOfferId,
            int? requestOfferQty)
        {
            if (requestOfferId == offerId && requestOfferQty.HasValue && requestOfferQty.Value > 0)
                return requestOfferQty.Value;

            var quantitiesByProduct = items
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

            var ratios = offer.OfferProducts
                .Where(op => op.Quantity > 0 && quantitiesByProduct.TryGetValue(op.ProductId, out var quantity) && quantity > 0)
                .Select(op => quantitiesByProduct[op.ProductId] / op.Quantity)
                .Where(quantity => quantity > 0)
                .ToList();

            return ratios.Count > 0 ? ratios.Min() : 1;
        }

        private static decimal? ResolveOfferUnitPrice(
            OrderItemCreateDto item,
            Dictionary<(int OfferId, Guid ProductId), decimal> offerUnitPrices)
        {
            return item.OfferLineId.HasValue && offerUnitPrices.TryGetValue((item.OfferLineId.Value, item.ProductId), out var unitPrice)
                ? unitPrice
                : null;
        }

        private static decimal GetEffectiveProductPrice(Product product)
        {
            return product.DiscountedPrice ?? product.BasePrice;
        }

        private static decimal ResolveOrderItemUnitPrice(
            Product product,
            List<OrderItemModifier> modifiers,
            Guid? selectedOptionId = null,
            List<SelectedRecipeAlternativeDto>? selectedAlternatives = null,
            decimal? baseUnitPriceOverride = null,
            PartnerPricingRule? partnerPricingRule = null)
        {
            // Option price fully replaces base price (no additive modifiers allowed alongside options)
            if (selectedOptionId.HasValue)
            {
                var option = product.Options.FirstOrDefault(o => o.Id == selectedOptionId.Value);
                if (option != null)
                    return partnerPricingRule?.Apply(option.Price) ?? option.Price;
            }

            var baseUnitPrice = baseUnitPriceOverride ?? partnerPricingRule?.Apply(product.BasePrice) ?? GetEffectiveProductPrice(product);
            var allModifiers = product.ProductModifierGroups
                .SelectMany(pmg => pmg.ModifierGroup.Modifiers)
                .ToDictionary(m => m.Id, m => m);

            var hasRecipeOverride = modifiers.Any(m =>
                m.ModifierId.HasValue &&
                allModifiers.TryGetValue(m.ModifierId.Value, out var definition) &&
                definition.RecipeItems.Any());

            var alternativesApply = !hasRecipeOverride;
            var alternativePricing = alternativesApply
                ? ResolveRecipeAlternativePricing(product, selectedAlternatives)
                : RecipeAlternativePricingResult.Empty;

            return hasRecipeOverride
                ? 0m
                : (alternativePricing.FixedOverrideUnitPrice ?? baseUnitPrice) + alternativePricing.PriceImpact;
        }

        private static PriceDifferenceCreateValues ResolveCreatePriceDifference(
            OrderCreateDto input,
            OrderSource orderSource,
            (Guid? userId, string? name, UserRole role) caller)
        {
            if (!input.HasPriceDifference)
                return PriceDifferenceCreateValues.Empty;

            if (orderSource != OrderSource.DeliveryPartner)
                return PriceDifferenceCreateValues.Invalid("Price difference is available only for delivery partner orders.");
            if (caller.userId == null || !CanAdjustDeliveryPartnerPrices(caller.role))
                return PriceDifferenceCreateValues.Invalid("You are not authorized to adjust delivery partner prices.");

            var reason = ResolvePriceDifferenceReason(input.PriceDifferenceReasonCode);
            if (reason == null)
                return PriceDifferenceCreateValues.Invalid("Price difference reason is invalid.");

            var note = NormalizeCreatePriceDifferenceNote(input.PriceDifferenceNote);
            if (note.Error != null)
                return PriceDifferenceCreateValues.Invalid(note.Error);

            var correctionMode = string.IsNullOrWhiteSpace(input.PriceDifferenceCorrectionMode)
                ? CorrectionModePerItem
                : input.PriceDifferenceCorrectionMode.Trim();
            if (correctionMode != CorrectionModePerItem && correctionMode != CorrectionModeOrderTotal)
                return PriceDifferenceCreateValues.Invalid("Price difference correction mode is invalid.");
            if (correctionMode == CorrectionModeOrderTotal && !input.CorrectPartnerTotal.HasValue)
                return PriceDifferenceCreateValues.Invalid("Correct partner total is required.");
            if (input.CorrectPartnerTotal < 0m)
                return PriceDifferenceCreateValues.Invalid("Correct partner total cannot be negative.");

            foreach (var item in input.Items.Where(i => correctionMode == CorrectionModePerItem && i.PartnerCorrectedUnitPrice.HasValue))
            {
                if (item.PartnerCorrectedUnitPrice <= 0m)
                    return PriceDifferenceCreateValues.Invalid("Correct partner price must be greater than zero.");
            }

            return new PriceDifferenceCreateValues(
                true,
                reason.Value.Code,
                reason.Value.Reason,
                note.Value,
                correctionMode,
                input.CorrectPartnerTotal.HasValue ? OrderPaymentHelper.RoundCurrency(input.CorrectPartnerTotal.Value) : null,
                null);
        }

        private static decimal ResolveCreateOrderUnitPrice(
            OrderItemCreateDto item,
            decimal baseUnitPrice,
            PriceDifferenceCreateValues priceDifference)
        {
            if (!priceDifference.HasFlag || priceDifference.CorrectionMode != CorrectionModePerItem || !item.PartnerCorrectedUnitPrice.HasValue)
                return baseUnitPrice;

            return OrderPaymentHelper.RoundCurrency(item.PartnerCorrectedUnitPrice.Value);
        }

        private void AddCreatePriceDifferenceAudits(
            Order order,
            PriceDifferenceCreateValues priceDifference,
            Guid? updatedBy,
            DateTime updatedAt)
        {
            if (!priceDifference.HasFlag || !updatedBy.HasValue)
                return;

            if (priceDifference.CorrectionMode == CorrectionModeOrderTotal)
            {
                _context.PartnerPriceOverrideAudits.Add(BuildCreatePriceDifferenceAudit(
                    order,
                    Guid.Empty,
                    order.OriginalPartnerTotal,
                    order.CorrectPartnerTotal,
                    null,
                    null,
                    priceDifference,
                    updatedBy.Value,
                    updatedAt));
                return;
            }

            var adjustedItems = order.OrderItems.Where(item => item.HasPartnerDiscountOverride).ToList();
            if (adjustedItems.Count == 0)
            {
                _context.PartnerPriceOverrideAudits.Add(BuildCreatePriceDifferenceAudit(
                    order,
                    Guid.Empty,
                    null,
                    null,
                    null,
                    null,
                    priceDifference,
                    updatedBy.Value,
                    updatedAt));
                return;
            }

            foreach (var item in adjustedItems)
            {
                var originalPrice = item.PartnerOriginalUnitPrice ?? item.PartnerPriceSnapshot ?? item.UnitPriceSnapshot ?? item.Price;
                _context.PartnerPriceOverrideAudits.Add(BuildCreatePriceDifferenceAudit(
                    order,
                    item.Id,
                    originalPrice,
                    item.PartnerDiscountedUnitPrice,
                    CalculatePartnerDiscount(originalPrice, originalPrice),
                    CalculatePartnerDiscount(originalPrice, item.PartnerDiscountedUnitPrice ?? item.Price),
                    priceDifference,
                    updatedBy.Value,
                    updatedAt));
            }
        }

        private static PartnerPriceOverrideAudit BuildCreatePriceDifferenceAudit(
            Order order,
            Guid orderItemId,
            decimal? oldPrice,
            decimal? newPrice,
            decimal? oldDiscount,
            decimal? newDiscount,
            PriceDifferenceCreateValues priceDifference,
            Guid updatedBy,
            DateTime updatedAt)
            => new()
            {
                Id = Guid.NewGuid(),
                TenantId = order.TenantId,
                OrderId = order.Id,
                OrderItemId = orderItemId,
                OldPrice = oldPrice,
                NewPrice = newPrice,
                OldDiscountAmount = oldDiscount,
                NewDiscountAmount = newDiscount,
                ReasonCode = priceDifference.ReasonCode,
                Reason = priceDifference.Reason,
                Note = priceDifference.Note,
                DeliveryPartnerId = order.DeliveryPartnerId,
                DeliveryPartnerName = order.DeliveryPartnerName,
                DeliveryPartnerNameAr = order.DeliveryPartnerNameAr,
                DeliveryPartnerCode = order.DeliveryPartnerCode,
                CorrectionMode = priceDifference.CorrectionMode,
                OriginalTotal = priceDifference.CorrectionMode == CorrectionModeOrderTotal ? oldPrice : null,
                CorrectTotal = priceDifference.CorrectPartnerTotal,
                DifferenceAmount = priceDifference.CorrectionMode == CorrectionModeOrderTotal
                    ? OrderPaymentHelper.RoundCurrency((priceDifference.CorrectPartnerTotal ?? 0m) - (oldPrice ?? 0m))
                    : null,
                UpdatedBy = updatedBy,
                UpdatedAt = updatedAt
            };

        private static bool CanAdjustDeliveryPartnerPrices(UserRole role)
            => role == UserRole.Admin ||
                role == UserRole.Manager ||
                role == UserRole.Cashier ||
                role == UserRole.Owner;

        private static (string Code, string Reason)? ResolvePriceDifferenceReason(string? value)
        {
            var code = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (code?.Equals(ReasonCodePriceDifference, StringComparison.OrdinalIgnoreCase) == true)
                return (ReasonCodePriceDifference, ReasonPriceDifference);
            if (code?.Equals(ReasonCodeNoDiscountApplied, StringComparison.OrdinalIgnoreCase) == true)
                return (ReasonCodeNoDiscountApplied, ReasonNoDiscountApplied);
            if (code?.Equals(ReasonCodeExtraDiscountApplied, StringComparison.OrdinalIgnoreCase) == true)
                return (ReasonCodeExtraDiscountApplied, ReasonExtraDiscountApplied);
            if (code?.Equals(ReasonCodeOther, StringComparison.OrdinalIgnoreCase) == true)
                return (ReasonCodeOther, ReasonOther);

            return null;
        }

        private static (string? Value, string? Error) NormalizeCreatePriceDifferenceNote(string? value)
        {
            var note = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            return note?.Length > DeliveryTextMaxLength
                ? (null, $"Price difference note cannot exceed {DeliveryTextMaxLength} characters.")
                : (note, null);
        }

        private static decimal CalculatePartnerDiscount(decimal originalPrice, decimal sellingPrice)
            => OrderPaymentHelper.RoundCurrency(Math.Max(0m, originalPrice - sellingPrice));

        private static decimal? ResolvePartnerPriceSnapshot(
            Product product,
            ProductOption? selectedOption,
            PartnerPricingRule? partnerPricingRule)
        {
            if (partnerPricingRule == null)
                return null;

            return partnerPricingRule.Apply(selectedOption?.Price ?? product.BasePrice);
        }

        private static decimal CalculateLineTotalAmount(
            decimal unitPrice,
            IEnumerable<OrderItemModifier> modifiers,
            int quantity,
            bool isComplimentary)
        {
            if (isComplimentary)
                return 0m;

            var modifiersTotal = modifiers.Sum(m => m.Price * m.Quantity);
            return (unitPrice + modifiersTotal) * quantity;
        }

        private record RecipeAlternativePricingResult(decimal? FixedOverrideUnitPrice, decimal PriceImpact)
        {
            public static RecipeAlternativePricingResult Empty { get; } = new(null, 0m);
        }

        private static RecipeAlternativePricingResult ResolveRecipeAlternativePricing(
            Product product,
            List<SelectedRecipeAlternativeDto>? selectedAlternatives)
        {
            if (selectedAlternatives is not { Count: > 0 })
                return RecipeAlternativePricingResult.Empty;

            var altIndex = product.RecipeItems
                .SelectMany(ri => ri.Alternatives.Select(a => (RecipeItem: ri, Alternative: a)))
                .ToDictionary(x => x.Alternative.Id, x => x);

            var impact = 0m;
            decimal? fixedOverrideUnitPrice = null;
            foreach (var sel in selectedAlternatives)
            {
                if (!altIndex.TryGetValue(sel.AlternativeId, out var hit) ||
                    hit.RecipeItem.Id != sel.RecipeItemId ||
                    !hit.Alternative.IsActive)
                    continue;

                if (hit.Alternative.PricingType == RecipeItemAlternativePricingType.FixedOverride)
                {
                    fixedOverrideUnitPrice = ResolveFixedOverrideUnitPrice(hit.Alternative, fixedOverrideUnitPrice);
                    continue;
                }

                impact += ResolveRecipeAlternativePriceImpact(hit.RecipeItem, hit.Alternative);
            }

            return new RecipeAlternativePricingResult(fixedOverrideUnitPrice, impact);
        }

        private static decimal ResolveRecipeAlternativePriceImpact(
            RecipeItem recipeItem,
            RecipeItemAlternative alternative)
        {
            return alternative.PricingType switch
            {
                RecipeItemAlternativePricingType.Included => 0m,
                RecipeItemAlternativePricingType.PriceDifference => alternative.CustomerAdditionalPrice ?? ResolveLegacyAlternativeImpact(recipeItem, alternative),
                _ => 0m,
            };
        }

        private static decimal ResolveLegacyAlternativeImpact(
            RecipeItem recipeItem,
            RecipeItemAlternative alternative)
        {
            var originalRowPrice = recipeItem.Amount * (recipeItem.RawMaterial?.CostPerUnit ?? 0m);
            return alternative.PriceAdjustment - originalRowPrice;
        }

        private static decimal? ResolveFixedOverrideUnitPrice(
            RecipeItemAlternative alternative,
            decimal? currentOverride)
        {
            var overridePrice = alternative.FixedOverridePrice ?? alternative.PriceAdjustment;
            return currentOverride.HasValue ? Math.Max(currentOverride.Value, overridePrice) : overridePrice;
        }

        private static List<OrderItemRecipeSnapshot> BuildRecipeSnapshot(
            Product product,
            List<OrderItemModifier> modifiers,
            int quantity,
            Guid tenantId,
            ProductOption? selectedOption = null,
            List<SelectedRecipeAlternativeDto>? selectedAlternatives = null)
        {
            // Option recipe fully replaces product and modifier recipes
            if (selectedOption?.RecipeItems.Any() == true)
            {
                return selectedOption.RecipeItems
                    .Select(ri => new OrderItemRecipeSnapshot
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        RawMaterialId = ri.RawMaterialId,
                        SourceOptionId = selectedOption.Id,
                        RawMaterialName = ri.RawMaterial?.Name ?? string.Empty,
                        RawMaterialNameAr = ri.RawMaterial?.NameAr,
                        Quantity = ri.Amount * quantity
                    }).ToList();
            }

            var selectedModifierQuantities = modifiers
                .Where(m => m.ModifierId.HasValue)
                .GroupBy(m => m.ModifierId!.Value)
                .ToDictionary(group => group.Key, group => group.Sum(m => Math.Max(1, m.Quantity)));

            var selectedDefinitions = product.ProductModifierGroups
                .SelectMany(pmg => pmg.ModifierGroup.Modifiers)
                .Where(m => selectedModifierQuantities.ContainsKey(m.Id))
                .ToList();

            var linkedProductSnapshots = selectedDefinitions
                .Where(m => m.LinkedProduct?.RecipeItems.Any() == true)
                .SelectMany(m => m.LinkedProduct!.RecipeItems.Select(ri => new OrderItemRecipeSnapshot
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RawMaterialId = ri.RawMaterialId,
                    SourceModifierId = m.Id,
                    RawMaterialName = ri.RawMaterial?.Name ?? string.Empty,
                    RawMaterialNameAr = ri.RawMaterial?.NameAr,
                    Quantity = ri.Amount * quantity * selectedModifierQuantities[m.Id]
                }))
                .ToList();

            var modifierDefinitions = selectedDefinitions
                .Where(m => m.RecipeItems.Any())
                .ToList();

            // Build: one row per recipe source (modifier override OR product default),
            // swapping any individual product RecipeItem whose alternative was selected.
            List<OrderItemRecipeSnapshot> result;

            if (modifierDefinitions.Any())
            {
                // Modifier override path (ingredient alternatives don't apply here —
                // the modifier's own recipe fully replaces the product recipe).
                result = modifierDefinitions
                    .SelectMany(m => m.RecipeItems.Select(ri => new
                    {
                        ri.RawMaterialId,
                        ri.RawMaterial,
                        Quantity = ri.Amount * quantity,
                        SourceModifierId = (Guid?)m.Id
                    }))
                    .GroupBy(item => new { item.RawMaterialId, item.SourceModifierId, item.RawMaterial.Name, item.RawMaterial.NameAr })
                    .Select(group => new OrderItemRecipeSnapshot
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        RawMaterialId = group.Key.RawMaterialId,
                        SourceModifierId = group.Key.SourceModifierId,
                        RawMaterialName = group.Key.Name,
                        RawMaterialNameAr = group.Key.NameAr,
                        Quantity = group.Sum(item => item.Quantity)
                    }).ToList();
            }
            else
            {
                // Default product recipe path — apply per-ingredient alternative swaps.
                // selected[recipeItemId] → chosen RecipeItemAlternative
                var selected = new Dictionary<Guid, RecipeItemAlternative>();
                if (selectedAlternatives is { Count: > 0 })
                {
                    var altIndex = product.RecipeItems
                        .SelectMany(ri => ri.Alternatives.Select(a => (ri.Id, a)))
                        .ToDictionary(x => x.a.Id, x => (x.Id, x.a));

                    foreach (var sel in selectedAlternatives)
                    {
                        if (altIndex.TryGetValue(sel.AlternativeId, out var hit) && hit.Id == sel.RecipeItemId && hit.a.IsActive)
                            selected[sel.RecipeItemId] = hit.a;
                    }
                }

                result = product.RecipeItems
                    .Select(ri =>
                    {
                        if (selected.TryGetValue(ri.Id, out var alt))
                        {
                            var altName = !string.IsNullOrWhiteSpace(alt.Name) ? alt.Name! : (alt.RawMaterial?.Name ?? string.Empty);
                            var altNameAr = !string.IsNullOrWhiteSpace(alt.NameAr) ? alt.NameAr : alt.RawMaterial?.NameAr;
                            return new OrderItemRecipeSnapshot
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                RawMaterialId = alt.RawMaterialId,
                                RawMaterialName = alt.RawMaterial?.Name ?? string.Empty,
                                RawMaterialNameAr = alt.RawMaterial?.NameAr,
                                SourceRecipeItemId = ri.Id,
                                SourceAlternativeId = alt.Id,
                                SourceAlternativeName = altName,
                                SourceAlternativeNameAr = altNameAr,
                                Quantity = alt.Amount * quantity
                            };
                        }

                        return new OrderItemRecipeSnapshot
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            RawMaterialId = ri.RawMaterialId,
                            RawMaterialName = ri.RawMaterial?.Name ?? string.Empty,
                            RawMaterialNameAr = ri.RawMaterial?.NameAr,
                            SourceRecipeItemId = ri.Id,
                            Quantity = ri.Amount * quantity
                        };
                    }).ToList();
            }

            result.AddRange(linkedProductSnapshots);
            return result;
        }

        private static string? ValidateInventoryAvailability(List<(Product Product, int Quantity, List<OrderItemModifierCreateDto> Modifiers, Guid? SelectedOptionId, List<SelectedRecipeAlternativeDto> SelectedAlternatives)> items)
        {
            // TEMP: Stock-availability gate paused per product request — orders are accepted
            // even when raw-material stock is insufficient. Re-enable by removing this early
            // return when the feature is updated.
            return null;

            #pragma warning disable CS0162 // Unreachable code — kept intact for re-enable.
            var requiredMaterials = new Dictionary<Guid, (string Name, decimal Quantity)>();

            foreach (var (product, itemQuantity, modifiers, selectedOptionId, selectedAlts) in items)
            {
                var resolvedModifiers = ResolveOrderItemModifiers(product, modifiers, product.TenantId);
                var selectedOption = selectedOptionId.HasValue
                    ? product.Options.FirstOrDefault(o => o.Id == selectedOptionId.Value)
                    : null;
                var snapshots = BuildRecipeSnapshot(product, resolvedModifiers, itemQuantity, product.TenantId, selectedOption, selectedAlts);
                foreach (var snapshot in snapshots)
                {
                    if (requiredMaterials.TryGetValue(snapshot.RawMaterialId, out var existing))
                    {
                        requiredMaterials[snapshot.RawMaterialId] = (existing.Name, existing.Quantity + snapshot.Quantity);
                    }
                    else
                    {
                        requiredMaterials[snapshot.RawMaterialId] = (snapshot.RawMaterialName, snapshot.Quantity);
                    }
                }
            }

            var insufficient = items
                .SelectMany(tuple => tuple.Product.RecipeItems.Select(ri => ri.RawMaterial))
                .Concat(items.SelectMany(tuple => tuple.Product.RecipeItems.SelectMany(ri => ri.Alternatives).Select(a => a.RawMaterial)))
                .Concat(items.SelectMany(tuple => tuple.Product.ProductModifierGroups.SelectMany(pmg => pmg.ModifierGroup.Modifiers).SelectMany(m => m.RecipeItems).Select(ri => ri.RawMaterial)))
                .Concat(items.SelectMany(tuple => tuple.Product.ProductModifierGroups.SelectMany(pmg => pmg.ModifierGroup.Modifiers).SelectMany(m => m.LinkedProduct != null ? m.LinkedProduct.RecipeItems.Select(ri => ri.RawMaterial) : Enumerable.Empty<RawMaterial>())))
                .Where(raw => raw != null && requiredMaterials.TryGetValue(raw.Id, out var required) && raw.CurrentStock < required.Quantity)
                .GroupBy(raw => raw.Id)
                .Select(group =>
                {
                    var material = group.First();
                    var required = requiredMaterials[material.Id];
                    return $"{required.Name} ({material.CurrentStock:F2}/{required.Quantity:F2})";
                })
                .FirstOrDefault();

            return insufficient == null ? null : $"Insufficient stock for {insufficient}.";
            #pragma warning restore CS0162
        }

        /// <summary>
        /// Calculates the complete order breakdown from server-side item data.
        /// Order: Subtotal → Discount → Service Charge → Tax → Total
        /// </summary>
        // DTO used by cancel endpoints
        public class CancelOrderRequest
        {
            /// <summary>Cancel reason code — must match a CancelReason.Code value.</summary>
            public string CancelReason { get; set; } = string.Empty;

            /// <summary>Required only when CancelReason == "OTHER" (min 5 chars).</summary>
            public string? CancelReasonNote { get; set; }

            /// <summary>Optional: DIRECT when direct cancellation is enabled; omitted keeps kitchen flow.</summary>
            public string? Mode { get; set; }
        }

        public class KitchenCancellationDecisionRequest
        {
            public KitchenCancellationDecision Decision { get; set; }
            public string? Note { get; set; }
        }

        private record OrderTotals(
            decimal Subtotal,
            decimal DiscountAmount,
            decimal DiscountGroupAmount,
            decimal ServiceChargeAmount,
            decimal TaxAmount,
            decimal Total);

        private static OrderTotals CalculateOrderTotals(
            IEnumerable<OrderItem> items,
            decimal discountPercentage,
            DiscountValueType groupDiscountType,
            decimal groupDiscountValue,
            decimal serviceChargeRate,
            decimal taxRate)
        {
            // Clamp rates defensively to prevent manipulation even if validation was bypassed
            discountPercentage = Math.Clamp(discountPercentage, 0m, 100m);
            serviceChargeRate  = Math.Clamp(serviceChargeRate,  0m, 100m);
            taxRate            = Math.Clamp(taxRate,            0m, 100m);

            // 1. Subtotal = sum of all non-complimentary items (server-side prices only)
            var subtotal = items
                .Where(oi => !oi.IsComplimentary)
                .Sum(oi => (oi.Price + (oi.Modifiers?.Sum(m => m.Price * m.Quantity) ?? 0m)) * oi.Quantity);

            // 2. Manual order discount applied on subtotal first
            var discountAmount = Math.Round(subtotal * discountPercentage / 100m, 2, MidpointRounding.AwayFromZero);
            var afterDiscount  = subtotal - discountAmount;

            // 3. Affiliation group discount applied on the post-order-discount base (stacks sequentially).
            // Percentage → % of the base; FixedAmount → flat value capped at the base (never negative).
            var discountGroupAmount = ComputeGroupDiscountAmount(groupDiscountType, groupDiscountValue, afterDiscount);
            var afterAllDiscounts = afterDiscount - discountGroupAmount;

            // 4. Service charge on the fully discounted subtotal
            var serviceChargeAmount = Math.Round(afterAllDiscounts * serviceChargeRate / 100m, 2, MidpointRounding.AwayFromZero);

            // 5. Tax on the fully discounted subtotal only (do not tax the service charge)
            var taxAmount = Math.Round(afterAllDiscounts * taxRate / 100m, 2, MidpointRounding.AwayFromZero);

            // 6. Total for order = fully discounted subtotal ONLY (service charge and tax apply only at payment)
            var total = afterAllDiscounts;

            return new OrderTotals(subtotal, discountAmount, discountGroupAmount, serviceChargeAmount, taxAmount, total);
        }

        // Server-authoritative group-discount amount. Percentage → % of the discountable base;
        // FixedAmount → flat value, floored at 0 and capped at the base so it can never push the
        // order negative or exceed what is being discounted.
        private static decimal ComputeGroupDiscountAmount(DiscountValueType type, decimal value, decimal baseAmount)
            => type == DiscountValueType.FixedAmount
                ? Math.Min(Math.Round(Math.Max(0m, value), 2, MidpointRounding.AwayFromZero), Math.Max(0m, baseAmount))
                : Math.Round(baseAmount * Math.Clamp(value, 0m, 100m) / 100m, 2, MidpointRounding.AwayFromZero);

        private static decimal CalculateAuditAmount(Order order, IEnumerable<OrderItem> items)
            => CalculateOrderTotals(
                items,
                order.DiscountPercentage,
                order.DiscountGroupType,
                order.DiscountGroupValue,
                order.ServiceChargeRate,
                order.TaxRate).Total;

        // ============================================================
        // Cancel Order / Cancel Item  (Rule 3 — full DB transaction)
        // ============================================================

        /// <summary>
        /// POST api/orders/{id}/cancel  — Cancels the entire order.
        /// Allowed roles: Admin, Waiter (Garçon), Cashier.
        /// Order must be in status READY. Runs inside a full DB transaction.
        /// </summary>
        [HttpPost("{id}/cancel")]
        [Authorize(Roles = AppRoleGroups.CancelOperators)]
        public async Task<ActionResult> CancelOrder(Guid id, [FromBody] CancelOrderRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                _logger.LogWarning("[Cancel] Rejected Order {OrderId} cancellation because the request body was empty. TraceId {TraceId}",
                    id, HttpContext.TraceIdentifier);
                return BadRequest("Request body is required.");
            }

            var validationError = ValidateCancelRequest(request, isFullOrder: true);
            if (validationError != null)
            {
                _logger.LogWarning("[Cancel] Rejected Order {OrderId} cancellation: {ValidationError}. TraceId {TraceId}",
                    id, validationError, HttpContext.TraceIdentifier);
                return BadRequest(validationError);
            }

            var useDirectCancel = await ShouldUseDirectCancellationAsync(request, ct);

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var branchId = await GetCurrentBranchIdAsync(ct);
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p!.RecipeItems)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct);

                if (order == null) return NotFound("Order not found.");
                if (order.Status == OrderStatus.Cancelled)
                    return BadRequest($"Cannot cancel an already-cancelled order.");

                // Paid orders are no longer rejected: they create a pending approval request and a
                // manager settles them with a Refund / Refund + Waste decision. Only unpaid orders
                // keep the original "cannot cancel a paid order" guard.
                var isFullyPaid = OrderPaymentHelper.BuildSnapshot(order).IsPaid;
                if (!isFullyPaid)
                {
                    if (order.Status == OrderStatus.Paid || order.Status == OrderStatus.Completed)
                        return BadRequest($"Cannot cancel a {order.Status} order.");
                    if (await HasRecordedPaymentsAsync(order.Id, ct))
                        return BadRequest("Orders with recorded payments must be refunded instead of cancelled.");
                }

                var (userId, userName, userRoleEnum) = GetCallerIdentity();
                if (userId == null) return Unauthorized();
                if (!CanCancelOrder(order, userId.Value, userRoleEnum))
                    return Forbid();
                var shiftId = await ResolveActiveCashierShiftIdAsync(userId.Value, order.TenantId, order.BranchId, ct);

                var hasPendingRequest = await _context.CancelLogs.AnyAsync(c =>
                    c.OrderId == order.Id &&
                    c.BranchId == order.BranchId &&
                    c.RequiresKitchenApproval &&
                    c.ApprovalStatus == CancellationApprovalStatus.PendingKitchenApproval, ct);
                if (hasPendingRequest)
                    return Conflict("A cancellation request is already pending kitchen approval.");

                var cancelMode = useDirectCancel ? OrderCancelModes.Direct : OrderCancelModes.Kitchen;

                // Paid orders always go to the approval queue so a manager can settle them with a
                // refund decision — they are never cancelled or wasted on the spot.
                // When an unpaid order is already Ready, the kitchen has finished — skip approval
                // and immediately cancel as waste (food was prepared but won't be served).
                var skipKitchenApproval = !isFullyPaid && order.Status == OrderStatus.Ready;
                var cancelImmediately = !isFullyPaid && (useDirectCancel || skipKitchenApproval);
                var immediateWasteItems = cancelImmediately
                    ? ResolveImmediateWasteItems(order, order.OrderItems)
                    : new List<OrderItem>();
                var hasImmediateWaste = immediateWasteItems.Count > 0;

                var cancelLog = new CancelLog
                {
                    Id              = Guid.NewGuid(),
                    TenantId        = order.TenantId,
                    BranchId        = order.BranchId,
                    OrderId         = order.Id,
                    OrderItemId     = null,
                    ItemId          = null,
                    ItemName        = null,
                    Quantity        = order.OrderItems.Sum(i => i.Quantity),
                    Amount          = CalculateAuditAmount(order, order.OrderItems),
                    CancelledById   = userId.Value,
                    CancelledByName = userName ?? "Unknown",
                    CancelledByRole = ResolveCancelledByRole(userRoleEnum),
                    OrderTime       = order.CreatedAt,
                    CancelledAt     = DateTime.UtcNow,
                    CancelReasonCode = request.CancelReason,
                    CancelReasonNote = request.CancelReasonNote,
                    CancelMode = cancelMode,
                    RequiresKitchenApproval = !cancelImmediately,
                    ApprovalStatus = cancelImmediately && hasImmediateWaste
                        ? CancellationApprovalStatus.Waste
                        : cancelImmediately
                            ? CancellationApprovalStatus.ApprovedCancelled
                            : CancellationApprovalStatus.PendingKitchenApproval,
                    WasteLogId      = null,
                    ShiftId         = shiftId
                };
                _context.CancelLogs.Add(cancelLog);

                List<Guid> wasteLogIds = new();
                if (cancelImmediately)
                {
                    wasteLogIds = await CreateWasteLogsForKitchenDecisionAsync(
                        order,
                        cancelLog,
                        immediateWasteItems,
                        userId.Value,
                        userName,
                        ct);
                    if (hasImmediateWaste)
                    {
                        ApplyKitchenDecision(cancelLog, KitchenCancellationDecision.Waste, userId.Value, userName);
                    }
                    order.Status        = OrderStatus.Cancelled;
                    order.LastUpdatedAt = DateTime.UtcNow;
                    order.SyncedAt      = DateTime.UtcNow;
                    StampOrderCancellation(order, cancelLog, cancelMode);
                }

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                await RunCancellationPostCommitAsync(
                    order.Id,
                    "order cancellation request",
                    () => PublishCancellationCreatedAsync(
                        order,
                        cancelLog,
                        wasteLogIds,
                        userName,
                        request.CancelReason,
                        ct));

                _logger.LogInformation("[Cancel] Order {OrderId} cancellation mode {CancelMode} by {User} at {CanceledAt}. Reason: {Reason}",
                    id, cancelMode, userName, cancelLog.CancelledAt, request.CancelReason);

                return Ok(new
                {
                    cancelLogId = cancelLog.Id,
                    requiresKitchenApproval = !cancelImmediately,
                    cancelMode,
                    approvalStatus = cancelLog.ApprovalStatus.ToString(),
                    wasteLogIds,
                    orderCancelled = cancelImmediately,
                    message = cancelImmediately
                        ? "Order cancelled."
                        : "Sent to kitchen for approval."
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await TryRollbackCancellationAsync(transaction, id, "order cancellation request");
                throw;
            }
            catch (Exception ex)
            {
                await TryRollbackCancellationAsync(transaction, id, "order cancellation request");
                _logger.LogError(ex,
                    "[Cancel] Failed to save cancellation request for Order {OrderId}. TraceId {TraceId}",
                    id, HttpContext.TraceIdentifier);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "The cancellation request could not be saved. Please retry."
                });
            }
        }

        /// <summary>
        /// POST api/orders/{id}/refund  — Issues a full refund for a Paid or Preparing order.
        /// Changes order status to Cancelled while preserving the original sale for financial reporting.
        /// Creates a RefundLog, a CancelLog, and one WasteLog per order item for full audit trail.
        /// Allowed roles: Admin, Manager, Cashier (not Waiter — financial operation).
        /// </summary>
        [HttpPost("{id}/refund")]
        [Authorize(Roles = AppRoleGroups.RefundOperators)]
        public async Task<ActionResult> RefundOrder(Guid id, [FromBody] RefundOrderRequest request, CancellationToken ct = default)
        {
            if (request == null)
                return BadRequest("Request body is required.");

            if (string.IsNullOrWhiteSpace(request.RefundReason))
                return BadRequest("Refund reason is required.");

            if (request.RefundReason.Equals("OTHER", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(request.RefundNote) || request.RefundNote.Trim().Length < 5))
                return BadRequest("A note of at least 5 characters is required when reason is OTHER.");

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var branchId = await GetCurrentBranchIdAsync(ct);
                // 1. Load order
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct);

                if (order == null) return NotFound("Order not found.");
                if (!OrderPaymentHelper.BuildSnapshot(order).IsPaid)
                    return BadRequest("Only fully paid orders can be refunded.");

                var (userId, userName, userRoleEnum) = GetCallerIdentity();
                if (userId == null) return Unauthorized();
                var shiftId = await ResolveActiveCashierShiftIdAsync(userId.Value, order.TenantId, order.BranchId, ct);

                // 2. Create RefundLog
                var refundLog = new RefundLog
                {
                    Id               = Guid.NewGuid(),
                    TenantId         = order.TenantId,
                    BranchId         = order.BranchId,
                    OrderId          = order.Id,
                    RefundAmount     = order.TotalAmount,
                    RefundReason     = request.RefundReason,
                    RefundNote       = request.RefundNote,
                    OriginalPaymentMethod = order.PaymentMethod,
                    ProcessedById    = userId.Value,
                    ProcessedByName  = userName ?? "Unknown",
                    ProcessedByRole  = userRoleEnum.ToString(),
                    ProcessedAt      = DateTime.UtcNow
                };
                _context.RefundLogs.Add(refundLog);

                // 3. Create WasteLog per item (food was prepared / served — still counts as waste on refund)
                var wasteLogIds = new List<Guid>();
                foreach (var item in order.OrderItems)
                {
                    var productName = item.ProductName ?? item.Product?.Name ?? "Unknown";
                    var unit        = "pcs";
                    var saleLoss    = CalculateAuditAmount(order, new[] { item });

                    if (item.Product?.RecipeItems?.Any() == true)
                    {
                        var firstIngredient = item.Product.RecipeItems.First();
                        var material = await _context.RawMaterials.FindAsync(firstIngredient.RawMaterialId);
                        if (material != null) unit = material.Unit.ToString();
                    }

                    var wasteLog = new WasteLog
                    {
                        Id                = Guid.NewGuid(),
                        TenantId          = order.TenantId,
                        BranchId          = order.BranchId,
                        Type              = WasteLogType.Waste,
                        Category          = WasteCategory.CancelProduct,
                        ItemId            = item.ProductId,
                        ProductId         = item.ProductId,
                        ItemName          = productName,
                        ItemNameAr        = item.Product?.NameAr,
                        Quantity          = item.Quantity,
                        Amount            = saleLoss,
                        Unit              = unit,
                        Reason            = $"REFUND: {request.RefundReason}{(string.IsNullOrEmpty(request.RefundNote) ? "" : " — " + request.RefundNote)}",
                        CostAmount        = item.StockDeductedCost,
                        SalePriceLoss     = saleLoss,
                        LoggedById        = userId.Value,
                        LoggedByName      = userName,
                        SourceOrderId     = order.Id,
                        SourceOrderItemId = item.Id,
                        ShiftId           = shiftId,
                        CreatedAt         = DateTime.UtcNow
                    };
                    _context.WasteLogs.Add(wasteLog);
                    wasteLogIds.Add(wasteLog.Id);
                }

                // 4. Create CancelLog (audit trail — reason code "REFUND" marks it as a post-payment cancel)
                var cancelLog = new CancelLog
                {
                    Id               = Guid.NewGuid(),
                    TenantId         = order.TenantId,
                    BranchId         = order.BranchId,
                    OrderId          = order.Id,
                    OrderItemId      = null,
                    ItemId           = null,
                    ItemName         = null,
                    Quantity         = order.OrderItems.Sum(i => i.Quantity),
                    Amount           = refundLog.RefundAmount,
                    CancelledById    = userId.Value,
                    CancelledByName  = userName ?? "Unknown",
                    CancelledByRole  = ResolveCancelledByRole(userRoleEnum),
                    OrderTime        = order.CreatedAt,
                    CancelledAt      = DateTime.UtcNow,
                    CancelReasonCode = "REFUND",
                    CancelReasonNote = $"Refund: {request.RefundReason}{(string.IsNullOrEmpty(request.RefundNote) ? "" : " — " + request.RefundNote)}",
                    CancelMode       = OrderCancelModes.Direct,
                    WasteLogId       = null,
                    ShiftId          = shiftId
                };
                _context.CancelLogs.Add(cancelLog);

                // 5. Cancel the order and preserve complete cancellation metadata.
                order.Status = OrderStatus.Cancelled;
                StampOrderCancellation(order, cancelLog, OrderCancelModes.Direct);
                order.LastUpdatedAt = DateTime.UtcNow;
                order.SyncedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                await ReconcileFinishedGoodsStockAsync(order.Id, "refund");
                await InvalidateOrderCachesAsync();

                // 5. Notify kitchen & all clients via SignalR
                await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.OrderRefunded, new
                {
                    OrderId     = order.Id,
                    OrderNumber = order.OrderNumber,
                    TableName   = order.TableName,
                    TableId     = order.TableId,
                    RefundAmount = refundLog.RefundAmount,
                    Status      = "Cancelled"
                }, ct);
                await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.ReceiveNewOrder, MapOrderToDto(order, IsArabicRequested(Request)), ct);

                // 6. Admin/Manager notification (fire-and-forget)
                _ = _notificationService.SendAsync(
                    NotificationType.OrderCancelled,
                    $"Refund issued for Order #{order.OrderNumber}",
                    $"Refunded by {userName} ({userRoleEnum}) — Reason: {request.RefundReason} — Amount: {refundLog.RefundAmount:F2}",
                    order.Id.ToString(),
                    "Admin,Manager",
                    order.TenantId,
                    order.BranchId);

                _logger.LogInformation("[Refund] Order {OrderId} refunded by {User}. Amount: {Amount}, Reason: {Reason}",
                    id, userName, refundLog.RefundAmount, request.RefundReason);

                return Ok(new
                {
                    refundLogId  = refundLog.Id,
                    cancelLogId  = cancelLog.Id,
                    refundAmount = refundLog.RefundAmount,
                    wasteLogIds  = wasteLogIds
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "[Refund] Failed to refund Order {OrderId}. Transaction rolled back.", id);
                return StatusCode(500, "An error occurred while processing the refund.");
            }
        }

        /// <summary>
        /// POST api/orders/{id}/items/{itemId}/cancel  — Cancels a single item on a READY order.
        /// Order must have more than one item remaining after the cancel.
        /// </summary>
        [HttpPost("{id}/items/{itemId}/cancel")]
        [Authorize(Roles = AppRoleGroups.CancelOperators)]
        public async Task<ActionResult> CancelOrderItem(Guid id, Guid itemId, [FromBody] CancelOrderRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                _logger.LogWarning("[Cancel] Rejected Item {ItemId} cancellation on Order {OrderId} because the request body was empty. TraceId {TraceId}",
                    itemId, id, HttpContext.TraceIdentifier);
                return BadRequest("Request body is required.");
            }

            var validationError = ValidateCancelRequest(request, isFullOrder: false);
            if (validationError != null)
            {
                _logger.LogWarning("[Cancel] Rejected Item {ItemId} cancellation on Order {OrderId}: {ValidationError}. TraceId {TraceId}",
                    itemId, id, validationError, HttpContext.TraceIdentifier);
                return BadRequest(validationError);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var branchId = await GetCurrentBranchIdAsync(ct);
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p!.RecipeItems)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Modifiers)
                    .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct);

                if (order == null) return NotFound("Order not found.");
                if (order.OrderType == OrderType.Takeaway)
                    return BadRequest("Takeaway order items cannot be cancelled.");
                if (order.Status == OrderStatus.Paid ||
                    order.Status == OrderStatus.Cancelled ||
                    order.Status == OrderStatus.Completed)
                    return BadRequest($"Cannot cancel items on a {order.Status} order.");
                if (await HasRecordedPaymentsAsync(order.Id, ct))
                    return BadRequest("Orders with recorded payments must be refunded instead of item-cancelled.");

                var item = order.OrderItems.FirstOrDefault(oi => oi.Id == itemId);
                if (item == null) return NotFound("Order item not found.");

                var (userId, userName, userRoleEnum) = GetCallerIdentity();
                if (userId == null) return Unauthorized();
                if (!CanCancelOrder(order, userId.Value, userRoleEnum))
                    return Forbid();
                var shiftId = await ResolveActiveCashierShiftIdAsync(userId.Value, order.TenantId, order.BranchId, ct);

                var affectedItems = ResolveCancellationItems(order, item.Id);
                var affectedIds = affectedItems.Select(i => i.Id).ToList();
                var hasPendingRequest = await _context.CancelLogs.AnyAsync(c =>
                    c.OrderId == order.Id &&
                    c.BranchId == order.BranchId &&
                    c.RequiresKitchenApproval &&
                    c.ApprovalStatus == CancellationApprovalStatus.PendingKitchenApproval &&
                    (!c.OrderItemId.HasValue || affectedIds.Contains(c.OrderItemId.Value)), ct);
                if (hasPendingRequest)
                    return Conflict("A cancellation request is already pending kitchen approval.");

                const string cancelMode = OrderCancelModes.Kitchen;

                var cancelLog = new CancelLog
                {
                    Id              = Guid.NewGuid(),
                    TenantId        = order.TenantId,
                    BranchId        = order.BranchId,
                    OrderId         = order.Id,
                    OrderItemId     = item.Id,
                    ItemId          = item.ProductId,
                    ItemName        = item.ProductName ?? item.Product?.Name,
                    ItemNameAr      = item.Product?.NameAr,
                    Quantity        = affectedItems.Sum(i => i.Quantity),
                    Amount          = CalculateAuditAmount(order, affectedItems),
                    CancelledById   = userId.Value,
                    CancelledByName = userName ?? "Unknown",
                    CancelledByRole = ResolveCancelledByRole(userRoleEnum),
                    OrderTime       = order.CreatedAt,
                    CancelledAt     = DateTime.UtcNow,
                    CancelReasonCode = request.CancelReason,
                    CancelReasonNote = request.CancelReasonNote,
                    CancelMode = cancelMode,
                    RequiresKitchenApproval = true,
                    ApprovalStatus = CancellationApprovalStatus.PendingKitchenApproval,
                    WasteLogId      = null,
                    ShiftId         = shiftId
                };
                _context.CancelLogs.Add(cancelLog);

                List<Guid> wasteLogIds = new();

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                await RunCancellationPostCommitAsync(
                    order.Id,
                    "item cancellation request",
                    () => PublishCancellationCreatedAsync(
                        order,
                        cancelLog,
                        wasteLogIds,
                        userName,
                        request.CancelReason,
                        ct));

                _logger.LogInformation("[Cancel] Item {ItemId} cancellation mode {CancelMode} on Order {OrderId} by {User} at {CanceledAt}",
                    itemId, cancelMode, id, userName, cancelLog.CancelledAt);

                return Ok(new
                {
                    cancelLogId = cancelLog.Id,
                    requiresKitchenApproval = true,
                    cancelMode,
                    approvalStatus = cancelLog.ApprovalStatus.ToString(),
                    affectedItemIds = affectedIds,
                    wasteLogIds,
                    orderCancelled = false,
                    message = "Sent for cancellation approval."
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await TryRollbackCancellationAsync(transaction, id, "item cancellation request");
                throw;
            }
            catch (Exception ex)
            {
                await TryRollbackCancellationAsync(transaction, id, "item cancellation request");
                _logger.LogError(ex,
                    "[Cancel] Failed to save Item {ItemId} cancellation request on Order {OrderId}. TraceId {TraceId}",
                    itemId, id, HttpContext.TraceIdentifier);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "The item cancellation request could not be saved. Please retry."
                });
            }
        }

        [HttpGet("cancel-requests")]
        [Authorize(Roles = AppRoleGroups.KitchenOperators)]
        public async Task<ActionResult> GetPendingCancelRequests(CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var pendingLogs = await _context.CancelLogs
                .AsNoTracking()
                .Include(c => c.Order)
                    .ThenInclude(o => o.OrderItems)
                .Include(c => c.Order)
                    .ThenInclude(o => o.Payments)
                .Where(c => c.RequiresKitchenApproval &&
                            c.BranchId == branchId &&
                            c.ApprovalStatus == CancellationApprovalStatus.PendingKitchenApproval)
                .OrderBy(c => c.CancelledAt)
                .ToListAsync(ct);

            var requests = pendingLogs.Select(c =>
            {
                var targets = c.OrderItemId.HasValue
                    ? ResolveCancellationItems(c.Order, c.OrderItemId.Value)
                    : c.Order.OrderItems.ToList();

                return new
                {
                    cancelLogId = c.Id,
                    c.OrderId,
                    c.OrderItemId,
                    isFullOrder = c.OrderItemId == null,
                    orderNumber = c.Order.OrderNumber,
                    orderType = c.Order.OrderType.ToString(),
                    tableName = c.Order.TableName,
                    isPaid = OrderPaymentHelper.BuildSnapshot(c.Order).IsPaid,
                    orderAmount = c.Order.TotalAmount,
                    requestedBy = c.CancelledByName,
                    requestedAt = c.CancelledAt,
                    reason = c.CancelReasonCode,
                    note = c.CancelReasonNote,
                    approvalStatus = c.ApprovalStatus.ToString(),
                    itemName = c.OrderItemId == null
                        ? null
                        : targets.FirstOrDefault()?.ProductName,
                    affectedItems = targets.Select(i => new
                    {
                        itemId = i.Id,
                        itemName = i.ProductName,
                        quantity = i.Quantity,
                        isReady = i.IsReady,
                        offerLineId = i.OfferLineId
                    }),
                    requiresWasteDecision = targets.Any(i => IsKitchenStartedOrReady(c.Order.Status, i.IsReady))
                };
            }).ToList();

            return Ok(requests);
        }

        [HttpPost("cancel-requests/{cancelLogId}/decision")]
        [Authorize(Roles = AppRoleGroups.KitchenOperators)]
        public async Task<ActionResult> DecideCancelRequest(
            Guid cancelLogId,
            [FromBody] KitchenCancellationDecisionRequest request,
            CancellationToken ct)
        {
            if (request == null)
                return BadRequest("Request body is required.");
            if (!Enum.IsDefined(typeof(KitchenCancellationDecision), request.Decision))
            {
                _logger.LogWarning("[Cancel] Rejected invalid decision {Decision} for CancelLog {CancelLogId}. TraceId {TraceId}",
                    request.Decision, cancelLogId, HttpContext.TraceIdentifier);
                return BadRequest("Decision must be ApprovedCancel, Rejected, Waste, Refund, or RefundWaste.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var branchId = await GetCurrentBranchIdAsync(ct);
                var cancelLog = await _context.CancelLogs
                    .Include(c => c.Order)
                        .ThenInclude(o => o.OrderItems)
                            .ThenInclude(oi => oi.Product)
                                .ThenInclude(p => p!.RecipeItems)
                    .Include(c => c.Order)
                        .ThenInclude(o => o.Payments)
                    .FirstOrDefaultAsync(c => c.Id == cancelLogId && c.BranchId == branchId, ct);

                if (cancelLog == null) return NotFound("Cancel request not found.");
                if (!cancelLog.RequiresKitchenApproval)
                    return BadRequest("This cancellation does not require kitchen approval.");
                if (cancelLog.ApprovalStatus != CancellationApprovalStatus.PendingKitchenApproval)
                    return BadRequest($"Cancel request is already {cancelLog.ApprovalStatus}.");

                var order = cancelLog.Order;

                var (kitchenUserId, kitchenUserName, kitchenUserRole) = GetCallerIdentity();
                if (kitchenUserId == null) return Unauthorized();

                if (request.Decision == KitchenCancellationDecision.Rejected)
                {
                    ApplyKitchenDecision(cancelLog, request.Decision, kitchenUserId.Value, kitchenUserName);
                    cancelLog.ApprovalStatus = CancellationApprovalStatus.Rejected;
                    await _context.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    await RunCancellationPostCommitAsync(
                        order.Id,
                        "cancellation rejection",
                        () => NotifyCancellationResolvedAsync(order, cancelLog, Array.Empty<Guid>(), ct));
                    _logger.LogInformation("[Cancel] CancelLog {CancelLogId} rejected by {User}.",
                        cancelLog.Id, kitchenUserName);
                    return Ok(new { cancelLogId = cancelLog.Id, approvalStatus = cancelLog.ApprovalStatus.ToString() });
                }

                var isRefundDecision = request.Decision == KitchenCancellationDecision.Refund
                    || request.Decision == KitchenCancellationDecision.RefundWaste;
                var wastesInventory = request.Decision == KitchenCancellationDecision.Waste
                    || request.Decision == KitchenCancellationDecision.RefundWaste;
                var isFullyPaid = OrderPaymentHelper.BuildSnapshot(order).IsPaid;

                // Guard the money: a refund decision must target a paid order, and a paid order must
                // never be settled with a plain cancel/waste that would silently keep the payment.
                if (isRefundDecision && !isFullyPaid)
                    return BadRequest("Only paid orders can be refunded.");
                if (!isRefundDecision && isFullyPaid)
                    return BadRequest("Paid orders must be settled with a Refund or Refund + Waste decision.");

                var targets = cancelLog.OrderItemId.HasValue
                    ? ResolveCancellationItems(order, cancelLog.OrderItemId.Value)
                    : order.OrderItems.ToList();
                if (targets.Count == 0)
                    return BadRequest("No cancellable order items were found.");

                var wasteTargets = wastesInventory ? targets : new List<OrderItem>();

                var wasteLogIds = await CreateWasteLogsForKitchenDecisionAsync(
                    order,
                    cancelLog,
                    wasteTargets,
                    kitchenUserId.Value,
                    kitchenUserName,
                    ct);

                if (isRefundDecision)
                {
                    _context.RefundLogs.Add(new RefundLog
                    {
                        Id = Guid.NewGuid(),
                        TenantId = order.TenantId,
                        BranchId = order.BranchId,
                        OrderId = order.Id,
                        RefundAmount = order.TotalAmount,
                        RefundReason = cancelLog.CancelReasonCode,
                        RefundNote = cancelLog.CancelReasonNote,
                        OriginalPaymentMethod = order.PaymentMethod,
                        ProcessedById = kitchenUserId.Value,
                        ProcessedByName = kitchenUserName ?? "Unknown",
                        ProcessedByRole = kitchenUserRole.ToString(),
                        ProcessedAt = DateTime.UtcNow
                    });
                }

                ApplyKitchenDecision(cancelLog, request.Decision, kitchenUserId.Value, kitchenUserName);
                cancelLog.CancelMode ??= OrderCancelModes.Kitchen;
                cancelLog.ApprovalStatus = wastesInventory
                    ? CancellationApprovalStatus.Waste
                    : CancellationApprovalStatus.ApprovedCancelled;

                if (cancelLog.OrderItemId.HasValue)
                {
                    RemoveCancelledItems(order, targets);
                    RecalculateOrderAfterItemCancellation(order);
                }
                else
                {
                    order.Status = OrderStatus.Cancelled;
                    order.LastUpdatedAt = DateTime.UtcNow;
                    order.SyncedAt = DateTime.UtcNow;
                }

                if (order.Status == OrderStatus.Cancelled)
                {
                    StampOrderCancellation(order, cancelLog, cancelLog.CancelMode);
                }

                var refundAmount = order.TotalAmount;
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                await RunCancellationPostCommitAsync(
                    order.Id,
                    "cancellation decision",
                    async () =>
                    {
                        await NotifyCancellationResolvedAsync(order, cancelLog, wasteLogIds, ct);
                        if (isRefundDecision)
                        {
                            await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.OrderRefunded, new
                            {
                                OrderId = order.Id,
                                OrderNumber = order.OrderNumber,
                                TableName = order.TableName,
                                TableId = order.TableId,
                                RefundAmount = refundAmount,
                                Status = "Cancelled"
                            }, ct);
                            await _notificationService.SendAsync(
                                NotificationType.OrderCancelled,
                                $"Refund issued for Order #{order.OrderNumber}",
                                $"Approved by {kitchenUserName} — Reason: {cancelLog.CancelReasonCode} — Amount: {refundAmount:F2}",
                                order.Id.ToString(),
                                "Admin,Manager",
                                order.TenantId,
                                order.BranchId);
                        }
                    });
                _logger.LogInformation("[Cancel] CancelLog {CancelLogId} resolved as {Decision} by {User}.",
                    cancelLog.Id, request.Decision, kitchenUserName);

                return Ok(new
                {
                    cancelLogId = cancelLog.Id,
                    approvalStatus = cancelLog.ApprovalStatus.ToString(),
                    kitchenDecision = cancelLog.KitchenDecision?.ToString(),
                    wasteLogIds,
                    orderCancelled = order.Status == OrderStatus.Cancelled
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await TryRollbackCancellationAsync(transaction, cancelLogId, "cancellation decision");
                throw;
            }
            catch (Exception ex)
            {
                await TryRollbackCancellationAsync(transaction, cancelLogId, "cancellation decision");
                _logger.LogError(ex,
                    "[Cancel] Failed to save decision for CancelLog {CancelLogId}. TraceId {TraceId}",
                    cancelLogId, HttpContext.TraceIdentifier);
                // DIAGNOSTIC: surface the root cause to the client so the failing decision can be
                // traced. Revert to the generic message once the cause is identified.
                var rootCause = ex.GetBaseException().Message;
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = $"Cancellation decision failed: {rootCause}"
                });
            }
        }

        // ============================================================
        // Offer Bundling Display Logic
        // ============================================================

        private static List<OrderItemDto> CreateDisplayItemsWithOfferBundleStatic(Order order, Offer? offer)
        {
            var result = new List<OrderItemDto>();
            var processedItemIds = new HashSet<Guid>();
            var hasOfferLineItems = order.OrderItems.Any(oi => oi.OfferLineId.HasValue);

            // --- NEW: Group items that carry an OfferLineId into per-offer bundles ---
            var offerLineGroups = order.OrderItems
                .Where(oi => oi.OfferLineId.HasValue)
                .GroupBy(oi => oi.OfferLineId!.Value)
                .ToList();

            foreach (var group in offerLineGroups)
            {
                var bundledProducts = new List<OrderOfferProductDto>();
                decimal offerTotal = 0m;
                var firstNamed = group.FirstOrDefault(oi => !string.IsNullOrWhiteSpace(oi.OfferLineName));
                var bundleName = firstNamed?.OfferLineName ?? "Offer";
                var firstNamedAr = group.FirstOrDefault(oi => !string.IsNullOrWhiteSpace(oi.OfferLineNameAr));
                var bundleNameAr = firstNamedAr?.OfferLineNameAr ?? bundleName;

                foreach (var oi in group)
                {
                    processedItemIds.Add(oi.Id);
                    offerTotal += CalcLineTotalAmount(oi);
                    bundledProducts.Add(new OrderOfferProductDto
                    {
                        ProductId = oi.ProductId,
                        Name = oi.ProductName ?? "",
                        NameAr = oi.Product?.NameAr ?? oi.ProductName ?? "",
                        Quantity = oi.Quantity,
                        OrderItemId = oi.Id,        // real DB row id — kitchen "Ready" targets this
                        IsReady = oi.IsReady
                    });
                }

                result.Add(new OrderItemDto
                {
                    Id = Guid.Empty,
                    ProductId = Guid.Empty,
                    OfferLineId = group.Key,
                    ProductName = bundleName,
                    ProductNameAr = bundleNameAr,
                    Quantity = 1,
                    UnitPrice = offerTotal,
                    LineTotalAmount = offerTotal,
                    Notes = string.IsNullOrWhiteSpace(order.OfferNote) ? null : order.OfferNote.Trim(),
                    IsComplimentary = false,
                    ItemStatus = "Prepared",
                    IsNewlyAdded = false,
                    PreparationStation = 0,
                    Modifiers = new List<OrderItemModifierDto>(),
                    IsOfferBundle = true,
                    BundledOfferProducts = bundledProducts
                });
            }

            // --- LEGACY fallback: items without OfferLineId but order has an Offer ---
            var legacyOfferProductIds = new HashSet<Guid>();
            if (!hasOfferLineItems && offer?.OfferProducts != null && offer.OfferProducts.Count > 0)
            {
                var legacyBundledProducts = new List<OrderOfferProductDto>();
                decimal legacyOfferTotal = 0m;

                foreach (var offerProduct in offer.OfferProducts)
                {
                    legacyOfferProductIds.Add(offerProduct.ProductId);

                    var orderItems = order.OrderItems
                        .Where(oi => oi.ProductId == offerProduct.ProductId && !oi.OfferLineId.HasValue)
                        .ToList();

                    foreach (var oi in orderItems)
                    {
                        processedItemIds.Add(oi.Id);
                        legacyOfferTotal += CalcLineTotalAmount(oi);
                    }

                    // Pick the first real OrderItem as the Ready target for this bundled product.
                    // IsReady is true only when every matched OrderItem is already ready.
                    var firstOi = orderItems.FirstOrDefault();
                    var allReady = orderItems.Count > 0 && orderItems.All(x => x.IsReady);

                    legacyBundledProducts.Add(new OrderOfferProductDto
                    {
                        ProductId = offerProduct.ProductId,
                        Name = offerProduct.Product?.Name ?? "",
                        NameAr = offerProduct.Product?.NameAr ?? "",
                        Quantity = offerProduct.Quantity,
                        OrderItemId = firstOi?.Id,   // real DB row id — kitchen "Ready" targets this
                        IsReady = allReady
                    });
                }

                if (legacyBundledProducts.Count > 0 && legacyOfferTotal > 0)
                {
                    result.Add(new OrderItemDto
                    {
                        Id = Guid.Empty,
                        ProductId = Guid.Empty,
                        OfferLineId = null,
                        ProductName = offer.Name,
                        ProductNameAr = string.IsNullOrWhiteSpace(offer.NameAr) ? offer.Name : offer.NameAr,
                        Quantity = 1,
                        UnitPrice = legacyOfferTotal,
                        LineTotalAmount = legacyOfferTotal,
                        Notes = string.IsNullOrWhiteSpace(order.OfferNote) ? null : order.OfferNote.Trim(),
                        IsComplimentary = false,
                        ItemStatus = "Prepared",
                        IsNewlyAdded = false,
                        PreparationStation = 0,
                        Modifiers = new List<OrderItemModifierDto>(),
                        IsOfferBundle = true,
                        BundledOfferProducts = legacyBundledProducts
                    });
                }
            }

            // Add all remaining (non-bundled) items as regular items
            foreach (var oi in order.OrderItems)
            {
                if (!processedItemIds.Contains(oi.Id))
                {
                    result.Add(MapOrderItemToDtoStatic(oi, order.DiscountPercentage));
                }
            }

            return result;
        }

        private static OrderItemDto MapOrderItemToDtoStatic(OrderItem oi, decimal discountPercentage)
        {
            return new OrderItemDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
                OfferLineId = oi.OfferLineId,
                ProductName = oi.ProductName,
                ProductNameAr = oi.Product?.NameAr,
                Quantity = oi.Quantity,
                UnitPrice = oi.IsComplimentary ? 0 : Math.Round(oi.Price * (1 - discountPercentage / 100m), 2, MidpointRounding.AwayFromZero),
                LineTotalAmount = CalcLineTotalAmount(oi),
                UnitPriceSnapshot = oi.UnitPriceSnapshot,
                LineTotalSnapshot = oi.LineTotalSnapshot,
                PartnerPriceSnapshot = oi.PartnerPriceSnapshot,
                HasPartnerDiscountOverride = oi.HasPartnerDiscountOverride,
                PartnerOriginalUnitPrice = oi.PartnerOriginalUnitPrice,
                PartnerDiscountedUnitPrice = oi.PartnerDiscountedUnitPrice,
                PartnerDiscountReason = oi.PartnerDiscountReason,
                PartnerDiscountUpdatedAt = oi.PartnerDiscountUpdatedAt,
                PartnerDiscountUpdatedBy = oi.PartnerDiscountUpdatedBy,
                PartnerDiscountUpdatedByName = ResolveUserDisplayName(oi.PartnerDiscountUpdatedByUser),
                Notes = oi.Notes,
                IsComplimentary = oi.IsComplimentary,
                ItemStatus = oi.IsReady ? "Ready" : "Prepared",
                IsNewlyAdded = oi.IsNewlyAdded,
                PreparationStation = oi.Product != null ? (int)oi.Product.PreparationStation : 0,
                SelectedOptionName = oi.SelectedOptionName,
                SelectedOptionNameAr = oi.SelectedOptionNameAr,
                Modifiers = (oi.Modifiers ?? Enumerable.Empty<OrderItemModifier>()).Select(MapModifierDto).ToList(),
                RecipeSnapshot = (oi.RecipeSnapshotItems ?? Enumerable.Empty<OrderItemRecipeSnapshot>()).Select(rs => new OrderItemRecipeSnapshotDto
                {
                    RawMaterialId = rs.RawMaterialId,
                    SourceModifierId = rs.SourceModifierId,
                    SourceOptionId = rs.SourceOptionId,
                    SourceRecipeItemId = rs.SourceRecipeItemId,
                    SourceAlternativeId = rs.SourceAlternativeId,
                    SourceAlternativeName = rs.SourceAlternativeName,
                    SourceAlternativeNameAr = rs.SourceAlternativeNameAr,
                    RawMaterialName = rs.RawMaterialName,
                    RawMaterialNameAr = rs.RawMaterialNameAr,
                    Quantity = rs.Quantity
                }).ToList(),
                IsOfferBundle = false,
                BundledOfferProducts = null
            };
        }

        private static bool IsKitchenStartedOrReady(OrderStatus orderStatus, bool itemIsReady)
            => itemIsReady || orderStatus == OrderStatus.Preparing || orderStatus == OrderStatus.Ready;

        private async Task<bool> ShouldUseDirectCancellationAsync(CancelOrderRequest request, CancellationToken ct)
        {
            if (!OrderCancelModes.IsDirect(request.Mode))
                return false;

            var settings = await _settingsService.GetSettingsAsync(_tenantResolver.GetTenantId(), ct);
            return settings.AllowDirectCancel;
        }

        private async Task<DateTime> GetRestaurantLocalNowAsync()
        {
            var settings = await _settingsService.GetSettingsAsync(
                _tenantResolver.GetTenantId(),
                HttpContext.RequestAborted);
            var timeZone = RestaurantTimeZone.Resolve(settings.TimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        }

        private static List<OrderItem> ResolveImmediateWasteItems(Order order, IEnumerable<OrderItem> targets)
        {
            var items = targets.ToList();
            return order.Status == OrderStatus.Ready
                ? items
                : items.Where(i => i.IsReady || i.StockDeductedAt.HasValue).ToList();
        }

        private static List<OrderItem> ResolveCancellationItems(Order order, Guid orderItemId)
        {
            var item = order.OrderItems.FirstOrDefault(oi => oi.Id == orderItemId);
            if (item == null)
                return new List<OrderItem>();

            return item.OfferLineId.HasValue
                ? order.OrderItems.Where(oi => oi.OfferLineId == item.OfferLineId).ToList()
                : new List<OrderItem> { item };
        }

        private static void StampOrderCancellation(Order order, CancelLog cancelLog, string cancelMode)
        {
            order.CancelMode = cancelMode;
            order.CanceledById = cancelLog.CancelledById;
            order.CanceledAt = cancelLog.CancelledAt;
            order.CancelReason = TruncateCancelReason(FormatCancelReason(cancelLog));
        }

        private static string TruncateCancelReason(string reason)
            => reason.Length <= OrderCancelReasonMaxLength
                ? reason
                : reason[..OrderCancelReasonMaxLength];

        private void ApplyKitchenDecision(
            CancelLog cancelLog,
            KitchenCancellationDecision decision,
            Guid kitchenUserId,
            string? kitchenUserName)
        {
            cancelLog.KitchenDecision = decision;
            cancelLog.KitchenDecisionById = kitchenUserId;
            cancelLog.KitchenDecisionByName = kitchenUserName ?? "Kitchen";
            cancelLog.KitchenDecidedAt = DateTime.UtcNow;
        }

        private Task<Guid?> ResolveActiveCashierShiftIdAsync(Guid userId, Guid tenantId, Guid branchId, CancellationToken ct)
        {
            return _context.CashierBalanceShifts
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.BranchId == branchId && s.CashierId == userId && s.ClosedAt == null)
                .OrderByDescending(s => s.OpenedAt)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(ct);
        }

        private async Task<List<Guid>> CreateWasteLogsForKitchenDecisionAsync(
            Order order,
            CancelLog cancelLog,
            IEnumerable<OrderItem> wasteItems,
            Guid kitchenUserId,
            string? kitchenUserName,
            CancellationToken ct)
        {
            var wasteLogIds = new List<Guid>();

            foreach (var item in wasteItems)
            {
                if (!item.StockDeductedAt.HasValue)
                {
                    try
                    {
                        await _inventoryService.ProcessOrderItemStockAsync(order.Id, item.Id);
                    }
                    catch (Exception ex)
                    {
                        // Non-blocking: a stock-deduction failure (out-of-stock material, FIFO edge
                        // case) must never abort the cancellation/waste. Mirrors MarkOrderAsReady —
                        // log and continue so the waste log is still recorded and the order is cancelled.
                        _logger.LogError(ex,
                            "[Cancel] Stock deduction failed for OrderItem {OrderItemId} during waste logging; continuing without inventory impact.",
                            item.Id);
                    }
                }

                var saleLoss = CalculateAuditAmount(order, new[] { item });
                var wasteLog = new WasteLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = order.TenantId,
                    BranchId = order.BranchId,
                    Type = WasteLogType.Waste,
                    Category = WasteCategory.CancelProduct,
                    ItemId = item.ProductId,
                    ProductId = item.ProductId,
                    ItemName = item.ProductName ?? item.Product?.Name ?? "Unknown",
                    ItemNameAr = item.Product?.NameAr,
                    Quantity = item.Quantity,
                    Amount = saleLoss,
                    // Cancel waste tracks finished products (1 sandwich, 2 drinks…), so the unit is
                    // always "pcs". Raw-material units (Kilogram, Litre) only apply to inventory
                    // waste from ExpiryJobService.
                    Unit = "pcs",
                    Reason = FormatCancelReason(cancelLog),
                    CostAmount = item.StockDeductedCost,
                    SalePriceLoss = saleLoss,
                    LoggedById = kitchenUserId,
                    LoggedByName = kitchenUserName,
                    CashierActionById = cancelLog.CancelledById,
                    CashierActionByName = cancelLog.CancelledByName,
                    KitchenDecision = KitchenCancellationDecision.Waste,
                    KitchenDecisionById = kitchenUserId,
                    KitchenDecisionByName = kitchenUserName ?? "Kitchen",
                    SourceOrderId = order.Id,
                    SourceOrderItemId = item.Id,
                    ShiftId = cancelLog.ShiftId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.WasteLogs.Add(wasteLog);
                wasteLogIds.Add(wasteLog.Id);

                if (cancelLog.OrderItemId == item.Id && cancelLog.WasteLogId == null)
                {
                    cancelLog.WasteLogId = wasteLog.Id;
                }
            }

            return wasteLogIds;
        }

        private static string FormatCancelReason(CancelLog cancelLog)
            => string.IsNullOrWhiteSpace(cancelLog.CancelReasonNote)
                ? cancelLog.CancelReasonCode
                : $"{cancelLog.CancelReasonCode}: {cancelLog.CancelReasonNote}";

        private void RemoveCancelledItems(Order order, IEnumerable<OrderItem> targets)
        {
            foreach (var item in targets.ToList())
            {
                _context.OrderItems.Remove(item);
                order.OrderItems.Remove(item);
            }
        }

        private static void RecalculateOrderAfterItemCancellation(Order order)
        {
            var remainingItems = order.OrderItems.ToList();
            var totals = CalculateOrderTotals(remainingItems, order.DiscountPercentage, order.DiscountGroupType, order.DiscountGroupValue, order.ServiceChargeRate, order.TaxRate);

            order.Subtotal = totals.Subtotal;
            order.DiscountAmount = totals.DiscountAmount;
            order.DiscountGroupAmount = totals.DiscountGroupAmount;
            order.ServiceChargeAmount = totals.ServiceChargeAmount;
            order.TaxAmount = totals.TaxAmount;
            order.TotalAmount = totals.Total + GetAdditionalFeeTotal(order);
            DeliveryAccountingHelper.Apply(order);
            order.LastUpdatedAt = DateTime.UtcNow;
            order.SyncedAt = DateTime.UtcNow;

            if (remainingItems.Count == 0)
            {
                order.Status = OrderStatus.Cancelled;
                return;
            }

            if (remainingItems.All(oi => oi.IsReady))
            {
                order.Status = OrderStatus.Ready;
                OrderCompletion.StampReadyAtIfNeeded(order);
                return;
            }

            if (order.Status == OrderStatus.Ready)
            {
                order.Status = OrderStatus.Preparing;
            }
        }

        private async Task NotifyCancellationResolvedAsync(
            Order order,
            CancelLog cancelLog,
            IReadOnlyCollection<Guid> wasteLogIds,
            CancellationToken ct)
        {
            await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.CancellationResolved, new
            {
                CancelLogId = cancelLog.Id,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderItemId = cancelLog.OrderItemId,
                ApprovalStatus = cancelLog.ApprovalStatus.ToString(),
                KitchenDecision = cancelLog.KitchenDecision?.ToString(),
                WasteLogIds = wasteLogIds
            }, ct);

            await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.ReceiveNewOrder, MapOrderToDto(order, IsArabicRequested(Request)), ct);

            if (order.Status == OrderStatus.Cancelled)
            {
                await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.OrderCancelled, new
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    OrderItemId = cancelLog.OrderItemId,
                    IsOrderCancelled = true
                }, ct);
            }
        }

        private async Task PublishCancellationCreatedAsync(
            Order order,
            CancelLog cancelLog,
            IReadOnlyCollection<Guid> wasteLogIds,
            string? requestedBy,
            string reason,
            CancellationToken ct)
        {
            if (!cancelLog.RequiresKitchenApproval)
            {
                await NotifyCancellationResolvedAsync(order, cancelLog, wasteLogIds, ct);
                return;
            }

            await BranchClients(order.BranchId).SendAsync(KitchenHubEvents.CancelRequested, new
            {
                CancelLogId = cancelLog.Id,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderItemId = cancelLog.OrderItemId,
                IsFullOrder = !cancelLog.OrderItemId.HasValue,
                ApprovalStatus = cancelLog.ApprovalStatus.ToString()
            }, ct);

            await _notificationService.SendAsync(
                NotificationType.OrderCancelled,
                $"Cancel request for Order #{order.OrderNumber}",
                $"Requested by {requestedBy} - Reason: {reason}",
                order.Id.ToString(),
                "Admin,Manager",
                order.TenantId,
                order.BranchId);
        }

        private async Task RunCancellationPostCommitAsync(
            Guid orderId,
            string operation,
            Func<Task> publishAsync)
        {
            await ReconcileFinishedGoodsStockAsync(orderId, operation);

            try
            {
                await InvalidateOrderCachesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[Cancel] {Operation} for Order {OrderId} committed, but cache invalidation failed. TraceId {TraceId}",
                    operation, orderId, HttpContext.TraceIdentifier);
            }

            try
            {
                await publishAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[Cancel] {Operation} for Order {OrderId} committed, but notifications failed. TraceId {TraceId}",
                    operation, orderId, HttpContext.TraceIdentifier);
            }
        }

        /// <summary>
        /// Lets the finished-goods stock ledger re-derive its position for an order whose state
        /// just changed. It is a reconciler, so it is safe to call after a cancellation request
        /// that changed nothing as well as after a real cancellation, and it is a no-op for
        /// orders with no finished-goods lines — every restaurant order.
        /// </summary>
        private async Task ReconcileFinishedGoodsStockAsync(Guid orderId, string operation)
        {
            try
            {
                await _productStockLedger.SyncOrderAsync(orderId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // The order state is already committed; a ledger failure must not undo it.
                _logger.LogError(ex,
                    "[Cancel] {Operation} for Order {OrderId} committed, but the stock ledger was not reconciled. TraceId {TraceId}",
                    operation, orderId, HttpContext.TraceIdentifier);
            }
        }

        private async Task TryRollbackCancellationAsync(
            IDbContextTransaction transaction,
            Guid referenceId,
            string operation)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                _logger.LogError(rollbackException,
                    "[Cancel] Rollback failed for {Operation} {ReferenceId}. TraceId {TraceId}",
                    operation, referenceId, HttpContext.TraceIdentifier);
            }
        }

        private static string? ValidateCancelRequest(CancelOrderRequest request, bool isFullOrder)
        {
            if (string.IsNullOrWhiteSpace(request.CancelReason))
                return "Cancel reason is required.";

            if (!OrderCancelModes.IsKnown(request.Mode))
                return "Cancel mode must be DIRECT or KITCHEN.";

            // Reason codes come from the DB (cancel-reasons endpoint).
            // We no longer maintain a server-side hardcoded whitelist so that custom
            // tenant reasons (added via the admin UI) are always accepted.
            // The only fixed rule: if reason code is "OTHER", a note ≥5 chars is required.
            if (request.CancelReason.Equals("OTHER", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(request.CancelReasonNote) || request.CancelReasonNote.Trim().Length < 5)
                    return "A note of at least 5 characters is required when reason is OTHER.";
            }

            return null;
        }

        /// <summary>
        /// Busts all cashier-order cache entries for the current tenant.
        /// Must be called after EVERY order mutation so reads are never stale.
        /// </summary>
        private async Task InvalidateOrderCachesAsync()
        {
            var tid = _tenantResolver.GetTenantId();
            await _cache.RemoveAsync(CacheKeys.OrdersCashierToday(tid));
            await _cache.RemoveAsync(CacheKeys.OrdersCashierAll(tid));
            await _cache.RemoveAsync(CacheKeys.KitchenToday(false));
            await _cache.RemoveAsync(CacheKeys.KitchenToday(true));
            await _cache.RemoveAsync(CacheKeys.KitchenAll(false));
            await _cache.RemoveAsync(CacheKeys.KitchenAll(true));
            await _cache.RemoveByPatternAsync(CacheKeys.TakeawayActiveOrdersPattern(tid));
            await _cache.RemoveByPatternAsync($"pos:orders:cashier:*:{tid}:*");
            await _cache.RemoveByPatternAsync("pos:orders:kitchen:*:*:*");

            // Dashboard cache must follow order-lifecycle changes (create, item-add, cancel,
            // refund) so totals, payment-split, and partner-split widgets reconcile within
            // the same TTL window as the underlying data. OrderPaid still flows through
            // DashboardInvalidationHandlers via MediatR; this covers every other path.
            try
            {
                await _dashboardCache.InvalidateScopeAsync(tid, Modules.Dashboard.Security.DashboardScope.Operations);
                await _dashboardCache.InvalidateScopeAsync(tid, Modules.Dashboard.Security.DashboardScope.Financial);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dashboard cache invalidation skipped during order cache flush.");
            }
        }

        private Task<bool> HasRecordedPaymentsAsync(Guid orderId, CancellationToken cancellationToken = default)
            => _context.Payments.AnyAsync(p => p.OrderId == orderId, cancellationToken);

        private (Guid? userId, string? userName, UserRole role) GetCallerIdentity()
        {
            var idClaim = User.Claims.FirstOrDefault(c =>
                c.Type == "sub" || c.Type == "nameid" ||
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

            var nameClaim = User.Claims.FirstOrDefault(c =>
                c.Type == "name" ||
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");

            var roleClaim = User.Claims.FirstOrDefault(c =>
                c.Type == "role" ||
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

            Guid? userId = idClaim != null && Guid.TryParse(idClaim.Value, out var gid) ? gid : null;
            var role = UserRole.Waiter;
            if (!Enum.TryParse<UserRole>(roleClaim?.Value, out role))
            {
                role = UserRole.Waiter;
            }

            return (userId, nameClaim?.Value, role);
        }

        // ============================================================
        // Input Validation
        // ============================================================

        private static string? ValidateOrderInput(OrderCreateDto input, bool deliveryPartnersEnabled)
        {
            if (!Enum.IsDefined(typeof(OrderType), input.OrderType))
                return "Order type is invalid.";
            if (!TryResolveOrderSource(input.OrderSource, out var orderSource))
                return "Order source is invalid.";
            if (input.DiscountPercentage < 0 || input.DiscountPercentage > 100)
                return "Discount percentage must be between 0 and 100.";
            if (input.ServiceChargeRate < 0 || input.ServiceChargeRate > 100)
                return "Service charge rate must be between 0 and 100.";
            if (input.TaxRate < 0 || input.TaxRate > 100)
                return "Tax rate must be between 0 and 100.";
            if (input.Items == null || input.Items.Count == 0)
                return "At least one item is required.";
            if ((OrderType)input.OrderType == OrderType.Delivery && !input.DeliveryZoneId.HasValue)
                return "Delivery zone is required for delivery orders.";
            if (input.DeliveryFeeOverride.HasValue && input.DeliveryFeeOverride.Value < 0)
                return "Delivery fee override cannot be negative.";
            if (!string.IsNullOrWhiteSpace(input.DeliveryAddress) &&
                input.DeliveryAddress.Trim().Length > DeliveryTextMaxLength)
                return $"Delivery address cannot exceed {DeliveryTextMaxLength} characters.";
            if (!string.IsNullOrWhiteSpace(input.DeliveryNotes) &&
                input.DeliveryNotes.Trim().Length > DeliveryTextMaxLength)
                return $"Delivery notes cannot exceed {DeliveryTextMaxLength} characters.";
            if (!string.IsNullOrWhiteSpace(input.CustomerPhone) &&
                input.CustomerPhone.Trim().Length > CustomerPhoneMaxLength)
                return $"Customer phone cannot exceed {CustomerPhoneMaxLength} characters.";
            if (orderSource == OrderSource.Online && string.IsNullOrWhiteSpace(input.CustomerName))
                return "Customer name is required for online orders.";
            if (orderSource == OrderSource.Online && string.IsNullOrWhiteSpace(input.CustomerPhone))
                return "Customer phone is required for online orders.";
            if (orderSource == OrderSource.Online && string.IsNullOrWhiteSpace(input.PaymentMethod))
                return "Payment method is required for online orders.";
            if (orderSource == OrderSource.Online &&
                !Guid.TryParse(input.ClientOrderUuid, out _))
                return "A valid order access token is required for online orders.";
            if (input.ScheduledFor.HasValue &&
                input.ScheduledFor.Value.ToUniversalTime() <= DateTime.UtcNow)
                return "Scheduled time must be in the future.";
            var talabatError = ValidateTalabatInput(input, orderSource);
            if (talabatError != null)
                return talabatError;
            var partnerError = ValidatePartnerInput(input, orderSource, deliveryPartnersEnabled);
            if (partnerError != null)
                return partnerError;

            foreach (var item in input.Items)
            {
                if (item.Quantity <= 0)
                    return "Item quantity must be greater than 0.";
                if (item.Price < 0)
                    return "Item price cannot be negative.";
                foreach (var m in item.Modifiers)
                {
                    if (m.Price < 0)
                        return $"Modifier price cannot be negative: {m.ModifierName}.";
                }
            }

            return null;
        }

        private static string? ValidateTalabatInput(OrderCreateDto input, OrderSource orderSource)
        {
            if (input.TalabatDeliveryFee.HasValue && input.TalabatDeliveryFee.Value < 0)
                return "Talabat delivery fee cannot be negative.";
            if (input.TalabatServiceFee.HasValue && input.TalabatServiceFee.Value < 0)
                return "Talabat service fee cannot be negative.";
            if (!string.IsNullOrWhiteSpace(input.TalabatCustomerPhone) &&
                input.TalabatCustomerPhone.Trim().Length > CustomerPhoneMaxLength)
                return $"Talabat customer phone cannot exceed {CustomerPhoneMaxLength} characters.";
            if (orderSource != OrderSource.Talabat)
                return null;
            if (string.IsNullOrWhiteSpace(input.TalabatOrderNumber))
                return "Talabat order number is required.";
            if (input.TalabatOrderNumber.Trim().Length > TalabatOrderNumberMaxLength)
                return $"Talabat order number cannot exceed {TalabatOrderNumberMaxLength} characters.";
            if (!string.IsNullOrWhiteSpace(input.TalabatCustomerName) &&
                input.TalabatCustomerName.Trim().Length > TalabatCustomerNameMaxLength)
                return $"Talabat customer name cannot exceed {TalabatCustomerNameMaxLength} characters.";
            if (string.IsNullOrWhiteSpace(input.TalabatPaymentMethod))
                return "Talabat payment method is required.";
            if (input.TalabatPaymentMethod.Trim().Length > PaymentMethodMaxLength)
                return $"Talabat payment method cannot exceed {PaymentMethodMaxLength} characters.";

            return null;
        }

        private static string? ValidatePartnerInput(
            OrderCreateDto input,
            OrderSource orderSource,
            bool deliveryPartnersEnabled)
        {
            if (input.PartnerDeliveryFee.HasValue && input.PartnerDeliveryFee.Value < 0)
                return "Partner delivery fee cannot be negative.";
            if (input.PartnerServiceFee.HasValue && input.PartnerServiceFee.Value < 0)
                return "Partner service fee cannot be negative.";
            if (input.ActualDeliveryCost.HasValue && input.ActualDeliveryCost.Value < 0)
                return "Actual delivery cost cannot be negative.";
            if (!string.IsNullOrWhiteSpace(input.PartnerCustomerPhone) &&
                input.PartnerCustomerPhone.Trim().Length > CustomerPhoneMaxLength)
                return $"Partner customer phone cannot exceed {CustomerPhoneMaxLength} characters.";
            if (!string.IsNullOrWhiteSpace(input.PartnerCustomerName) &&
                input.PartnerCustomerName.Trim().Length > PartnerCustomerNameMaxLength)
                return $"Partner customer name cannot exceed {PartnerCustomerNameMaxLength} characters.";
            if (!string.IsNullOrWhiteSpace(input.PartnerPaymentMethod) &&
                input.PartnerPaymentMethod.Trim().Length > PaymentMethodMaxLength)
                return $"Partner payment method cannot exceed {PaymentMethodMaxLength} characters.";
            if (orderSource != OrderSource.DeliveryPartner)
                return null;
            if (!deliveryPartnersEnabled)
                return DeliveryPartnersDisabledMessage;
            if (!input.DeliveryPartnerId.HasValue || input.DeliveryPartnerId.Value == Guid.Empty)
                return "Delivery partner is required.";
            if (string.IsNullOrWhiteSpace(input.PartnerOrderNumber))
                return "Partner order number is required.";
            if (input.PartnerOrderNumber.Trim().Length > PartnerOrderNumberMaxLength)
                return $"Partner order number cannot exceed {PartnerOrderNumberMaxLength} characters.";
            if (string.IsNullOrWhiteSpace(input.PartnerCustomerName))
                return "Partner customer name is required.";
            if (string.IsNullOrWhiteSpace(input.PartnerCustomerPhone))
                return "Partner customer phone is required.";
            if (string.IsNullOrWhiteSpace(input.PartnerPaymentMethod))
                return "Partner payment method is required.";

            return null;
        }

        private static string? NormalizeCustomerPhone(string? phone)
        {
            var normalizedPhone = phone?.Trim();
            return string.IsNullOrWhiteSpace(normalizedPhone) ? null : normalizedPhone;
        }
    }
}

