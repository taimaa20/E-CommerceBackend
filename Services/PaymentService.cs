using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Hubs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Services.Caching;

namespace RestaurantPos.Api.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _repository;
        private readonly ICacheService _cache;
        private readonly IVoucherService _voucherService;
        private readonly IMediator _mediator;
        private readonly IHubContext<KitchenHub> _hubContext;
        private readonly INotificationService _notificationService;
        private readonly IBranchContext _branchContext;
        private readonly IBranchConfigurationService _branchConfigurationService;
        private readonly ILogger<PaymentService> _logger;
        private static readonly JsonSerializerOptions CostSharingJsonOptions = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public PaymentService(
            IPaymentRepository repository,
            ICacheService cache,
            IVoucherService voucherService,
            IMediator mediator,
            IHubContext<KitchenHub> hubContext,
            INotificationService notificationService,
            IBranchContext branchContext,
            IBranchConfigurationService branchConfigurationService,
            ILogger<PaymentService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _voucherService = voucherService ?? throw new ArgumentNullException(nameof(voucherService));
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PaymentProcessResult> AddPaymentAsync(
            CreatePaymentRequest request,
            Guid? userId,
            UserRole userRole,
            CancellationToken cancellationToken = default)
        {
            var branchId = (await _branchContext.GetCurrentAsync(cancellationToken)).CurrentBranch.Id;
            return await AddPaymentForBranchAsync(
                request,
                userId,
                userRole,
                branchId,
                cancellationToken);
        }

        public Task<PaymentProcessResult> AddOnlinePaymentAsync(
            CreatePaymentRequest request,
            Guid branchId,
            CancellationToken cancellationToken = default)
            => AddPaymentForBranchAsync(
                request,
                null,
                UserRole.Cashier,
                branchId,
                cancellationToken);

        private async Task<PaymentProcessResult> AddPaymentForBranchAsync(
            CreatePaymentRequest request,
            Guid? userId,
            UserRole userRole,
            Guid branchId,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.OrderId == Guid.Empty)
                throw new InvalidOperationException("OrderId is required.");

            if (string.IsNullOrWhiteSpace(request.Method))
                throw new InvalidOperationException("Payment method is required.");

            ValidateTenderedAmount(request);

            var order = await _repository.GetOrderForPaymentAsync(request.OrderId, cancellationToken);
            if (order == null)
                throw new KeyNotFoundException("Order not found.");

            if (order.BranchId != branchId)
                throw new KeyNotFoundException("Order not found.");

            if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Completed)
                throw new InvalidOperationException("This order cannot accept payments.");

            if (OrderPaymentHelper.IsPaid(order) &&
                !(order.Payments?.Any() ?? false))
                throw new InvalidOperationException("Order is already paid.");

            if (userRole == UserRole.Waiter && order.OrderType != OrderType.Takeaway)
                throw new UnauthorizedAccessException("Waiters can only collect payment for takeaway orders.");

            ValidateRates(request, order);
            var clientActionId = NormalizeClientActionId(request.ClientActionId);
            var replayedPayment = FindExistingPayment(order, clientActionId);
            if (replayedPayment != null)
            {
                _logger.LogInformation(
                    "Payment replay detected for order {OrderId} and client action {ClientActionId}",
                    order.Id,
                    clientActionId);
                return BuildReplayResult(order, replayedPayment);
            }

            await using var transaction = await _repository.BeginTransactionAsync(cancellationToken);
            Payment payment;
            decimal paidAmount;
            decimal remainingAmount;
            bool becamePaid;
            string? paymentMethodSummary;
            VoucherApplicationResult? voucherApplication = null;

            try
            {
                // P3 (overpayment guard): acquire a row-level lock on the
                // Orders row before reading committed payments. This
                // serializes concurrent payment transactions (cross-tab,
                // replay race, duplicate attempt) so the sum-check below
                // operates on a stable view. Without this lock, two
                // transactions at READ COMMITTED isolation could each see
                // a remaining balance of $0 still available and both
                // commit, silently overpaying the order.
                await _repository.LockOrderRowAsync(order.Id, cancellationToken);

                order = await _repository.GetOrderForPaymentAsync(request.OrderId, cancellationToken)
                    ?? throw new KeyNotFoundException("Order was deleted during payment processing.");

                if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Completed)
                    throw new InvalidOperationException("This order cannot accept payments.");

                replayedPayment = FindExistingPayment(order, clientActionId);
                if (replayedPayment != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    _logger.LogInformation(
                        "Concurrent payment replay reconciled for order {OrderId} and client action {ClientActionId}",
                        order.Id,
                        clientActionId);
                    return BuildReplayResult(order, replayedPayment);
                }

                ValidateRates(request, order);
                PrepareOrderForPayment(order, request);
                if (request.ApplyVoucher)
                    voucherApplication = await _voucherService.ApplyToOrderAsync(
                        order,
                        userId,
                        order.TotalAmount,
                        cancellationToken);

                var paymentSnapshot = OrderPaymentHelper.BuildSnapshot(order);
                var remainingBeforePayment = paymentSnapshot.RemainingAmount;
                var paymentAmount = ResolvePaymentAmount(
                    request.Amount,
                    remainingBeforePayment,
                    request.ApplyVoucher && remainingBeforePayment <= 0);

                if (paymentAmount > remainingBeforePayment)
                    throw new InvalidOperationException("Payment amount exceeds the remaining balance.");

                // P3 (overpayment guard, second check): cross-validate
                // against the actual committed sum in the database. The
                // order snapshot we have was loaded AsNoTracking before
                // the lock — a payment that committed between that load
                // and the lock acquisition would not appear in
                // order.Payments. The DB sum is authoritative.
                var committedPaid = await _repository.GetTotalPaidAmountAsync(order.Id, cancellationToken);
                var overpaymentTolerance = 0.01m; // 1-unit currency rounding allowance
                if (committedPaid + paymentAmount > order.TotalAmount + overpaymentTolerance)
                    throw new InvalidOperationException(
                        $"Payment would exceed the order total. Already paid: {committedPaid:F2}, attempted: {paymentAmount:F2}, total: {order.TotalAmount:F2}.");

                if (request.AmountTendered.HasValue &&
                    OrderPaymentHelper.IsCashMethod(request.Method) &&
                    OrderPaymentHelper.RoundCurrency(request.AmountTendered.Value) < paymentAmount)
                    throw new InvalidOperationException("Amount tendered cannot be less than the payment amount.");

                var selectedPaymentMethod = await ResolvePaymentMethodAsync(request, order.TenantId, order.BranchId, cancellationToken);
                var referenceNumber = ResolveReferenceNumber(request, selectedPaymentMethod);
                var paymentCostSharing = CalculatePaymentCostSharing(
                    paymentAmount,
                    selectedPaymentMethod,
                    request.PaymentMethodCostSharingOverride,
                    userRole);

                var paidAt = ResolvePaidAt(
                    string.IsNullOrWhiteSpace(order.PublicOrderNumber)
                        ? null
                        : request.PaidAt);
                payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    TenantId = order.TenantId,
                    BranchId = order.BranchId,
                    OrderId = order.Id,
                    ClientActionId = clientActionId,
                    Amount = paymentAmount,
                    Method = request.Method.Trim(),
                    PaymentMethodId = selectedPaymentMethod?.Id,
                    PaymentMethodName = selectedPaymentMethod?.NameEn,
                    PaymentMethodNameAr = selectedPaymentMethod?.NameAr,
                    PaymentMethodCode = selectedPaymentMethod?.Code,
                    ReferenceNumber = referenceNumber,
                    CreatedAt = paidAt,
                    CreatedByUserId = userId
                };
                ApplyPaymentCostSharing(payment, paymentCostSharing);

                order.Payments ??= new List<Payment>();
                order.Payments.Add(payment);

                if (!string.IsNullOrWhiteSpace(request.CustomerPhone))
                    order.CustomerPhone = request.CustomerPhone.Trim();

                if (request.AmountTendered.HasValue && OrderPaymentHelper.IsCashMethod(payment.Method))
                {
                    var tenderedAmount = OrderPaymentHelper.RoundCurrency(request.AmountTendered.Value);
                    var paymentChange = OrderPaymentHelper.RoundCurrency(Math.Max(0m, tenderedAmount - paymentAmount));
                    order.AmountTendered = OrderPaymentHelper.RoundCurrency((order.AmountTendered ?? 0m) + tenderedAmount);
                    order.ChangeAmount = OrderPaymentHelper.RoundCurrency((order.ChangeAmount ?? 0m) + paymentChange);
                }

                var updatedSnapshot = OrderPaymentHelper.BuildSnapshot(order);
                paidAmount = updatedSnapshot.PaidAmount;
                remainingAmount = updatedSnapshot.RemainingAmount;
                becamePaid = updatedSnapshot.IsPaid || (request.ApplyVoucher && remainingAmount <= 0);
                paymentMethodSummary = becamePaid
                    ? OrderPaymentHelper.BuildPaymentMethodSummary(order.Payments.Select(p => p.Method))
                    : order.PaymentMethod;

                if (becamePaid)
                {
                    order.PaymentMethod = paymentMethodSummary;
                    order.PaidAt = paidAt;
                    order.PaidByUserId = userId;
                    ApplyOrderCostSharing(order, request.DeliveryPartnerCostSharingOverride, userRole);
                    OrderCompletion.ApplyStatus(order);
                }

                order.SyncedAt = DateTime.UtcNow;

                var rowsAffected = await _repository.UpdateOrderPaymentFieldsAsync(order, cancellationToken);
                if (rowsAffected == 0)
                    throw new KeyNotFoundException("Order was deleted during payment processing.");

                await _repository.InsertPaymentAsync(payment, cancellationToken);

                if (becamePaid && order.TableId.HasValue)
                    await _repository.UpdateTableStatusAsync(order.TableId.Value, TableStatus.Reserved, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            try
            {
                await InvalidateOrderCachesAsync(order.TenantId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Payment for order {OrderId} committed but cache invalidation failed", order.Id);
            }

            if (becamePaid)
            {
                try
                {
                    await HandleOrderPaidSideEffectsAsync(order, paymentMethodSummary ?? payment.Method, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Payment {PaymentId} for order {OrderId} committed but paid-order side effects failed",
                        payment.Id,
                        order.Id);
                }
            }

            return new PaymentProcessResult
            {
                Order = order,
                Payment = payment,
                PaidAmount = paidAmount,
                RemainingAmount = remainingAmount,
                PaymentStatus = becamePaid
                    ? OrderPaymentHelper.PaidStatus
                    : paidAmount > 0
                        ? OrderPaymentHelper.PartialStatus
                        : OrderPaymentHelper.PendingStatus,
                BecamePaid = becamePaid,
                PaymentMethodSummary = paymentMethodSummary,
                VoucherRemainingToday = voucherApplication?.RemainingToday
            };
        }

        private static void ValidateRates(CreatePaymentRequest request, Order order)
        {
            if ((order.Payments?.Any() ?? false))
            {
                if (request.ServiceChargeRate.HasValue && request.ServiceChargeRate.Value != order.ServiceChargeRate)
                    throw new InvalidOperationException("Service charge rate cannot change after payment has started.");

                if (request.TaxRate.HasValue && request.TaxRate.Value != order.TaxRate)
                    throw new InvalidOperationException("Tax rate cannot change after payment has started.");
            }
        }

        private static void ValidateTenderedAmount(CreatePaymentRequest request)
        {
            if (request.AmountTendered.HasValue && !OrderPaymentHelper.IsCashMethod(request.Method))
                throw new InvalidOperationException("Amount tendered is only valid for cash payments.");
        }

        private static CostSharingCalculation? CalculatePaymentCostSharing(
            decimal paymentAmount,
            PaymentMethod? method,
            CostSharingOverrideDto? costSharingOverride,
            UserRole userRole)
        {
            if (method == null)
            {
                if (costSharingOverride != null)
                    throw new InvalidOperationException("Payment-method cost sharing override requires a card payment method.");

                return null;
            }

            var rule = BuildPaymentMethodRule(method, costSharingOverride, userRole);
            return CostSharingHelper.Calculate(paymentAmount, rule);
        }

        private static CostSharingRuleSnapshot BuildPaymentMethodRule(
            PaymentMethod method,
            CostSharingOverrideDto? costSharingOverride,
            UserRole userRole)
        {
            var mode = method.CostSharingMode;
            var restaurantPercentage = method.CostSharingRestaurantPercentage;
            var counterpartyPercentage = method.CostSharingCounterpartyPercentage;

            if (costSharingOverride != null)
            {
                EnsurePerOrderOverrideAllowed(method.CostSharingScope, userRole, costSharingOverride);
                mode = costSharingOverride.Mode;
                restaurantPercentage = costSharingOverride.RestaurantPercentage;
                counterpartyPercentage = costSharingOverride.CounterpartyPercentage;
            }

            var percentages = CostSharingHelper.NormalizePercentages(
                mode,
                restaurantPercentage,
                counterpartyPercentage);

            return new CostSharingRuleSnapshot(
                CostSharingTargetType.PaymentMethod,
                method.Id,
                method.NameEn,
                method.Code,
                mode,
                method.CostSharingScope,
                method.CostSharingCommissionPercentage,
                percentages.RestaurantPercentage,
                percentages.CounterpartyPercentage);
        }

        private static void ApplyPaymentCostSharing(Payment payment, CostSharingCalculation? calculation)
        {
            if (calculation == null)
                return;

            payment.CostSharingMode = calculation.Rule.Mode;
            payment.CostSharingScope = calculation.Rule.Scope;
            payment.CostSharingCommissionPercentage = calculation.Rule.TotalCommissionPercentage;
            payment.CostSharingRestaurantPercentage = calculation.Rule.RestaurantPercentage;
            payment.CostSharingCounterpartyPercentage = calculation.Rule.CounterpartyPercentage;
            payment.CostSharingCommissionAmount = calculation.TotalCommission;
            payment.CostSharingRestaurantShareAmount = calculation.RestaurantShare;
            payment.CostSharingCounterpartyShareAmount = calculation.CounterpartyShare;
        }

        private static void ApplyOrderCostSharing(
            Order order,
            CostSharingOverrideDto? deliveryPartnerOverride,
            UserRole userRole)
        {
            var paymentDetails = BuildPaymentCostSharingDetails(order.Payments ?? new List<Payment>());
            var deliveryCalculation = CalculateDeliveryPartnerCostSharing(order, deliveryPartnerOverride, userRole);
            var details = new List<CostSharingCalculation>(paymentDetails);

            if (deliveryCalculation != null)
                details.Add(deliveryCalculation);

            order.CostSharingTotalCommission = CostSharingHelper.RoundCurrency(
                details.Sum(d => d.TotalCommission));
            order.CostSharingRestaurantShare = CostSharingHelper.RoundCurrency(
                details.Sum(d => d.RestaurantShare));
            order.CostSharingCounterpartyShare = CostSharingHelper.RoundCurrency(
                details.Sum(d => d.CounterpartyShare));
            order.CostSharingNetSettlement = CostSharingHelper.RoundCurrency(
                Math.Max(0m, order.TotalAmount - order.CostSharingTotalCommission));
            order.CostSharingDetailsJson = SerializeCostSharingDetails(details);
            order.CostSharingCalculatedAt = DateTime.UtcNow;
        }

        private static List<CostSharingCalculation> BuildPaymentCostSharingDetails(IEnumerable<Payment> payments)
        {
            return payments
                .Where(p => p.CostSharingCommissionAmount > 0m)
                .Select(p => new CostSharingCalculation(
                    new CostSharingRuleSnapshot(
                        CostSharingTargetType.PaymentMethod,
                        p.PaymentMethodId,
                        p.PaymentMethodName,
                        p.PaymentMethodCode,
                        p.CostSharingMode,
                        p.CostSharingScope,
                        p.CostSharingCommissionPercentage,
                        p.CostSharingRestaurantPercentage,
                        p.CostSharingCounterpartyPercentage),
                    p.Amount,
                    p.CostSharingCommissionAmount,
                    p.CostSharingRestaurantShareAmount,
                    p.CostSharingCounterpartyShareAmount))
                .ToList();
        }

        private static CostSharingCalculation? CalculateDeliveryPartnerCostSharing(
            Order order,
            CostSharingOverrideDto? costSharingOverride,
            UserRole userRole)
        {
            if (order.OrderSource != OrderSource.DeliveryPartner || order.DeliveryPartner == null)
            {
                if (costSharingOverride != null)
                    throw new InvalidOperationException("Delivery partner cost sharing override requires a delivery partner order.");

                return null;
            }

            var partner = order.DeliveryPartner;
            var mode = partner.CostSharingMode;
            var restaurantPercentage = partner.CostSharingRestaurantPercentage;
            var counterpartyPercentage = partner.CostSharingCounterpartyPercentage;

            if (costSharingOverride != null)
            {
                EnsurePerOrderOverrideAllowed(partner.CostSharingScope, userRole, costSharingOverride);
                mode = costSharingOverride.Mode;
                restaurantPercentage = costSharingOverride.RestaurantPercentage;
                counterpartyPercentage = costSharingOverride.CounterpartyPercentage;
            }

            var percentages = CostSharingHelper.NormalizePercentages(
                mode,
                restaurantPercentage,
                counterpartyPercentage);
            var rule = new CostSharingRuleSnapshot(
                CostSharingTargetType.DeliveryPartner,
                partner.Id,
                partner.Name,
                partner.Code,
                mode,
                partner.CostSharingScope,
                partner.CostSharingCommissionPercentage,
                percentages.RestaurantPercentage,
                percentages.CounterpartyPercentage);

            return CostSharingHelper.Calculate(order.TotalAmount, rule);
        }

        private static void EnsurePerOrderOverrideAllowed(
            CostSharingScope scope,
            UserRole userRole,
            CostSharingOverrideDto costSharingOverride)
        {
            if (scope != CostSharingScope.PerOrder)
                throw new InvalidOperationException("Per-order cost sharing is not enabled for this configuration.");

            if (userRole is not (UserRole.Admin or UserRole.Manager or UserRole.Owner))
                throw new UnauthorizedAccessException("You do not have permission to override cost sharing.");

            if (costSharingOverride.Mode == CostSharingMode.Shared &&
                !CostSharingHelper.SharedPercentagesTotalOneHundred(
                    costSharingOverride.RestaurantPercentage,
                    costSharingOverride.CounterpartyPercentage))
            {
                throw new InvalidOperationException("Cost sharing percentages must total 100%.");
            }
        }

        private static string? SerializeCostSharingDetails(IReadOnlyCollection<CostSharingCalculation> details)
        {
            if (details.Count == 0)
                return null;

            var payload = details.Select(d => new
            {
                TargetType = d.Rule.TargetType.ToString(),
                d.Rule.TargetId,
                d.Rule.TargetName,
                d.Rule.TargetCode,
                Mode = d.Rule.Mode.ToString(),
                Scope = d.Rule.Scope.ToString(),
                d.Rule.TotalCommissionPercentage,
                d.Rule.RestaurantPercentage,
                d.Rule.CounterpartyPercentage,
                d.GrossAmount,
                d.TotalCommission,
                d.RestaurantShare,
                d.CounterpartyShare
            });

            return JsonSerializer.Serialize(payload, CostSharingJsonOptions);
        }

        private async Task<PaymentMethod?> ResolvePaymentMethodAsync(
            CreatePaymentRequest request,
            Guid tenantId,
            Guid branchId,
            CancellationToken cancellationToken)
        {
            if (OrderPaymentHelper.IsCashMethod(request.Method))
                return null;

            if (!request.PaymentMethodId.HasValue || request.PaymentMethodId.Value == Guid.Empty)
                throw new InvalidOperationException("Card payments require a payment method.");

            var activeMethods = await _cache.GetOrCreateAsync(
                CacheKeys.PaymentMethodsActive(tenantId),
                () => _repository.GetActivePaymentMethodsAsync(tenantId, cancellationToken),
                TimeSpan.FromMinutes(15),
                TimeSpan.FromHours(1),
                cancellationToken) ?? new List<PaymentMethod>();

            var selectedMethod = activeMethods.FirstOrDefault(m => m.Id == request.PaymentMethodId.Value)
                ?? throw new InvalidOperationException("Selected payment method is not active.");

            var enabledMethodIds = await _branchConfigurationService.GetEnabledPaymentMethodIdsAsync(branchId, cancellationToken);
            if (!enabledMethodIds.Contains(selectedMethod.Id))
                throw new InvalidOperationException("Selected payment method is not enabled for the current branch.");

            if (IsCashPaymentMethod(selectedMethod))
                throw new InvalidOperationException("Card payments require a non-cash payment method.");

            return selectedMethod;
        }

        private static bool IsCashPaymentMethod(PaymentMethod method)
            => string.Equals(method.Code?.Trim(), "CASH", StringComparison.OrdinalIgnoreCase);

        private static string? ResolveReferenceNumber(CreatePaymentRequest request, PaymentMethod? paymentMethod)
        {
            if (paymentMethod == null)
                return null;

            var referenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber)
                ? null
                : request.ReferenceNumber.Trim();

            if (paymentMethod.RequiresReferenceNumber && referenceNumber == null)
                throw new InvalidOperationException("Reference number is required for the selected payment method.");

            if (referenceNumber?.Length > 120)
                throw new InvalidOperationException("Reference number is too long.");

            return referenceNumber;
        }

        private static Payment? FindExistingPayment(Order order, string? clientActionId)
        {
            if (clientActionId == null)
                return null;

            return order.Payments?.FirstOrDefault(p =>
                string.Equals(p.ClientActionId, clientActionId, StringComparison.Ordinal));
        }

        private static PaymentProcessResult BuildReplayResult(Order order, Payment payment)
        {
            var snapshot = OrderPaymentHelper.BuildSnapshot(order);
            return new PaymentProcessResult
            {
                Order = order,
                Payment = payment,
                PaidAmount = snapshot.PaidAmount,
                RemainingAmount = snapshot.RemainingAmount,
                PaymentStatus = snapshot.PaymentStatus,
                BecamePaid = snapshot.IsPaid,
                PaymentMethodSummary = snapshot.PaymentMethod
            };
        }

        private static string? NormalizeClientActionId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim();
            if (normalized.Length > 120)
                throw new InvalidOperationException("Client action id is too long.");

            return normalized;
        }

        private static void ApplyRateOverrides(Order order, CreatePaymentRequest request)
        {
            if (request.ServiceChargeRate.HasValue)
                order.ServiceChargeRate = request.ServiceChargeRate.Value;

            if (request.TaxRate.HasValue)
                order.TaxRate = request.TaxRate.Value;
        }

        private static void PrepareOrderForPayment(Order order, CreatePaymentRequest request)
        {
            if (!string.IsNullOrWhiteSpace(order.PublicOrderNumber))
                return;

            ApplyRateOverrides(order, request);
            RecalculateOrderTotals(order);
        }

        private static DateTime ResolvePaidAt(DateTime? paidAt)
        {
            if (!paidAt.HasValue)
                return DateTime.UtcNow;

            return paidAt.Value.Kind switch
            {
                DateTimeKind.Utc => paidAt.Value,
                DateTimeKind.Local => paidAt.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(paidAt.Value, DateTimeKind.Utc)
            };
        }

        private static decimal ResolvePaymentAmount(decimal? requestedAmount, decimal remainingAmount, bool allowZero)
        {
            var paymentAmount = OrderPaymentHelper.RoundCurrency(requestedAmount ?? remainingAmount);

            if (paymentAmount < 0)
                throw new InvalidOperationException("Payment amount cannot be negative.");

            if (paymentAmount == 0 && !allowZero)
                throw new InvalidOperationException("Payment amount must be greater than 0.");

            return paymentAmount;
        }

        private static void RecalculateOrderTotals(Order order)
        {
            var totals = CalculateOrderTotals(
                order.OrderItems,
                order.DiscountPercentage,
                order.DiscountGroupType,
                order.DiscountGroupValue,
                order.ServiceChargeRate,
                order.TaxRate);

            order.TotalAmount = totals.Total + totals.ServiceChargeAmount + totals.TaxAmount + GetAdditionalFeeTotal(order);
            order.Subtotal = totals.Subtotal;
            order.DiscountAmount = totals.DiscountAmount;
            order.DiscountGroupAmount = totals.DiscountGroupAmount;
            order.ServiceChargeAmount = totals.ServiceChargeAmount;
            order.TaxAmount = totals.TaxAmount;

            if (order.IsVoucherApplied)
                order.TotalAmount = OrderPaymentHelper.RoundCurrency(Math.Max(0m, order.TotalAmount - order.VoucherDiscountAmount));

            DeliveryAccountingHelper.Apply(order);
        }

        private async Task HandleOrderPaidSideEffectsAsync(Order order, string paymentMethod, CancellationToken cancellationToken)
        {
            try
            {
                await _mediator.Publish(new OrderPaidEvent(order.Id, order.TotalAmount, paymentMethod, order.PaidByUserId), cancellationToken);

                await _hubContext.Clients.Group(KitchenHubGroups.Branch(order.BranchId)).SendAsync(KitchenHubEvents.OrderPaid, new
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    TableName = order.TableName,
                    TableId = order.TableId,
                    Status = order.Status.ToString(),
                    OrderType = order.OrderType.ToString(),
                    TotalAmount = order.TotalAmount,
                    PaymentMethod = paymentMethod
                }, cancellationToken);

                _ = _notificationService.SendAsync(
                    NotificationType.OrderPaid,
                    $"Order #{order.OrderNumber} Paid",
                    $"{order.TableName} — {paymentMethod} ({order.TotalAmount:F2})",
                    order.Id.ToString(),
                    "Cashier,Waiter");

                if (order.OrderType is OrderType.Takeaway or OrderType.Delivery)
                {
                    var channelLabel = ResolveOrderChannelLabel(order);
                    await _hubContext.Clients.Group(KitchenHubGroups.Branch(order.BranchId)).SendAsync(KitchenHubEvents.ReceiveNewOrder, MapOrderToDto(order), cancellationToken);
                    _ = _notificationService.SendAsync(
                        NotificationType.NewOrder,
                        $"New {channelLabel} Order #{order.OrderNumber}",
                        $"{channelLabel} — Paid ({paymentMethod})",
                        order.Id.ToString(),
                        "Kitchen");

                    // Takeaway/Delivery use a payment-first flow: kitchen tickets must be enqueued
                    // here (not at order creation) so items only print after payment is
                    // confirmed. Mirrors the Dine-in publish in OrdersController.CreateOrder.
                    // Fire-and-forget: handler creates its own DI scope; awaiting would
                    // add ticket-build + INSERT latency to the payment response.
                    _ = _mediator.Publish(
                        new OrderCreatedForPrintingEvent(order.Id, order.TenantId, order.PaidByUserId),
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Post-payment notifications failed for order {OrderId} after successful payment.", order.Id);
            }
        }

        private async Task InvalidateOrderCachesAsync(Guid tenantId)
        {
            await _cache.RemoveAsync(CacheKeys.OrdersCashierToday(tenantId));
            await _cache.RemoveAsync(CacheKeys.OrdersCashierAll(tenantId));
            await _cache.RemoveAsync(CacheKeys.KitchenToday(false));
            await _cache.RemoveAsync(CacheKeys.KitchenToday(true));
            await _cache.RemoveAsync(CacheKeys.KitchenAll(false));
            await _cache.RemoveAsync(CacheKeys.KitchenAll(true));
            await _cache.RemoveByPatternAsync(CacheKeys.TakeawayActiveOrdersPattern(tenantId));
            await _cache.RemoveByPatternAsync($"pos:orders:cashier:*:{tenantId}:*");
            await _cache.RemoveByPatternAsync("pos:orders:kitchen:*:*:*");
        }

        private static OrderDto MapOrderToDto(Order order)
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
                TicketId = order.TicketId,
                TableName = ResolveTableName(order),
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
                NetRestaurantRevenue = order.NetRestaurantRevenue,
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
                Status = order.Status.ToString(),
                IsPaid = paymentSnapshot.IsPaid,
                PaymentStatus = paymentSnapshot.PaymentStatus,
                PaymentMethod = paymentSnapshot.PaymentMethod,
                CashierName = !string.IsNullOrWhiteSpace(order.PaidByUser?.FullName) ? order.PaidByUser.FullName : order.PaidByUser?.Username,
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
                LastUpdatedAt = order.LastUpdatedAt,
                SyncedAt = order.SyncedAt,
                Payments = paymentSnapshot.Payments,
                Items = order.OrderItems.Select(MapOrderItemToDto).ToList(),
                HasPartnerDiscountOverride = order.OrderItems.Any(i => i.HasPartnerDiscountOverride)
            };
        }

        private static string ResolveOrderNumber(Order order)
            => string.IsNullOrWhiteSpace(order.PublicOrderNumber)
                ? order.OrderNumber
                : order.PublicOrderNumber;

        private static decimal GetAdditionalFeeTotal(Order order)
            => (order.DeliveryFee ?? 0m)
                + (order.TalabatDeliveryFee ?? 0m)
                + (order.TalabatServiceFee ?? 0m);

        private static string ResolveOrderChannelLabel(Order order)
        {
            if (order.OrderSource == OrderSource.Talabat)
                return "Talabat";

            if (order.OrderSource == OrderSource.DeliveryPartner)
                return ResolvePartnerDisplayName(order);

            return order.OrderType == OrderType.Delivery ? "Delivery" : "Takeaway";
        }

        private static string ResolveTableName(Order order)
        {
            if (order.OrderSource == OrderSource.Talabat)
                return "TALABAT";

            if (order.OrderSource == OrderSource.DeliveryPartner)
                return ResolvePartnerDisplayName(order);

            return order.OrderType == OrderType.Takeaway ? "TAKEAWAY" : order.OrderType == OrderType.Delivery ? "DELIVERY" : order.TableName;
        }

        private static string ResolvePartnerDisplayName(Order order)
            => !string.IsNullOrWhiteSpace(order.DeliveryPartnerName)
                ? order.DeliveryPartnerName
                : !string.IsNullOrWhiteSpace(order.DeliveryPartnerCode)
                    ? order.DeliveryPartnerCode
                    : "DELIVERY PARTNER";

        private static List<OrderOfferProductDto> MapOrderOfferProducts(Offer? offer)
        {
            if (offer?.OfferProducts == null)
                return new List<OrderOfferProductDto>();

            return offer.OfferProducts
                .OrderBy(op => op.Id)
                .Select(op => new OrderOfferProductDto
                {
                    ProductId = op.ProductId,
                    Name = op.Product?.Name ?? string.Empty,
                    NameAr = op.Product?.NameAr ?? string.Empty,
                    Quantity = op.Quantity
                })
                .ToList();
        }

        private static OrderItemDto MapOrderItemToDto(OrderItem item)
        {
            return new OrderItemDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                OfferLineId = item.OfferLineId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.IsComplimentary ? 0 : OrderPaymentHelper.RoundCurrency(item.Price),
                LineTotalAmount = CalcLineTotalAmount(item),
                UnitPriceSnapshot = item.UnitPriceSnapshot,
                LineTotalSnapshot = item.LineTotalSnapshot,
                PartnerPriceSnapshot = item.PartnerPriceSnapshot,
                HasPartnerDiscountOverride = item.HasPartnerDiscountOverride,
                PartnerOriginalUnitPrice = item.PartnerOriginalUnitPrice,
                PartnerDiscountedUnitPrice = item.PartnerDiscountedUnitPrice,
                PartnerDiscountReason = item.PartnerDiscountReason,
                PartnerDiscountUpdatedAt = item.PartnerDiscountUpdatedAt,
                PartnerDiscountUpdatedBy = item.PartnerDiscountUpdatedBy,
                Notes = item.Notes,
                IsComplimentary = item.IsComplimentary,
                ItemStatus = item.IsReady ? "Ready" : "Prepared",
                IsNewlyAdded = item.IsNewlyAdded,
                PreparationStation = item.Product != null ? (int)item.Product.PreparationStation : 0,
                Modifiers = (item.Modifiers ?? Enumerable.Empty<OrderItemModifier>()).Select(MapModifierDto).ToList()
            };
        }

        private static OrderItemModifierDto MapModifierDto(OrderItemModifier modifier)
        {
            return new OrderItemModifierDto
            {
                ModifierName = modifier.ModifierName,
                ModifierNameAr = modifier.ModifierNameAr,
                Price = modifier.Price,
                Quantity = modifier.Quantity
            };
        }

        private static decimal CalcLineTotalAmount(OrderItem item)
        {
            var modifiersTotal = (item.Modifiers ?? Enumerable.Empty<OrderItemModifier>())
                .Sum(m => m.Price * m.Quantity);

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
            var total = afterAllDiscounts;

            return new OrderTotals(subtotal, discountAmount, discountGroupAmount, serviceChargeAmount, taxAmount, total);
        }
    }
}
