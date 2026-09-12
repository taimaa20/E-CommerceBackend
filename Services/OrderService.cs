using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using System.Globalization;
using System.Text;

namespace RestaurantPos.Api.Services
{
    public class OrderService : IOrderService
    {
        private const int MaxPageSize = 100;
        private const int MaxSearchLength = 100;
        private const int MaxPaymentLength = 50;
        private const int MaxPartnerDiscountReasonLength = 500;
        private const string SortNewest = "newest";
        private const string SortOldest = "oldest";
        private const string CsvDateFormat = "yyyy-MM-dd HH:mm:ss";
        private const string ReasonCodePriceDifference = "PriceDifference";
        private const string ReasonCodeNoDiscountApplied = "NoDiscountApplied";
        private const string ReasonCodeExtraDiscountApplied = "ExtraDiscountApplied";
        private const string ReasonCodeOther = "Other";
        private const string ReasonPriceDifference = "Price Difference";
        private const string ReasonNoDiscountApplied = "No Discount Applied";
        private const string ReasonExtraDiscountApplied = "Extra Discount Applied";
        private const string ReasonOther = "Other";
        private const string DefaultPartnerPriceOverrideReason = "Partner price updated.";
        private const string DefaultOrderTotalOverrideReason = "Order total corrected.";
        private const string CorrectionModePerItem = "PerItem";
        private const string CorrectionModeOrderTotal = "OrderTotal";
        private static readonly string[] ExportHeaders =
        {
            "Order No",
            "Date",
            "Total",
            "Food Revenue",
            "Delivery Collected",
            "Delivery Cost",
            "Delivery Profit",
            "Marketplace Delivery Fees",
            "Marketplace Service Fees",
            "Net Restaurant Revenue",
            "Payment Method",
            "Payment Reference",
            "Cashier",
            "Source",
            "Talabat Order No",
            "Partner",
            "Partner Code",
            "Partner Order No",
            "Status"
        };
        private static readonly char[] CsvSpecialChars = { ',', '"', '\r', '\n' };

        private readonly IOrderRepository _repository;
        private readonly ILogger<OrderService> _logger;

        public OrderService(IOrderRepository repository, ILogger<OrderService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<PaginatedResponse<OrderListItemDto>> GetOrdersAsync(
            OrderFilterRequest request,
            Guid tenantId,
            Guid? branchId,
            bool isArabic,
            CancellationToken ct)
        {
            var filter = BuildFilter(request, tenantId, branchId);
            return _repository.GetOrdersAsync(filter, isArabic, ct);
        }

        public Task<OrderListSummaryDto> GetOrdersSummaryAsync(
            OrderFilterRequest request,
            Guid tenantId,
            Guid? branchId,
            CancellationToken ct)
        {
            var filter = BuildFilter(request, tenantId, branchId);
            return _repository.GetOrdersSummaryAsync(filter, ct);
        }

        public async Task<string> ExportOrdersCsvAsync(
            OrderFilterRequest request,
            Guid tenantId,
            Guid? branchId,
            CancellationToken ct)
        {
            var filter = BuildFilter(request, tenantId, branchId);
            var rows = await _repository.GetOrdersExportRowsAsync(filter, ct);
            return BuildOrdersCsv(rows);
        }

        public async Task<List<PartnerPriceOverrideAuditDto>> GetPartnerPriceOverrideAuditsAsync(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct)
        {
            if (orderId == Guid.Empty)
                throw new ValidationException("Order is required.");
            ValidateTenant(tenantId);
            ValidateBranch(branchId);

            if (!await _repository.OrderExistsAsync(orderId, tenantId, branchId, ct))
                throw new NotFoundException("Order", orderId);

            return await _repository.GetPartnerPriceOverrideAuditsAsync(orderId, tenantId, branchId, ct);
        }

        public async Task<PartnerDiscountOverrideDto> UpdatePartnerDiscountOverrideAsync(
            Guid orderId,
            Guid orderItemId,
            PartnerDiscountOverrideRequest request,
            Guid tenantId,
            Guid branchId,
            Guid updatedBy,
            CancellationToken ct)
        {
            ValidatePartnerDiscountBoundary(orderId, orderItemId, tenantId, branchId, updatedBy, request);

            var item = await _repository.GetOrderItemForPartnerDiscountAsync(orderId, orderItemId, tenantId, branchId, ct)
                ?? throw new NotFoundException("Order item", orderItemId);

            var values = NormalizePartnerDiscountValues(request, item);
            var oldPrice = item.Price;
            var actor = await _repository.GetUserAsync(updatedBy, tenantId, ct);

            ApplyPartnerDiscountValues(item, values, updatedBy);
            RecalculatePartnerOrderTotals(item.Order);
            await _repository.AddPartnerPriceOverrideAuditAsync(BuildPartnerDiscountAudit(item, oldPrice, values, updatedBy), ct);
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation("Partner discount override updated for order {OrderId}, item {OrderItemId} by {UserId}", orderId, orderItemId, updatedBy);
            return MapPartnerDiscountOverride(item, actor);
        }

        public async Task<DeliveryPartnerPriceAdjustmentDto> UpdateDeliveryPartnerPriceAdjustmentAsync(
            Guid orderId,
            DeliveryPartnerPriceAdjustmentRequest request,
            Guid tenantId,
            Guid branchId,
            Guid updatedBy,
            CancellationToken ct)
        {
            ValidateDeliveryPartnerAdjustmentBoundary(orderId, tenantId, branchId, updatedBy, request);

            var order = await _repository.GetOrderForDeliveryPartnerPriceAdjustmentAsync(orderId, tenantId, branchId, ct)
                ?? throw new NotFoundException("Order", orderId);
            ValidateDeliveryPartnerAdjustmentOrder(order);

            var values = NormalizeDeliveryPartnerAdjustmentValues(request);
            var actor = await _repository.GetUserAsync(updatedBy, tenantId, ct);
            var previousTotal = order.TotalAmount;
            var updatedAt = DateTime.UtcNow;
            var adjustedItems = values.CorrectionMode == CorrectionModePerItem
                ? ApplyDeliveryPartnerItemAdjustments(order, values, updatedBy, updatedAt)
                : new List<DeliveryPartnerItemAdjustment>();

            order.HasPriceDifference = true;
            order.PriceDifferenceCorrectionMode = values.CorrectionMode;
            if (values.CorrectionMode == CorrectionModeOrderTotal)
                ApplyDeliveryPartnerTotalAdjustment(order, previousTotal, values.CorrectPartnerTotal!.Value);
            else
            {
                order.OriginalPartnerTotal = null;
                order.CorrectPartnerTotal = null;
                order.TotalDifferenceAmount = null;
                RecalculatePartnerOrderTotals(order);
            }
            await AddDeliveryPartnerAdjustmentAuditsAsync(order, adjustedItems, values, previousTotal, updatedBy, updatedAt, ct);
            order.LastUpdatedAt = updatedAt;
            order.SyncedAt = updatedAt;
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation("Delivery partner price adjustment saved for order {OrderId} by {UserId}", orderId, updatedBy);
            return MapDeliveryPartnerPriceAdjustment(order, previousTotal, values, adjustedItems, updatedBy, actor, updatedAt);
        }

        public async Task<OrderTotalOverrideDto> UpdateOrderTotalOverrideAsync(
            Guid orderId,
            OrderTotalOverrideRequest request,
            Guid tenantId,
            Guid branchId,
            Guid updatedBy,
            CancellationToken ct)
        {
            ValidateOrderTotalOverrideBoundary(orderId, tenantId, branchId, updatedBy, request);

            var order = await _repository.GetOrderForTotalOverrideAsync(orderId, tenantId, branchId, ct)
                ?? throw new NotFoundException("Order", orderId);

            var previousTotal = order.TotalAmount;
            var correctedTotal = OrderPaymentHelper.RoundCurrency(request.CorrectedTotalAmount);
            var reason = NormalizePartnerDiscountReason(request.Reason) ?? DefaultOrderTotalOverrideReason;
            var actor = await _repository.GetUserAsync(updatedBy, tenantId, ct);

            ApplyOrderTotalOverride(order, correctedTotal);
            await _repository.AddPartnerPriceOverrideAuditAsync(
                BuildOrderTotalOverrideAudit(order, previousTotal, correctedTotal, reason, updatedBy),
                ct);
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation("Order total override updated for order {OrderId} by {UserId}", orderId, updatedBy);
            return MapOrderTotalOverride(order, previousTotal, reason, updatedBy, actor);
        }

        public async Task MarkOnlineOrderPreparingAsync(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct)
        {
            var order = await GetOnlineLifecycleOrderAsync(orderId, tenantId, branchId, ct);
            if (order.Status == OrderStatus.Preparing)
                return;
            if (order.Status is not OrderStatus.New and not OrderStatus.Paid ||
                order.OrderItems.All(item => item.IsReady))
                throw new ValidationException("This order cannot start preparation.");

            order.Status = OrderStatus.Preparing;
            StampLifecycleUpdate(order);
            await _repository.SaveChangesAsync(ct);
            _logger.LogInformation("Online order {OrderId} started preparation", orderId);
        }

        public async Task MarkOnlineOrderDispatchedAsync(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct)
        {
            var order = await GetOnlineLifecycleOrderAsync(orderId, tenantId, branchId, ct);
            if (order.DispatchedAt.HasValue)
                return;
            if (order.OrderType != OrderType.Delivery)
                throw new ValidationException("Only delivery orders can be dispatched.");
            if (order.Status is not OrderStatus.Ready and not OrderStatus.Paid ||
                order.OrderItems.Count == 0 || order.OrderItems.Any(item => !item.IsReady))
                throw new ValidationException("The order must be ready before dispatch.");

            order.DispatchedAt = DateTime.UtcNow;
            StampLifecycleUpdate(order);
            await _repository.SaveChangesAsync(ct);
            _logger.LogInformation("Online delivery order {OrderId} dispatched", orderId);
        }

        private async Task<Order> GetOnlineLifecycleOrderAsync(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            CancellationToken ct)
        {
            var order = await _repository.GetOrderForLifecycleAsync(orderId, tenantId, branchId, ct)
                ?? throw new NotFoundException("Order", orderId);
            if (order.OrderSource != OrderSource.Online)
                throw new ValidationException("This action is available only for Online orders.");
            return order;
        }

        private static void StampLifecycleUpdate(Order order)
        {
            var now = DateTime.UtcNow;
            order.LastUpdatedAt = now;
            order.SyncedAt = now;
        }

        private static void ValidatePartnerDiscountBoundary(
            Guid orderId,
            Guid orderItemId,
            Guid tenantId,
            Guid branchId,
            Guid updatedBy,
            PartnerDiscountOverrideRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (orderId == Guid.Empty || orderItemId == Guid.Empty)
                throw new ValidationException("Order and order item are required.");
            ValidateTenant(tenantId);
            ValidateBranch(branchId);
            if (updatedBy == Guid.Empty)
                throw new UnauthorizedException("Invalid session.");
        }

        private static void ValidateDeliveryPartnerAdjustmentBoundary(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            Guid updatedBy,
            DeliveryPartnerPriceAdjustmentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (orderId == Guid.Empty)
                throw new ValidationException("Order is required.");
            ValidateTenant(tenantId);
            ValidateBranch(branchId);
            if (updatedBy == Guid.Empty)
                throw new UnauthorizedException("Invalid session.");
        }

        private static void ValidateDeliveryPartnerAdjustmentOrder(Order order)
        {
            if (order.OrderSource != OrderSource.DeliveryPartner)
                throw new ValidationException("Price adjustments are available only for delivery partner orders.");
            if (order.Status == OrderStatus.Cancelled)
                throw new ValidationException("Cannot adjust a cancelled order.");
        }

        private static void ValidateOrderTotalOverrideBoundary(
            Guid orderId,
            Guid tenantId,
            Guid branchId,
            Guid updatedBy,
            OrderTotalOverrideRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (orderId == Guid.Empty)
                throw new ValidationException("Order is required.");
            ValidateTenant(tenantId);
            ValidateBranch(branchId);
            if (updatedBy == Guid.Empty)
                throw new UnauthorizedException("Invalid session.");
            if (request.CorrectedTotalAmount <= 0m)
                throw new ValidationException("Corrected order total must be greater than zero.");
        }

        private static PartnerDiscountValues NormalizePartnerDiscountValues(
            PartnerDiscountOverrideRequest request,
            OrderItem item)
        {
            var originalPrice = item.PartnerOriginalUnitPrice
                ?? request.PartnerOriginalUnitPrice
                ?? item.PartnerPriceSnapshot
                ?? item.Price;
            var partnerPrice = request.PartnerDiscountedUnitPrice ?? item.Price;
            var reason = NormalizePartnerDiscountReason(request.PartnerDiscountReason);
            var reasonCode = NormalizeOptionalAdjustmentReasonCode(request.ReasonCode);
            var note = NormalizePartnerDiscountReason(request.Note);

            if (originalPrice <= 0m)
                throw new ValidationException("Original price must be greater than zero.");
            if (partnerPrice <= 0m)
                throw new ValidationException("Partner price must be greater than zero.");

            return new PartnerDiscountValues(
                request.HasPartnerDiscountOverride,
                originalPrice,
                OrderPaymentHelper.RoundCurrency(partnerPrice),
                reason ?? DefaultPartnerPriceOverrideReason,
                reasonCode,
                note);
        }

        private static DeliveryPartnerAdjustmentValues NormalizeDeliveryPartnerAdjustmentValues(
            DeliveryPartnerPriceAdjustmentRequest request)
        {
            if (!request.HasPriceDifference)
                throw new ValidationException("Price difference flag must be enabled.");

            var (reasonCode, reason) = ResolveAdjustmentReason(request.ReasonCode);
            var note = NormalizePartnerDiscountReason(request.Note);
            var correctionMode = NormalizeCorrectionMode(request.CorrectionMode);
            var correctPartnerTotal = NormalizeCorrectPartnerTotal(request.CorrectPartnerTotal, correctionMode);
            var items = correctionMode == CorrectionModePerItem
                ? NormalizeDeliveryPartnerAdjustmentItems(request.Items)
                : new List<DeliveryPartnerAdjustmentItemInput>();

            return new DeliveryPartnerAdjustmentValues(reasonCode, reason, note, correctionMode, correctPartnerTotal, items);
        }

        private static string NormalizeCorrectionMode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals(CorrectionModePerItem, StringComparison.OrdinalIgnoreCase))
                return CorrectionModePerItem;
            if (value.Equals(CorrectionModeOrderTotal, StringComparison.OrdinalIgnoreCase))
                return CorrectionModeOrderTotal;
            throw new ValidationException("Price difference correction mode is invalid.");
        }

        private static decimal? NormalizeCorrectPartnerTotal(decimal? value, string correctionMode)
        {
            if (correctionMode == CorrectionModePerItem)
                return null;
            if (!value.HasValue)
                throw new ValidationException("Correct partner total is required.");
            if (value.Value < 0m)
                throw new ValidationException("Correct partner total cannot be negative.");
            return OrderPaymentHelper.RoundCurrency(value.Value);
        }

        private static List<DeliveryPartnerAdjustmentItemInput> NormalizeDeliveryPartnerAdjustmentItems(
            IEnumerable<DeliveryPartnerPriceAdjustmentItemRequest>? items)
        {
            var result = new List<DeliveryPartnerAdjustmentItemInput>();
            foreach (var item in items ?? Enumerable.Empty<DeliveryPartnerPriceAdjustmentItemRequest>())
            {
                if (item.OrderItemId == Guid.Empty)
                    throw new ValidationException("Order item is required.");
                if (item.NewUnitPrice <= 0m)
                    throw new ValidationException("New selling price must be greater than zero.");
                result.Add(new(item.OrderItemId, OrderPaymentHelper.RoundCurrency(item.NewUnitPrice)));
            }

            if (result.Select(i => i.OrderItemId).Distinct().Count() != result.Count)
                throw new ValidationException("Duplicate order items are not allowed.");

            return result;
        }

        private static (string Code, string Reason) ResolveAdjustmentReason(string? value)
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

            throw new ValidationException("Price difference reason is invalid.");
        }

        private static string? NormalizeOptionalAdjustmentReasonCode(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : ResolveAdjustmentReason(value).Code;
        }

        private static string? NormalizePartnerDiscountReason(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var reason = value.Trim();
            if (reason.Length > MaxPartnerDiscountReasonLength)
                throw new ValidationException($"Reason cannot exceed {MaxPartnerDiscountReasonLength} characters.");

            return reason;
        }

        private static void ApplyPartnerDiscountValues(
            OrderItem item,
            PartnerDiscountValues values,
            Guid updatedBy)
        {
            item.HasPartnerDiscountOverride = values.HasOverride;
            item.PartnerOriginalUnitPrice = values.OriginalUnitPrice;
            item.PartnerDiscountedUnitPrice = values.DiscountedUnitPrice;
            item.PartnerDiscountReason = values.Reason;
            item.PartnerDiscountUpdatedAt = DateTime.UtcNow;
            item.PartnerDiscountUpdatedBy = updatedBy;
            item.Price = values.DiscountedUnitPrice ?? item.Price;
        }

        private static List<DeliveryPartnerItemAdjustment> ApplyDeliveryPartnerItemAdjustments(
            Order order,
            DeliveryPartnerAdjustmentValues values,
            Guid updatedBy,
            DateTime updatedAt)
        {
            var orderItems = order.OrderItems.ToDictionary(i => i.Id);
            var adjustedItems = new List<DeliveryPartnerItemAdjustment>();

            foreach (var input in values.Items)
            {
                if (!orderItems.TryGetValue(input.OrderItemId, out var item))
                    throw new ValidationException("Order item does not belong to this order.");

                adjustedItems.Add(ApplyDeliveryPartnerItemAdjustment(item, input, values, updatedBy, updatedAt));
            }

            return adjustedItems;
        }

        private static DeliveryPartnerItemAdjustment ApplyDeliveryPartnerItemAdjustment(
            OrderItem item,
            DeliveryPartnerAdjustmentItemInput input,
            DeliveryPartnerAdjustmentValues values,
            Guid updatedBy,
            DateTime updatedAt)
        {
            var previousPrice = item.Price;
            var originalPrice = ResolvePartnerOriginalUnitPrice(item);
            var previousDiscount = CalculatePartnerDiscount(originalPrice, previousPrice);
            var newDiscount = CalculatePartnerDiscount(originalPrice, input.NewUnitPrice);

            item.HasPartnerDiscountOverride = true;
            item.PartnerOriginalUnitPrice = originalPrice;
            item.PartnerDiscountedUnitPrice = input.NewUnitPrice;
            item.PartnerDiscountReason = values.Reason;
            item.PartnerDiscountUpdatedAt = updatedAt;
            item.PartnerDiscountUpdatedBy = updatedBy;
            item.Price = input.NewUnitPrice;
            item.UnitPriceSnapshot = input.NewUnitPrice;
            item.LineTotalSnapshot = CalculateLineTotalSnapshot(item);

            return new(item.Id, previousPrice, input.NewUnitPrice, previousDiscount, newDiscount);
        }

        private static void RecalculatePartnerOrderTotals(Order order)
        {
            var totals = CalculateOrderTotals(
                order.OrderItems,
                order.DiscountPercentage,
                order.DiscountGroupType,
                order.DiscountGroupValue,
                order.ServiceChargeRate,
                order.TaxRate);

            order.Subtotal = totals.Subtotal;
            order.DiscountAmount = totals.DiscountAmount;
            order.DiscountGroupAmount = totals.DiscountGroupAmount;
            order.ServiceChargeAmount = totals.ServiceChargeAmount;
            order.TaxAmount = totals.TaxAmount;
            order.TotalAmount = totals.Total + totals.ServiceChargeAmount + totals.TaxAmount + GetAdditionalFeeTotal(order);

            if (order.IsVoucherApplied)
                order.TotalAmount = OrderPaymentHelper.RoundCurrency(Math.Max(0m, order.TotalAmount - order.VoucherDiscountAmount));

            SyncSinglePaidPayment(order);
            DeliveryAccountingHelper.Apply(order);
        }

        private async Task AddDeliveryPartnerAdjustmentAuditsAsync(
            Order order,
            IReadOnlyCollection<DeliveryPartnerItemAdjustment> adjustedItems,
            DeliveryPartnerAdjustmentValues values,
            decimal previousTotal,
            Guid updatedBy,
            DateTime updatedAt,
            CancellationToken ct)
        {
            if (adjustedItems.Count == 0)
            {
                await _repository.AddPartnerPriceOverrideAuditAsync(
                    BuildDeliveryPartnerOrderFlagAudit(order, values, previousTotal, updatedBy, updatedAt),
                    ct);
                return;
            }

            foreach (var item in adjustedItems)
                await _repository.AddPartnerPriceOverrideAuditAsync(BuildDeliveryPartnerItemAudit(order, item, values, updatedBy, updatedAt), ct);
        }

        private static void ApplyDeliveryPartnerTotalAdjustment(Order order, decimal originalTotal, decimal correctTotal)
        {
            var systemTotal = order.PriceDifferenceCorrectionMode == CorrectionModeOrderTotal
                ? order.OriginalPartnerTotal ?? originalTotal
                : originalTotal;
            order.OriginalPartnerTotal = systemTotal;
            order.CorrectPartnerTotal = correctTotal;
            order.TotalDifferenceAmount = OrderPaymentHelper.RoundCurrency(correctTotal - systemTotal);
            order.TotalAmount = correctTotal;
            SyncSinglePaidPayment(order);
        }

        private static void ApplyOrderTotalOverride(Order order, decimal correctedTotal)
        {
            var updatedAt = DateTime.UtcNow;
            var delta = correctedTotal - order.TotalAmount;
            var currentFoodRevenue = order.FoodSubtotal > 0m ? order.FoodSubtotal : order.TotalAmount;
            var currentNetRevenue = order.NetRestaurantRevenue > 0m ? order.NetRestaurantRevenue : order.TotalAmount;

            order.TotalAmount = correctedTotal;
            order.FoodSubtotal = RoundNonNegative(currentFoodRevenue + delta);
            order.NetRestaurantRevenue = RoundNonNegative(currentNetRevenue + delta);
            order.LastUpdatedAt = updatedAt;
            order.SyncedAt = updatedAt;
            SyncSinglePaidPayment(order);
        }

        private static decimal RoundNonNegative(decimal amount)
            => OrderPaymentHelper.RoundCurrency(Math.Max(0m, amount));

        private static void SyncSinglePaidPayment(Order order)
        {
            var payment = order.Payments?.Count == 1 ? order.Payments.First() : null;
            if (payment == null || !OrderPaymentHelper.IsPaid(order))
                return;

            payment.Amount = order.TotalAmount;
            order.PaidAt ??= payment.CreatedAt;
            order.PaymentMethod ??= payment.Method;
        }

        private static decimal GetAdditionalFeeTotal(Order order)
            => (order.DeliveryFee ?? 0m)
                + (order.TalabatDeliveryFee ?? 0m)
                + (order.TalabatServiceFee ?? 0m);

        private static decimal ResolvePartnerOriginalUnitPrice(OrderItem item)
            => OrderPaymentHelper.RoundCurrency(
                item.PartnerOriginalUnitPrice ??
                item.PartnerPriceSnapshot ??
                item.UnitPriceSnapshot ??
                item.Price);

        private static decimal CalculatePartnerDiscount(decimal originalPrice, decimal sellingPrice)
            => OrderPaymentHelper.RoundCurrency(Math.Max(0m, originalPrice - sellingPrice));

        private static decimal CalculateLineTotalSnapshot(OrderItem item)
        {
            var modifiersTotal = item.Modifiers?.Sum(m => m.Price * m.Quantity) ?? 0m;
            return item.IsComplimentary
                ? 0m
                : OrderPaymentHelper.RoundCurrency((item.Price + modifiersTotal) * item.Quantity);
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
            discountPercentage = Math.Clamp(discountPercentage, 0m, 100m);
            serviceChargeRate = Math.Clamp(serviceChargeRate, 0m, 100m);
            taxRate = Math.Clamp(taxRate, 0m, 100m);

            var subtotal = items
                .Where(oi => !oi.IsComplimentary)
                .Sum(oi => (oi.Price + (oi.Modifiers?.Sum(m => m.Price * m.Quantity) ?? 0m)) * oi.Quantity);

            var discountAmount = OrderPaymentHelper.RoundCurrency(subtotal * discountPercentage / 100m);
            var afterDiscount = subtotal - discountAmount;
            // Group discount: percentage of the base, or a flat amount capped at the base.
            var discountGroupAmount = groupDiscountType == DiscountValueType.FixedAmount
                ? Math.Min(OrderPaymentHelper.RoundCurrency(Math.Max(0m, groupDiscountValue)), Math.Max(0m, afterDiscount))
                : OrderPaymentHelper.RoundCurrency(afterDiscount * Math.Clamp(groupDiscountValue, 0m, 100m) / 100m);
            var afterAllDiscounts = afterDiscount - discountGroupAmount;
            var serviceChargeAmount = OrderPaymentHelper.RoundCurrency(afterAllDiscounts * serviceChargeRate / 100m);
            var taxAmount = OrderPaymentHelper.RoundCurrency(afterAllDiscounts * taxRate / 100m);

            return new OrderTotals(subtotal, discountAmount, discountGroupAmount, serviceChargeAmount, taxAmount, afterAllDiscounts);
        }

        private static PartnerPriceOverrideAudit BuildPartnerDiscountAudit(
            OrderItem item,
            decimal? oldPrice,
            PartnerDiscountValues values,
            Guid updatedBy)
            => new()
            {
                Id = Guid.NewGuid(),
                TenantId = item.TenantId,
                OrderId = item.OrderId,
                OrderItemId = item.Id,
                OldPrice = oldPrice,
                NewPrice = values.DiscountedUnitPrice,
                OldDiscountAmount = CalculatePartnerDiscount(values.OriginalUnitPrice, oldPrice ?? item.Price),
                NewDiscountAmount = CalculatePartnerDiscount(values.OriginalUnitPrice, values.DiscountedUnitPrice ?? item.Price),
                ReasonCode = values.ReasonCode,
                Reason = values.Reason,
                Note = values.Note,
                DeliveryPartnerId = item.Order.DeliveryPartnerId,
                DeliveryPartnerName = item.Order.DeliveryPartnerName,
                DeliveryPartnerNameAr = item.Order.DeliveryPartnerNameAr,
                DeliveryPartnerCode = item.Order.DeliveryPartnerCode,
                UpdatedBy = updatedBy,
                UpdatedAt = item.PartnerDiscountUpdatedAt ?? DateTime.UtcNow
            };

        private static PartnerPriceOverrideAudit BuildDeliveryPartnerItemAudit(
            Order order,
            DeliveryPartnerItemAdjustment item,
            DeliveryPartnerAdjustmentValues values,
            Guid updatedBy,
            DateTime updatedAt)
            => BuildDeliveryPartnerAudit(
                order,
                item.OrderItemId,
                item.PreviousPrice,
                item.NewPrice,
                item.PreviousDiscount,
                item.NewDiscount,
                values,
                updatedBy,
                updatedAt);

        private static PartnerPriceOverrideAudit BuildDeliveryPartnerOrderFlagAudit(
            Order order,
            DeliveryPartnerAdjustmentValues values,
            decimal previousTotal,
            Guid updatedBy,
            DateTime updatedAt)
            => BuildDeliveryPartnerAudit(
                order,
                Guid.Empty,
                values.CorrectionMode == CorrectionModeOrderTotal ? order.OriginalPartnerTotal ?? previousTotal : null,
                values.CorrectPartnerTotal,
                null,
                null,
                values,
                updatedBy,
                updatedAt);

        private static PartnerPriceOverrideAudit BuildDeliveryPartnerAudit(
            Order order,
            Guid orderItemId,
            decimal? previousPrice,
            decimal? newPrice,
            decimal? previousDiscount,
            decimal? newDiscount,
            DeliveryPartnerAdjustmentValues values,
            Guid updatedBy,
            DateTime updatedAt)
            => new()
            {
                Id = Guid.NewGuid(),
                TenantId = order.TenantId,
                OrderId = order.Id,
                OrderItemId = orderItemId,
                OldPrice = previousPrice,
                NewPrice = newPrice,
                OldDiscountAmount = previousDiscount,
                NewDiscountAmount = newDiscount,
                ReasonCode = values.ReasonCode,
                Reason = values.Reason,
                Note = values.Note,
                DeliveryPartnerId = order.DeliveryPartnerId,
                DeliveryPartnerName = order.DeliveryPartnerName,
                DeliveryPartnerNameAr = order.DeliveryPartnerNameAr,
                DeliveryPartnerCode = order.DeliveryPartnerCode,
                CorrectionMode = values.CorrectionMode,
                OriginalTotal = values.CorrectionMode == CorrectionModeOrderTotal ? previousPrice : null,
                CorrectTotal = values.CorrectPartnerTotal,
                DifferenceAmount = values.CorrectionMode == CorrectionModeOrderTotal
                    ? OrderPaymentHelper.RoundCurrency((values.CorrectPartnerTotal ?? 0m) - (previousPrice ?? 0m))
                    : null,
                UpdatedBy = updatedBy,
                UpdatedAt = updatedAt
            };

        private static PartnerPriceOverrideAudit BuildOrderTotalOverrideAudit(
            Order order,
            decimal previousTotal,
            decimal correctedTotal,
            string reason,
            Guid updatedBy)
            => new()
            {
                Id = Guid.NewGuid(),
                TenantId = order.TenantId,
                OrderId = order.Id,
                OrderItemId = Guid.Empty,
                OldPrice = previousTotal,
                NewPrice = correctedTotal,
                Reason = reason,
                UpdatedBy = updatedBy,
                UpdatedAt = order.LastUpdatedAt ?? DateTime.UtcNow
            };

        private static PartnerDiscountOverrideDto MapPartnerDiscountOverride(OrderItem item, User? actor)
            => new()
            {
                OrderId = item.OrderId,
                OrderItemId = item.Id,
                HasPartnerDiscountOverride = item.HasPartnerDiscountOverride,
                PartnerOriginalUnitPrice = item.PartnerOriginalUnitPrice,
                PartnerDiscountedUnitPrice = item.PartnerDiscountedUnitPrice,
                PartnerDiscountReason = item.PartnerDiscountReason,
                PartnerDiscountUpdatedAt = item.PartnerDiscountUpdatedAt,
                PartnerDiscountUpdatedBy = item.PartnerDiscountUpdatedBy,
                PartnerDiscountUpdatedByName = ResolveUserDisplayName(actor)
            };

        private static DeliveryPartnerPriceAdjustmentDto MapDeliveryPartnerPriceAdjustment(
            Order order,
            decimal previousTotal,
            DeliveryPartnerAdjustmentValues values,
            IReadOnlyCollection<DeliveryPartnerItemAdjustment> items,
            Guid updatedBy,
            User? actor,
            DateTime updatedAt)
            => new()
            {
                OrderId = order.Id,
                HasPriceDifference = true,
                ReasonCode = values.ReasonCode,
                Reason = values.Reason,
                Note = values.Note,
                CorrectionMode = values.CorrectionMode,
                OriginalPartnerTotal = order.OriginalPartnerTotal,
                CorrectPartnerTotal = order.CorrectPartnerTotal,
                TotalDifferenceAmount = order.TotalDifferenceAmount,
                PreviousTotalAmount = previousTotal,
                NewTotalAmount = order.TotalAmount,
                UpdatedAt = updatedAt,
                UpdatedBy = updatedBy,
                UpdatedByName = ResolveUserDisplayName(actor),
                Items = items.Select(MapDeliveryPartnerItemAdjustment).ToList()
            };

        private static DeliveryPartnerPriceAdjustmentItemDto MapDeliveryPartnerItemAdjustment(
            DeliveryPartnerItemAdjustment item)
            => new()
            {
                OrderItemId = item.OrderItemId,
                PreviousPrice = item.PreviousPrice,
                NewPrice = item.NewPrice,
                PreviousDiscount = item.PreviousDiscount,
                NewDiscount = item.NewDiscount
            };

        private static OrderTotalOverrideDto MapOrderTotalOverride(
            Order order,
            decimal previousTotal,
            string reason,
            Guid updatedBy,
            User? actor)
            => new()
            {
                OrderId = order.Id,
                PreviousTotalAmount = previousTotal,
                CorrectedTotalAmount = order.TotalAmount,
                Reason = reason,
                UpdatedAt = order.LastUpdatedAt ?? DateTime.UtcNow,
                UpdatedBy = updatedBy,
                UpdatedByName = ResolveUserDisplayName(actor)
            };

        private static string? ResolveUserDisplayName(User? user)
            => user == null
                ? null
                : !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Username;

        private sealed record PartnerDiscountValues(
            bool HasOverride,
            decimal OriginalUnitPrice,
            decimal? DiscountedUnitPrice,
            string? Reason,
            string? ReasonCode,
            string? Note);

        private sealed record DeliveryPartnerAdjustmentValues(
            string ReasonCode,
            string Reason,
            string? Note,
            string CorrectionMode,
            decimal? CorrectPartnerTotal,
            IReadOnlyList<DeliveryPartnerAdjustmentItemInput> Items);

        private sealed record DeliveryPartnerAdjustmentItemInput(Guid OrderItemId, decimal NewUnitPrice);

        private sealed record DeliveryPartnerItemAdjustment(
            Guid OrderItemId,
            decimal PreviousPrice,
            decimal NewPrice,
            decimal PreviousDiscount,
            decimal NewDiscount);

        private static string BuildOrdersCsv(IEnumerable<OrderExportRowDto> rows)
        {
            var csv = new StringBuilder();
            AppendCsvRow(csv, ExportHeaders);

            foreach (var row in rows)
            {
                AppendCsvRow(csv, new[]
                {
                    row.OrderNumber,
                    row.CreatedAt.ToString(CsvDateFormat, CultureInfo.InvariantCulture),
                    row.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture),
                    row.FoodSubtotal.ToString("0.00", CultureInfo.InvariantCulture),
                    row.CustomerDeliveryFee.ToString("0.00", CultureInfo.InvariantCulture),
                    row.ActualDeliveryCost.ToString("0.00", CultureInfo.InvariantCulture),
                    row.DeliveryMargin.ToString("0.00", CultureInfo.InvariantCulture),
                    row.MarketplaceDeliveryFee.ToString("0.00", CultureInfo.InvariantCulture),
                    row.MarketplaceServiceFee.ToString("0.00", CultureInfo.InvariantCulture),
                    row.NetRestaurantRevenue.ToString("0.00", CultureInfo.InvariantCulture),
                    row.PaymentMethod ?? string.Empty,
                    row.PaymentReferenceNumber ?? string.Empty,
                    row.CashierName ?? string.Empty,
                    row.OrderSource,
                    row.TalabatOrderNumber ?? string.Empty,
                    row.PartnerName ?? string.Empty,
                    row.PartnerCode ?? string.Empty,
                    row.PartnerOrderNumber ?? string.Empty,
                    row.Status
                });
            }

            return csv.ToString();
        }

        private static void AppendCsvRow(StringBuilder csv, IEnumerable<string> values)
        {
            csv.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        private static string EscapeCsv(string value)
        {
            return value.IndexOfAny(CsvSpecialChars) >= 0
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }

        private static OrderQueryFilter BuildFilter(OrderFilterRequest request, Guid tenantId, Guid? branchId)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateTenant(tenantId);
            ValidatePaging(request);

            var (dateFrom, dateTo) = NormalizeDateRange(request);
            var sort = NormalizeSort(request.Sort);

            return new OrderQueryFilter
            {
                TenantId = tenantId,
                BranchId = branchId,
                PageNumber = request.Page,
                PageSize = request.PageSize,
                Status = ParseEnum<OrderStatus>(request.Status, "Status"),
                Payment = NormalizeOptional(request.Payment, MaxPaymentLength, "Payment"),
                OrderType = ParseEnum<OrderType>(request.Type, "Type"),
                OrderSource = ParseEnum<OrderSource>(request.Source, "Source"),
                PartnerId = request.PartnerId,
                VoucherApplied = request.VoucherApplied,
                DateFrom = dateFrom,
                DateTo = dateTo,
                Search = NormalizeOptional(request.Search, MaxSearchLength, "Search"),
                SortDescending = sort.Equals(SortNewest, StringComparison.OrdinalIgnoreCase)
            };
        }

        private static void ValidateTenant(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ValidationException("Tenant is required.");
        }

        private static void ValidateBranch(Guid branchId)
        {
            if (branchId == Guid.Empty)
                throw new ValidationException("Branch is required.");
        }

        private static void ValidatePaging(OrderFilterRequest request)
        {
            if (request.Page < 1)
                throw new ValidationException("Page must be greater than zero.");

            if (request.PageSize < 1 || request.PageSize > MaxPageSize)
                throw new ValidationException($"PageSize must be between 1 and {MaxPageSize}.");
        }

        private static string NormalizeSort(string? sort)
        {
            var normalized = string.IsNullOrWhiteSpace(sort) ? SortNewest : sort.Trim();

            return normalized.Equals(SortNewest, StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals(SortOldest, StringComparison.OrdinalIgnoreCase)
                ? normalized
                : throw new ValidationException("Sort must be newest or oldest.");
        }

        private static (DateTime? DateFrom, DateTime? DateTo) NormalizeDateRange(OrderFilterRequest request)
        {
            var dateFrom = NormalizeUtc(request.DateFrom);
            var dateTo = NormalizeUtc(request.DateTo);

            if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value >= dateTo.Value)
                throw new ValidationException("DateFrom must be before DateTo.");

            return (dateFrom, dateTo);
        }

        private static TEnum? ParseEnum<TEnum>(string? value, string fieldName)
            where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals("all", StringComparison.OrdinalIgnoreCase))
                return null;

            return Enum.TryParse<TEnum>(value.Trim(), true, out var parsed)
                ? parsed
                : throw new ValidationException($"{fieldName} filter is invalid.");
        }

        private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals("all", StringComparison.OrdinalIgnoreCase))
                return null;

            var normalized = value.Trim();
            if (normalized.Length > maxLength)
                throw new ValidationException($"{fieldName} filter is too long.");

            return normalized;
        }

        private static DateTime? NormalizeUtc(DateTime? value)
        {
            if (!value.HasValue)
                return null;

            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
        }
    }
}
