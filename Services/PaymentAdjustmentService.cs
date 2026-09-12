using MediatR;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    public sealed class PaymentAdjustmentService : IPaymentAdjustmentService
    {
        private const string CashName = "Cash";
        private const string CashNameAr = "نقدي";
        private static readonly IReadOnlyDictionary<string, string> ReasonLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PaymentAdjustmentReasonCodes.WrongPaymentType] = "Wrong payment type selected",
                [PaymentAdjustmentReasonCodes.CustomerChangedMethod] = "Customer changed payment method",
                [PaymentAdjustmentReasonCodes.SplitPaymentCorrection] = "Split payment correction",
                [PaymentAdjustmentReasonCodes.CashByMistake] = "Cash entered by mistake",
                [PaymentAdjustmentReasonCodes.CardByMistake] = "Card entered by mistake"
            };

        private readonly IPaymentAdjustmentRepository _repository;
        private readonly IMediator _mediator;
        private readonly INotificationService _notificationService;
        private readonly IBranchContext _branchContext;
        private readonly IBranchConfigurationService _branchConfigurationService;
        private readonly ILogger<PaymentAdjustmentService> _logger;

        public PaymentAdjustmentService(
            IPaymentAdjustmentRepository repository,
            IMediator mediator,
            INotificationService notificationService,
            IBranchContext branchContext,
            IBranchConfigurationService branchConfigurationService,
            ILogger<PaymentAdjustmentService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PaymentAdjustmentResultDto> RequestAsync(
            Guid orderId,
            PaymentAdjustmentCreateDto request,
            Guid requesterId,
            UserRole requesterRole,
            CancellationToken ct)
        {
            ValidateRequest(request, requesterId, requesterRole);
            var requester = await ResolveUserAsync(requesterId, ct);

            await using var transaction = await _repository.BeginTransactionAsync(ct);
            await _repository.LockOrderAsync(orderId, ct);
            var order = await GetOrderAsync(orderId, ct);
            await EnsureCurrentBranchAsync(order, ct);
            if (await _repository.HasPendingAsync(orderId, ct))
                throw new ValidationException("This order already has a pending payment adjustment.");

            var targets = await BuildTargetsAsync(request, order.BranchId, ct);
            ValidateOrderAndChange(order, targets);
            var adjustment = BuildPendingAdjustment(order, request, requester, targets);
            await _repository.SaveRequestAsync(adjustment, ct);
            await transaction.CommitAsync(ct);

            await NotifyApproversAsync(order, adjustment);
            _logger.LogInformation(
                "Payment adjustment {AdjustmentId} requested for order {OrderId} by {RequesterId}",
                adjustment.Id,
                order.Id,
                requester.Id);
            return MapResult(order, adjustment, order.Payments);
        }

        public async Task<PaymentAdjustmentReviewDto> GetPendingAsync(
            Guid orderId,
            Guid adjustmentId,
            CancellationToken ct)
        {
            var adjustment = await _repository.GetPendingAsync(orderId, adjustmentId, ct)
                ?? throw new NotFoundException("Pending payment adjustment was not found.");
            await EnsureCurrentBranchAsync(adjustment.Order, ct);
            return MapReview(adjustment);
        }

        public async Task<PaymentAdjustmentResultDto> ApproveAsync(
            Guid orderId,
            Guid adjustmentId,
            PaymentAdjustmentDecisionDto request,
            Guid approverId,
            UserRole approverRole,
            CancellationToken ct)
        {
            ValidateApprover(approverId, approverRole);
            var approver = await ResolveApproverAsync(approverId, ct);

            await using var transaction = await _repository.BeginTransactionAsync(ct);
            await _repository.LockOrderAsync(orderId, ct);
            var adjustment = await LoadPendingAsync(orderId, adjustmentId, ct);
            var order = adjustment.Order;
            await EnsureCurrentBranchAsync(order, ct);
            ValidateOriginalPayments(order.Payments, adjustment.Details);

            var targets = await BuildTargetsAsync(adjustment.Details, order.BranchId, ct);
            var approvedAt = DateTime.UtcNow;
            var newPayments = BuildPayments(order, targets, approvedAt);
            AssignNewPaymentIds(adjustment.Details, targets, newPayments);
            ApplyDecision(adjustment, approver, PaymentAdjustmentStatus.Approved, request.ManagerNotes, approvedAt);

            var summary = OrderPaymentHelper.BuildPaymentMethodSummary(newPayments.Select(payment => payment.Method))
                ?? throw new ValidationException("At least one payment method is required.");
            await _repository.SaveApprovedAsync(
                order,
                order.Payments.ToList(),
                newPayments,
                adjustment,
                summary,
                approvedAt,
                ct);
            await transaction.CommitAsync(ct);

            await PublishInvalidationAsync(order.Id, order.TenantId, ct);
            LogDecision(adjustment, approverId);
            return MapResult(order, adjustment, newPayments, summary);
        }

        public async Task RejectAsync(
            Guid orderId,
            Guid adjustmentId,
            PaymentAdjustmentDecisionDto request,
            Guid approverId,
            UserRole approverRole,
            CancellationToken ct)
        {
            ValidateApprover(approverId, approverRole);
            var approver = await ResolveApproverAsync(approverId, ct);

            await using var transaction = await _repository.BeginTransactionAsync(ct);
            await _repository.LockOrderAsync(orderId, ct);
            var adjustment = await LoadPendingAsync(orderId, adjustmentId, ct);
            await EnsureCurrentBranchAsync(adjustment.Order, ct);
            ApplyDecision(
                adjustment,
                approver,
                PaymentAdjustmentStatus.Rejected,
                request.ManagerNotes,
                DateTime.UtcNow);
            await _repository.SaveDecisionAsync(ct);
            await transaction.CommitAsync(ct);
            LogDecision(adjustment, approverId);
        }

        public Task<IReadOnlyList<PaymentAdjustmentApproverDto>> GetApproversAsync(
            Guid requesterId,
            CancellationToken ct)
            => _repository.GetApproversAsync(requesterId, ct);

        public Task<PaginatedResponse<PaymentAdjustmentHistoryDto>> GetHistoryAsync(
            PaymentAdjustmentReportFilterDto filter,
            CancellationToken ct)
        {
            return GetHistoryInternalAsync(filter, ct);
        }

        private async Task<PaginatedResponse<PaymentAdjustmentHistoryDto>> GetHistoryInternalAsync(
            PaymentAdjustmentReportFilterDto filter,
            CancellationToken ct)
        {
            OrderType? orderType = null;
            if (!string.IsNullOrWhiteSpace(filter.OrderType))
            {
                if (!Enum.TryParse<OrderType>(filter.OrderType, true, out var parsedOrderType))
                    throw new ValidationException("Order type is invalid.");
                orderType = parsedOrderType;
            }

            var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
            return await _repository.GetHistoryAsync(new PaymentAdjustmentHistoryQuery(
                filter.Page,
                filter.PageSize,
                filter.DateFrom,
                filter.DateTo,
                filter.User,
                filter.Manager,
                filter.PaymentMethodId,
                orderType,
                branchId), ct);
        }

        private async Task<Order> GetOrderAsync(Guid orderId, CancellationToken ct)
            => await _repository.GetOrderAsync(orderId, ct)
                ?? throw new NotFoundException($"Order {orderId} was not found.");

        private async Task<User> ResolveUserAsync(Guid userId, CancellationToken ct)
            => await _repository.GetUserAsync(userId, ct)
                ?? throw new UnauthorizedException("The requesting user could not be resolved.");

        private async Task<User> ResolveApproverAsync(Guid userId, CancellationToken ct)
        {
            var user = await ResolveUserAsync(userId, ct);
            if (!(user.Role.IsAdminOrAbove() || user.Role is UserRole.Manager))
                throw new ForbiddenException("Manager approval is required.");
            return user;
        }

        private async Task<PaymentAdjustment> LoadPendingAsync(
            Guid orderId,
            Guid adjustmentId,
            CancellationToken ct)
            => await _repository.GetPendingAsync(orderId, adjustmentId, ct)
                ?? throw new NotFoundException("Pending payment adjustment was not found.");

        private static void ValidateRequest(
            PaymentAdjustmentCreateDto request,
            Guid requesterId,
            UserRole requesterRole)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (requesterId == Guid.Empty)
                throw new UnauthorizedException("The requesting user could not be resolved.");
            if (requesterRole is not (UserRole.Admin or UserRole.Manager or UserRole.Cashier))
                throw new ForbiddenException("You cannot request payment method changes.");
            if (request.Payments.Count == 0)
                throw new ValidationException("At least one new payment is required.");
            ResolveReason(request);
        }

        private static void ValidateApprover(Guid approverId, UserRole approverRole)
        {
            if (approverId == Guid.Empty)
                throw new UnauthorizedException("The approving user could not be resolved.");
            if (approverRole is not (UserRole.Admin or UserRole.Manager))
                throw new ForbiddenException("Manager approval is required.");
        }

        private static void ValidateOrderAndChange(Order order, IReadOnlyCollection<TargetAllocation> targets)
        {
            ValidateOrder(order);
            var oldTotal = OrderPaymentHelper.RoundCurrency(order.Payments.Sum(payment => payment.Amount));
            var newTotal = OrderPaymentHelper.RoundCurrency(targets.Sum(target => target.Amount));
            if (oldTotal != newTotal)
                throw new ValidationException("The adjusted payment total must equal the current payment total.");
            if (oldTotal != OrderPaymentHelper.RoundCurrency(order.TotalAmount))
                throw new ValidationException("The current payment total does not match the order total.");
            if (BuildOldSignature(order.Payments).SequenceEqual(BuildNewSignature(targets)))
                throw new ValidationException("The adjusted payments must contain at least one change.");
        }

        private static void ValidateOrder(Order order)
        {
            if (order.Status == OrderStatus.Cancelled)
                throw new ValidationException("Cancelled orders cannot be adjusted.");
            if (order.Payments.Count == 0)
                throw new ValidationException("The order has no payment records to adjust.");
            if (!OrderPaymentHelper.BuildSnapshot(order).IsPaid)
                throw new ValidationException("Payment methods can only be adjusted after payment completion.");
        }

        private async Task EnsureCurrentBranchAsync(Order order, CancellationToken ct)
        {
            var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
            if (order.BranchId != branchId)
                throw new NotFoundException($"Order {order.Id} was not found.");
        }

        private async Task<List<TargetAllocation>> BuildTargetsAsync(
            PaymentAdjustmentCreateDto request,
            Guid branchId,
            CancellationToken ct)
        {
            var methods = await GetBranchEnabledPaymentMethodsAsync(branchId, ct);
            return BuildTargets(request, methods);
        }

        private async Task<List<TargetAllocation>> BuildTargetsAsync(
            IEnumerable<PaymentAdjustmentDetail> details,
            Guid branchId,
            CancellationToken ct)
        {
            var methods = await GetBranchEnabledPaymentMethodsAsync(branchId, ct);
            return BuildTargets(details, methods);
        }

        private async Task<IReadOnlyCollection<PaymentMethod>> GetBranchEnabledPaymentMethodsAsync(
            Guid branchId,
            CancellationToken ct)
        {
            var enabledIds = await _branchConfigurationService.GetEnabledPaymentMethodIdsAsync(branchId, ct);
            return (await _repository.GetActivePaymentMethodsAsync(ct))
                .Where(method => enabledIds.Contains(method.Id))
                .ToList();
        }

        private static List<TargetAllocation> BuildTargets(
            PaymentAdjustmentCreateDto request,
            IReadOnlyCollection<PaymentMethod> methods)
        {
            var lookup = methods.ToDictionary(method => method.Id);
            return request.Payments.Select(allocation =>
            {
                if (allocation.IsCash)
                    return CreateCashTarget(allocation.Amount, allocation.ReferenceNumber);
                if (!lookup.TryGetValue(allocation.PaymentMethodId, out var method))
                    throw new ValidationException("One or more payment methods are invalid or inactive.");
                return CreateTarget(method, allocation.Amount, allocation.ReferenceNumber);
            }).ToList();
        }

        private static List<TargetAllocation> BuildTargets(
            IEnumerable<PaymentAdjustmentDetail> details,
            IReadOnlyCollection<PaymentMethod> methods)
        {
            var lookup = methods.ToDictionary(method => method.Id);
            return details
                .GroupBy(detail => new
                {
                    detail.NewPaymentMethodId,
                    detail.NewPaymentMethodName,
                    detail.NewPaymentMethodNameAr,
                    detail.NewPaymentMethodCode,
                    detail.NewReferenceNumber
                })
                .Select(group =>
                {
                    var amount = OrderPaymentHelper.RoundCurrency(group.Sum(detail => detail.Amount));
                    if (IsCash(group.Key.NewPaymentMethodCode, group.Key.NewPaymentMethodName))
                        return CreateCashTarget(amount, group.Key.NewReferenceNumber);
                    if (!group.Key.NewPaymentMethodId.HasValue ||
                        !lookup.TryGetValue(group.Key.NewPaymentMethodId.Value, out var method))
                        throw new ValidationException("A proposed payment method is no longer active.");
                    return CreateTarget(method, amount, group.Key.NewReferenceNumber);
                })
                .ToList();
        }

        private static TargetAllocation CreateTarget(
            PaymentMethod method,
            decimal amount,
            string? referenceNumber)
        {
            var normalizedAmount = ValidateAmount(amount);
            var reference = NormalizeReference(referenceNumber);
            if (method.RequiresReferenceNumber && reference == null)
                throw new ValidationException($"Reference number is required for {method.NameEn}.");
            var isCash = IsCash(method.Code, method.NameEn);
            return new TargetAllocation(
                isCash ? null : method.Id,
                method.NameEn,
                method.NameAr,
                method.Code,
                isCash,
                normalizedAmount,
                reference);
        }

        private static TargetAllocation CreateCashTarget(decimal amount, string? referenceNumber)
            => new(
                null,
                CashName,
                CashNameAr,
                OrderPaymentHelper.CashKey,
                true,
                ValidateAmount(amount),
                NormalizeReference(referenceNumber));

        private static decimal ValidateAmount(decimal amount)
        {
            var normalized = OrderPaymentHelper.RoundCurrency(amount);
            if (normalized <= 0 || normalized != amount)
                throw new ValidationException("Payment amounts must be positive with no more than two decimal places.");
            return normalized;
        }

        private static PaymentAdjustment BuildPendingAdjustment(
            Order order,
            PaymentAdjustmentCreateDto request,
            User requester,
            IReadOnlyList<TargetAllocation> targets)
        {
            var requestedAt = DateTime.UtcNow;
            var adjustment = new PaymentAdjustment
            {
                Id = Guid.NewGuid(),
                TenantId = order.TenantId,
                BranchId = order.BranchId,
                OrderId = order.Id,
                RequestedByUserId = requester.Id,
                RequestedByUserName = DisplayName(requester),
                ReasonCode = request.ReasonCode.Trim().ToUpperInvariant(),
                Reason = ResolveReason(request),
                Status = PaymentAdjustmentStatus.Pending,
                CreatedAt = requestedAt,
                UpdatedAt = requestedAt
            };
            adjustment.Details = BuildDetails(adjustment, order.Payments.OrderBy(p => p.CreatedAt).ToList(), targets);
            return adjustment;
        }

        private static List<PaymentAdjustmentDetail> BuildDetails(
            PaymentAdjustment adjustment,
            IReadOnlyList<Payment> oldPayments,
            IReadOnlyList<TargetAllocation> targets)
        {
            var details = new List<PaymentAdjustmentDetail>();
            var oldIndex = 0;
            var newIndex = 0;
            var oldRemaining = oldPayments[0].Amount;
            var newRemaining = targets[0].Amount;

            while (oldIndex < oldPayments.Count && newIndex < targets.Count)
            {
                var amount = OrderPaymentHelper.RoundCurrency(Math.Min(oldRemaining, newRemaining));
                details.Add(BuildDetail(adjustment, oldPayments[oldIndex], targets[newIndex], amount));
                oldRemaining = OrderPaymentHelper.RoundCurrency(oldRemaining - amount);
                newRemaining = OrderPaymentHelper.RoundCurrency(newRemaining - amount);
                if (oldRemaining == 0 && ++oldIndex < oldPayments.Count)
                    oldRemaining = oldPayments[oldIndex].Amount;
                if (newRemaining == 0 && ++newIndex < targets.Count)
                    newRemaining = targets[newIndex].Amount;
            }

            return details;
        }

        private static PaymentAdjustmentDetail BuildDetail(
            PaymentAdjustment adjustment,
            Payment oldPayment,
            TargetAllocation target,
            decimal amount)
        {
            var oldIsCash = OrderPaymentHelper.IsCashMethod(oldPayment.Method);
            return new PaymentAdjustmentDetail
            {
                Id = Guid.NewGuid(),
                TenantId = adjustment.TenantId,
                BranchId = adjustment.BranchId,
                AdjustmentId = adjustment.Id,
                PaymentId = oldPayment.Id,
                OldPaymentMethodId = oldIsCash ? null : oldPayment.PaymentMethodId,
                NewPaymentMethodId = target.PaymentMethodId,
                OldPaymentMethodName = oldPayment.PaymentMethodName
                    ?? (oldIsCash ? CashName : OrderPaymentHelper.ResolvePaymentDisplayName(oldPayment)),
                OldPaymentMethodNameAr = oldPayment.PaymentMethodNameAr,
                NewPaymentMethodName = target.Name,
                NewPaymentMethodNameAr = target.NameAr,
                NewPaymentMethodCode = target.Code,
                NewReferenceNumber = target.ReferenceNumber,
                Amount = amount,
                CreatedAt = adjustment.CreatedAt,
                UpdatedAt = adjustment.CreatedAt
            };
        }

        private static List<Payment> BuildPayments(
            Order order,
            IReadOnlyList<TargetAllocation> targets,
            DateTime approvedAt)
        {
            return targets.Select(target => new Payment
            {
                Id = Guid.NewGuid(),
                TenantId = order.TenantId,
                BranchId = order.BranchId,
                OrderId = order.Id,
                Amount = target.Amount,
                Method = target.IsCash ? CashName : "Card",
                PaymentMethodId = target.PaymentMethodId,
                PaymentMethodName = target.Name,
                PaymentMethodNameAr = target.NameAr,
                PaymentMethodCode = target.Code,
                ReferenceNumber = target.ReferenceNumber,
                CreatedByUserId = order.PaidByUserId,
                CreatedAt = approvedAt,
                UpdatedAt = approvedAt
            }).ToList();
        }

        private static void AssignNewPaymentIds(
            IEnumerable<PaymentAdjustmentDetail> details,
            IReadOnlyList<TargetAllocation> targets,
            IReadOnlyList<Payment> payments)
        {
            foreach (var detail in details)
            {
                var index = -1;
                for (var i = 0; i < targets.Count; i++)
                {
                    if (!Matches(detail, targets[i]))
                        continue;
                    index = i;
                    break;
                }
                if (index < 0)
                    throw new ValidationException("The proposed payment allocation is invalid.");
                detail.NewPaymentId = payments[index].Id;
            }
        }

        private static bool Matches(PaymentAdjustmentDetail detail, TargetAllocation target)
            => detail.NewPaymentMethodId == target.PaymentMethodId &&
                string.Equals(detail.NewPaymentMethodCode, target.Code, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(detail.NewReferenceNumber ?? string.Empty, target.ReferenceNumber ?? string.Empty, StringComparison.Ordinal);

        private static void ValidateOriginalPayments(
            IEnumerable<Payment> currentPayments,
            IEnumerable<PaymentAdjustmentDetail> details)
        {
            var expected = details
                .GroupBy(detail => detail.PaymentId)
                .ToDictionary(
                    group => group.Key,
                    group => OrderPaymentHelper.RoundCurrency(group.Sum(detail => detail.Amount)));
            var current = currentPayments.ToDictionary(
                payment => payment.Id,
                payment => OrderPaymentHelper.RoundCurrency(payment.Amount));
            if (expected.Count != current.Count || expected.Any(pair =>
                    !current.TryGetValue(pair.Key, out var amount) || amount != pair.Value))
                throw new ValidationException("Order payments changed after this request was created.");
        }

        private static void ApplyDecision(
            PaymentAdjustment adjustment,
            User approver,
            PaymentAdjustmentStatus status,
            string? notes,
            DateTime decidedAt)
        {
            adjustment.ApprovedByUserId = approver.Id;
            adjustment.ApprovedByUserName = DisplayName(approver);
            adjustment.Status = status;
            adjustment.ApprovedAt = decidedAt;
            adjustment.DecisionNotes = NormalizeNotes(notes);
            adjustment.UpdatedAt = decidedAt;
        }

        private async Task NotifyApproversAsync(Order order, PaymentAdjustment adjustment)
        {
            try
            {
                await _notificationService.SendAsync(
                    NotificationType.PaymentAdjustmentRequested,
                    "Payment change approval required",
                    $"Order {ResolveOrderNumber(order)} has a pending payment change requested by {adjustment.RequestedByUserName}.",
                    order.Id.ToString(),
                    AppRoleGroups.PaymentChangeApprovers,
                    order.TenantId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Payment adjustment {AdjustmentId} saved but manager notification failed",
                    adjustment.Id);
            }
        }

        private async Task PublishInvalidationAsync(Guid orderId, Guid tenantId, CancellationToken ct)
        {
            try
            {
                await _mediator.Publish(new PaymentMethodAdjustedEvent(orderId, tenantId), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Payment adjustment for order {OrderId} committed but dashboard invalidation failed",
                    orderId);
            }
        }

        private void LogDecision(PaymentAdjustment adjustment, Guid approverId)
        {
            _logger.LogInformation(
                "Payment adjustment {AdjustmentId} for order {OrderId} was {Status} by {ApproverId}",
                adjustment.Id,
                adjustment.OrderId,
                adjustment.Status,
                approverId);
        }

        private static PaymentAdjustmentResultDto MapResult(
            Order order,
            PaymentAdjustment adjustment,
            IEnumerable<Payment> payments,
            string? paymentMethod = null)
        {
            return new PaymentAdjustmentResultDto
            {
                AdjustmentId = adjustment.Id,
                OrderId = order.Id,
                Status = adjustment.Status.ToString(),
                TotalAmount = order.TotalAmount,
                PaymentMethod = paymentMethod ?? order.PaymentMethod ?? string.Empty,
                RequestedAt = adjustment.CreatedAt,
                ApprovedAt = adjustment.ApprovedAt,
                ApprovedByUserId = adjustment.ApprovedByUserId,
                ApprovedBy = adjustment.ApprovedByUserName,
                Payments = payments.Select(OrderPaymentHelper.MapPayment).ToList()
            };
        }

        private static PaymentAdjustmentReviewDto MapReview(PaymentAdjustment adjustment)
        {
            return new PaymentAdjustmentReviewDto
            {
                AdjustmentId = adjustment.Id,
                OrderId = adjustment.OrderId,
                OrderNumber = ResolveOrderNumber(adjustment.Order),
                Status = adjustment.Status.ToString(),
                Reason = adjustment.Reason,
                RequestedBy = adjustment.RequestedByUserName,
                RequestedAt = adjustment.CreatedAt,
                TotalAmount = adjustment.Order.TotalAmount,
                CurrentPayments = adjustment.Order.Payments
                    .OrderBy(payment => payment.CreatedAt)
                    .Select(MapReviewPayment)
                    .ToList(),
                ProposedPayments = adjustment.Details
                    .GroupBy(detail => new
                    {
                        detail.NewPaymentMethodId,
                        detail.NewPaymentMethodName,
                        detail.NewPaymentMethodNameAr,
                        detail.NewPaymentMethodCode,
                        detail.NewReferenceNumber
                    })
                    .Select(group => new PaymentAdjustmentReviewPaymentDto
                    {
                        PaymentMethodId = group.Key.NewPaymentMethodId,
                        PaymentMethodName = group.Key.NewPaymentMethodName,
                        PaymentMethodNameAr = group.Key.NewPaymentMethodNameAr,
                        PaymentMethodCode = group.Key.NewPaymentMethodCode,
                        Amount = OrderPaymentHelper.RoundCurrency(group.Sum(detail => detail.Amount)),
                        ReferenceNumber = group.Key.NewReferenceNumber
                    })
                    .ToList()
            };
        }

        private static PaymentAdjustmentReviewPaymentDto MapReviewPayment(Payment payment)
            => new()
            {
                PaymentMethodId = payment.PaymentMethodId,
                PaymentMethodName = payment.PaymentMethodName
                    ?? OrderPaymentHelper.ResolvePaymentDisplayName(payment),
                PaymentMethodNameAr = payment.PaymentMethodNameAr,
                PaymentMethodCode = payment.PaymentMethodCode,
                Amount = payment.Amount,
                ReferenceNumber = payment.ReferenceNumber
            };

        private static string ResolveReason(PaymentAdjustmentCreateDto request)
        {
            var code = request.ReasonCode?.Trim().ToUpperInvariant() ?? string.Empty;
            if (code == PaymentAdjustmentReasonCodes.Other)
            {
                var text = request.ReasonText?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    throw new ValidationException("Reason text is required when Other is selected.");
                return text;
            }

            return ReasonLabels.TryGetValue(code, out var label)
                ? label
                : throw new ValidationException("Payment adjustment reason is invalid.");
        }

        private static string[] BuildOldSignature(IEnumerable<Payment> payments)
        {
            return payments
                .GroupBy(payment => new
                {
                    Method = OrderPaymentHelper.IsCashMethod(payment.Method)
                        ? OrderPaymentHelper.CashKey
                        : payment.PaymentMethodId?.ToString() ?? payment.Method.Trim().ToUpperInvariant(),
                    Reference = payment.ReferenceNumber?.Trim() ?? string.Empty
                })
                .Select(group => $"{group.Key.Method}|{group.Key.Reference}|{OrderPaymentHelper.RoundCurrency(group.Sum(payment => payment.Amount))}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] BuildNewSignature(IEnumerable<TargetAllocation> targets)
        {
            return targets
                .GroupBy(target => new
                {
                    Method = target.IsCash
                        ? OrderPaymentHelper.CashKey
                        : target.PaymentMethodId?.ToString() ?? target.Code,
                    Reference = target.ReferenceNumber ?? string.Empty
                })
                .Select(group => $"{group.Key.Method}|{group.Key.Reference}|{OrderPaymentHelper.RoundCurrency(group.Sum(target => target.Amount))}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsCash(string? code, string? name)
            => string.Equals(code, OrderPaymentHelper.CashKey, StringComparison.OrdinalIgnoreCase)
                || OrderPaymentHelper.IsCashMethod(name);

        private static string? NormalizeReference(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? NormalizeNotes(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string DisplayName(User user)
            => string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName.Trim();

        private static string ResolveOrderNumber(Order order)
            => order.DisplayOrderNumber ?? order.PublicOrderNumber ?? order.OrderNumber;

        private sealed record TargetAllocation(
            Guid? PaymentMethodId,
            string Name,
            string? NameAr,
            string Code,
            bool IsCash,
            decimal Amount,
            string? ReferenceNumber);
    }
}
