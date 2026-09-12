using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Hubs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    public class CashierShiftService : ICashierShiftService
    {
        private readonly ICashierShiftRepository _repository;
        private readonly INotificationService _notificationService;
        private readonly IHubContext<KitchenHub> _hubContext;
        private readonly IShiftRulesConfigService _rulesConfigService;
        private readonly IShiftCloseValidator _closeValidator;
        private readonly IConfigAuditService _audit;
        private readonly IBranchContext _branchContext;
        private readonly ILogger<CashierShiftService> _logger;

        public CashierShiftService(
            ICashierShiftRepository repository,
            INotificationService notificationService,
            IHubContext<KitchenHub> hubContext,
            IShiftRulesConfigService rulesConfigService,
            IShiftCloseValidator closeValidator,
            IConfigAuditService audit,
            IBranchContext branchContext,
            ILogger<CashierShiftService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _rulesConfigService = rulesConfigService ?? throw new ArgumentNullException(nameof(rulesConfigService));
            _closeValidator = closeValidator ?? throw new ArgumentNullException(nameof(closeValidator));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<CashierBalanceShiftDto>> GetShiftsAsync(
            Guid tenantId,
            Guid currentUserId,
            bool isAdmin,
            CashierShiftQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var branchId = await GetCurrentBranchIdAsync(cancellationToken);
            var shifts = await _repository.GetShiftsAsync(
                tenantId,
                branchId,
                query,
                isAdmin,
                currentUserId,
                cancellationToken);

            return await MapShiftsAsync(tenantId, branchId, shifts, cancellationToken);
        }

        public Task<List<CashierShiftOwnerOptionDto>> GetShiftOwnerOptionsAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
            => _repository.GetShiftOwnerOptionsAsync(tenantId, cancellationToken);

        public async Task<CashierBalanceShiftDto?> GetActiveShiftAsync(
            Guid tenantId,
            Guid cashierId,
            Guid currentUserId,
            bool isAdmin,
            CancellationToken cancellationToken = default)
        {
            if (!isAdmin && currentUserId != cashierId)
                throw new ForbiddenException("You are not allowed to access this shift.");

            var branchId = await GetCurrentBranchIdAsync(cancellationToken);
            var shift = await _repository.GetActiveShiftAsync(tenantId, branchId, cashierId, cancellationToken);
            if (shift == null)
                return null;

            return (await MapShiftsAsync(tenantId, branchId, new[] { shift }, cancellationToken)).Single();
        }

        public async Task<CashierBalanceShiftDto?> GetShiftByIdAsync(
            Guid tenantId,
            Guid shiftId,
            Guid currentUserId,
            bool isAdmin,
            CancellationToken cancellationToken = default)
        {
            var branchId = await GetCurrentBranchIdAsync(cancellationToken);
            var shift = await _repository.GetShiftByIdAsync(tenantId, branchId, shiftId, cancellationToken);
            if (shift == null)
                return null;

            if (!isAdmin && shift.CashierId != currentUserId)
                throw new ForbiddenException("You are not allowed to access this shift.");

            return (await MapShiftsAsync(tenantId, branchId, new[] { shift }, cancellationToken)).Single();
        }

        public async Task<CashierBalanceShiftDto> OpenShiftAsync(
            Guid tenantId,
            Guid openedByUserId,
            OpenShiftRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (openedByUserId == Guid.Empty)
                throw new UnauthorizedException("You are not allowed to access this shift.");

            var branchId = await GetCurrentBranchIdAsync(cancellationToken);
            var cashier = await _repository.GetCashierByIdAsync(tenantId, request.CashierId, cancellationToken)
                ?? throw new NotFoundException("Cashier not found.");

            if (!Helpers.CashierShiftUserEligibility.CanOwnShift(cashier.Role))
                throw new ConflictException("User is not a cashier.");

            var clientActionId = NormalizeClientActionId(request.ClientActionId);
            if (clientActionId != null)
            {
                var replayed = await _repository.GetShiftByOpenTransactionIdAsync(
                    tenantId,
                    branchId,
                    clientActionId,
                    cancellationToken);
                if (replayed != null)
                    return (await MapShiftsAsync(tenantId, branchId, new[] { replayed }, cancellationToken)).Single();
            }

            if (await _repository.HasActiveShiftAsync(tenantId, branchId, cashier.Id, cancellationToken))
                throw new ConflictException("This cashier already has an open shift. Close it before opening a new one.");

            // ── Per-tenant open rules ────────────────────────────────────────
            // Defaults to permissive Unlimited when no config row exists, so
            // existing tenants keep current behavior exactly.
            var rules = await _rulesConfigService.GetForTenantAsync(tenantId, cancellationToken);
            var posDeviceId = NormalizeOptionalText(request.PosDeviceId);
            switch (rules?.ActiveShiftRule ?? ActiveShiftRule.Unlimited)
            {
                case ActiveShiftRule.SinglePerBranch:
                    if (await _repository.HasAnyActiveShiftAsync(tenantId, branchId, cancellationToken))
                        throw new ConflictException(
                            "Branch already has an active shift. Close it before opening a new one.");
                    break;

                case ActiveShiftRule.SinglePerPosDevice:
                    if (string.IsNullOrWhiteSpace(posDeviceId))
                        throw new ConflictException(
                            "Shift open requires a POS device identifier under the current rules.");
                    if (await _repository.HasActiveShiftForDeviceAsync(tenantId, branchId, posDeviceId, cancellationToken))
                        throw new ConflictException(
                            "This POS device already has an active shift. Close it before opening a new one.");
                    break;

                // SinglePerCashier is already enforced by HasActiveShiftAsync above.
                // Unlimited intentionally has no extra check.
            }

            var shift = new CashierBalanceShift
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = branchId,
                CashierId = cashier.Id,
                OpenTransactionId = clientActionId,
                CashierName = cashier.FullName ?? cashier.Username,
                OpenedByManagerId = openedByUserId,
                ShiftNumber = await _repository.GetNextShiftNumberAsync(tenantId, branchId, cashier.Id, cancellationToken),
                OpenedAt = DateTime.UtcNow,
                OpeningBalance = request.OpeningBalance,
                Notes = NormalizeOptionalText(request.Notes),
                PosDeviceId = posDeviceId,
            };

            await _repository.AddAsync(shift, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(
                tenantId, ConfigAuditEventType.ShiftOpened,
                previousValue: null,
                newValue: new
                {
                    shift.CashierId,
                    shift.CashierName,
                    shift.ShiftNumber,
                    shift.OpenedAt,
                    shift.OpeningBalance,
                    shift.PosDeviceId,
                },
                targetId: shift.Id,
                ct: cancellationToken);

            var dto = (await MapShiftsAsync(tenantId, branchId, new[] { shift }, cancellationToken)).Single();
            await PublishShiftUpdatedAsync(dto, "opened", cancellationToken);
            return dto;
        }

        public async Task<CashierBalanceShiftDto> OpenOwnShiftAsync(
            Guid tenantId,
            Guid currentUserId,
            OpenOwnShiftRequest? request,
            CancellationToken cancellationToken = default)
        {
            if (currentUserId == Guid.Empty)
                throw new UnauthorizedException("You are not allowed to access this shift.");

            var cashier = await _repository.GetCashierByIdAsync(tenantId, currentUserId, cancellationToken)
                ?? throw new NotFoundException("Cashier not found.");

            if (!Helpers.CashierShiftUserEligibility.CanOwnShift(cashier.Role))
                throw new ConflictException("User is not allowed to open a cashier shift.");

            return await OpenShiftAsync(
                tenantId,
                currentUserId,
                new OpenShiftRequest
                {
                    CashierId = currentUserId,
                    OpeningBalance = request?.OpeningBalance ?? 0m,
                    Notes = request?.Notes,
                    ClientActionId = request?.ClientActionId,
                    PosDeviceId = request?.PosDeviceId,
                },
                cancellationToken);
        }

        public async Task<CashierBalanceShiftDto> CloseShiftAsync(
            Guid tenantId,
            Guid shiftId,
            Guid currentUserId,
            bool isAdmin,
            CloseShiftRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var branchId = await GetCurrentBranchIdAsync(cancellationToken);
            var shift = await _repository.GetShiftByIdAsync(tenantId, branchId, shiftId, cancellationToken)
                ?? throw new NotFoundException("Shift not found.");

            if (!isAdmin && shift.CashierId != currentUserId)
                throw new ForbiddenException("You are not allowed to access this shift.");

            var clientActionId = NormalizeClientActionId(request.ClientActionId);
            if (shift.ClosedAt.HasValue)
            {
                if (clientActionId != null && shift.CloseTransactionId == clientActionId)
                    return (await MapShiftsAsync(tenantId, branchId, new[] { shift }, cancellationToken)).Single();

                throw new ConflictException("Shift is already closed.");
            }

            var closedAt = DateTime.UtcNow;

            // ── Close validation gate ────────────────────────────────────────
            // Validator returns IsAllowed=true when no config exists or when
            // AllowShiftCloseWithOpenOrders is on, so legacy tenants are
            // unaffected. Force flag is honored only when the tenant has opted
            // in to AllowForcedShiftClose.
            var validation = await _closeValidator.ValidateAsync(
                tenantId, shift, closedAt, cancellationToken);

            if (!validation.IsAllowed)
            {
                if (!(request.Force && validation.ForceCloseAllowed))
                    throw new Exceptions.ShiftCloseBlockedException(validation);

                shift.ForceClosed = true;
                shift.ForceCloseReason = NormalizeOptionalText(request.ForceReason)
                    ?? "Force closed by cashier";

                _logger.LogWarning(
                    "Shift {ShiftId} force-closed by user {UserId} with {BlockerCount} blocker(s).",
                    shift.Id, currentUserId, validation.Blockers.Count);
            }

            var metrics = (await _repository.GetLiveMetricsAsync(
                tenantId,
                branchId,
                new[] { shift.Id },
                closedAt,
                cancellationToken)).Single();

            var variance = request.ClosingBalance - metrics.ExpectedBalance;
            var classification = variance > 0
                ? VarianceClassification.Surplus
                : variance < 0
                    ? VarianceClassification.Deficit
                    : VarianceClassification.Match;

            shift.ClosedAt = closedAt;
            shift.ClosingBalance = request.ClosingBalance;
            shift.CloseTransactionId = clientActionId;
            shift.PaidOrderCount = metrics.PaidOrderCount;
            shift.OrdersTotal = metrics.OrdersTotal;
            shift.CashOrdersTotal = metrics.CashOrdersTotal;
            shift.CardOrdersTotal = metrics.CardOrdersTotal;
            shift.ExpectedBalance = metrics.ExpectedBalance;
            shift.RefundsTotal = metrics.RefundsTotal;
            shift.NetTotal = metrics.OrdersTotal - metrics.RefundsTotal;
            shift.Variance = variance;
            shift.Classification = classification;
            shift.ClosingComment = NormalizeOptionalText(request.Notes);

            await _repository.SaveChangesAsync(cancellationToken);

            await NotifyVarianceIfNeededAsync(tenantId, shift, variance, classification);

            await _audit.LogAsync(
                tenantId,
                shift.ForceClosed ? ConfigAuditEventType.ShiftForceClosed : ConfigAuditEventType.ShiftClosed,
                previousValue: null,
                newValue: new
                {
                    shift.ClosedAt,
                    shift.ClosingBalance,
                    shift.ExpectedBalance,
                    shift.Variance,
                    Classification = shift.Classification?.ToString(),
                    BlockerCount = validation.Blockers.Count,
                    BlockerCodes = validation.Blockers.Select(b => b.Code).ToList(),
                },
                targetId: shift.Id,
                reason: shift.ForceCloseReason,
                ct: cancellationToken);

            var dto = (await MapShiftsAsync(tenantId, branchId, new[] { shift }, cancellationToken)).Single();
            await PublishShiftUpdatedAsync(dto, "closed", cancellationToken);
            return dto;
        }

        public async Task DeleteShiftAsync(
            Guid tenantId,
            Guid shiftId,
            CancellationToken cancellationToken = default)
        {
            var branchId = await GetCurrentBranchIdAsync(cancellationToken);
            var shift = await _repository.GetShiftByIdAsync(tenantId, branchId, shiftId, cancellationToken)
                ?? throw new NotFoundException("Shift not found.");

            _repository.Remove(shift);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        private async Task<List<CashierBalanceShiftDto>> MapShiftsAsync(
            Guid tenantId,
            Guid branchId,
            IEnumerable<CashierBalanceShift> shifts,
            CancellationToken cancellationToken)
        {
            var shiftList = shifts.ToList();
            var liveMetricIds = shiftList.Select(s => s.Id).ToArray();

            var liveMetrics = (await _repository.GetLiveMetricsAsync(
                    tenantId,
                    branchId,
                    liveMetricIds,
                    DateTime.UtcNow,
                    cancellationToken))
                .ToDictionary(m => m.ShiftId);

            return shiftList
                .Select(shift =>
                {
                    var metrics = liveMetrics.GetValueOrDefault(shift.Id);
                    var useStoredSnapshot = HasStoredSnapshot(shift);
                    var paidOrderCount = useStoredSnapshot ? shift.PaidOrderCount ?? 0 : metrics?.PaidOrderCount ?? 0;
                    var ordersTotal = useStoredSnapshot ? shift.OrdersTotal ?? 0m : metrics?.OrdersTotal ?? 0m;
                    var cashOrdersTotal = useStoredSnapshot ? shift.CashOrdersTotal ?? 0m : metrics?.CashOrdersTotal ?? 0m;
                    var cardOrdersTotal = useStoredSnapshot ? shift.CardOrdersTotal ?? 0m : metrics?.CardOrdersTotal ?? 0m;
                    var deliveryCost = metrics?.DeliveryCost ?? 0m;
                    var cashExpensesTotal = metrics?.CashExpensesTotal ?? 0m;
                    var expectedBalance = CalculateExpectedBalance(
                        shift.OpeningBalance, cashOrdersTotal, deliveryCost, cashExpensesTotal);
                    var variance = CalculateVariance(shift.ClosingBalance, expectedBalance) ?? shift.Variance;
                    var refundsTotal = shift.RefundsTotal ?? metrics?.RefundsTotal ?? 0m;
                    var netTotal = shift.NetTotal ?? (ordersTotal - refundsTotal);

                    return new CashierBalanceShiftDto
                    {
                        Id = shift.Id,
                        BranchId = shift.BranchId,
                        CashierId = shift.CashierId,
                        CashierName = shift.CashierName,
                        OpenedByManagerId = shift.OpenedByManagerId,
                        ShiftNumber = shift.ShiftNumber,
                        OpenedAt = shift.OpenedAt,
                        OpeningBalance = shift.OpeningBalance,
                        ClosedAt = shift.ClosedAt,
                        ClosingBalance = shift.ClosingBalance,
                        Variance = variance,
                        Classification = variance.HasValue
                            ? ClassifyVariance(variance.Value).ToString()
                            : shift.Classification?.ToString(),
                        Notes = shift.Notes,
                        ClosingComment = shift.ClosingComment,
                        IsClosed = shift.IsClosed,
                        IsOpen = shift.IsOpen,
                        PaidOrderCount = paidOrderCount,
                        OrdersTotal = ordersTotal,
                        CashOrdersTotal = cashOrdersTotal,
                        CardOrdersTotal = cardOrdersTotal,
                        ExpectedBalance = expectedBalance,
                        RefundsTotal = refundsTotal,
                        NetTotal = netTotal,
                        FoodRevenue = metrics?.FoodRevenue ?? 0m,
                        DeliveryCollected = metrics?.DeliveryCollected ?? 0m,
                        DeliveryCost = deliveryCost,
                        DeliveryProfit = metrics?.DeliveryProfit ?? 0m,
                        MarketplaceFees = metrics?.MarketplaceFees ?? 0m,
                        MarketplaceServiceFees = metrics?.MarketplaceServiceFees ?? 0m,
                        NetRestaurantRevenue = metrics?.NetRestaurantRevenue ?? 0m,
                        ExpenseCount = metrics?.ExpenseCount ?? 0,
                        ExpensesTotal = metrics?.ExpensesTotal ?? 0m,
                        CashExpensesTotal = cashExpensesTotal,
                        NonCashExpensesTotal = metrics?.NonCashExpensesTotal ?? 0m,
                        PartnerSalesBreakdown = metrics?.PartnerSalesBreakdown ?? new List<CashierShiftPartnerSalesDto>(),
                        PaymentMethodsBreakdown = metrics?.PaymentMethodsBreakdown ?? new List<CashierShiftPaymentMethodBreakdownDto>()
                    };
                })
                .ToList();
        }

        private async Task NotifyVarianceIfNeededAsync(
            Guid tenantId,
            CashierBalanceShift shift,
            decimal variance,
            VarianceClassification classification)
        {
            var settings = await _repository.GetSystemSettingsAsync(tenantId);
            if (settings == null || settings.VarianceThreshold <= 0 || Math.Abs(variance) <= settings.VarianceThreshold)
                return;

            var absVariance = Math.Abs(variance);
            await _notificationService.SendAsync(
                NotificationType.BalanceVariance,
                "Cash Drawer Variance Alert",
                $"{shift.CashierName}'s shift closed with a {classification} of {absVariance:F3} {settings.Currency}. Threshold: {settings.VarianceThreshold:F3}",
                shift.Id.ToString(),
                AppRoleGroups.AdminOnly,
                tenantId,
                shift.BranchId);
        }

        private async Task PublishShiftUpdatedAsync(
            CashierBalanceShiftDto dto,
            string action,
            CancellationToken cancellationToken)
        {
            try
            {
                await _hubContext.Clients.Group(KitchenHubGroups.Branch(dto.BranchId)).SendAsync(KitchenHubEvents.CashierShiftUpdated, new
                {
                    action,
                    shiftId = dto.Id,
                    cashierId = dto.CashierId,
                    cashierName = dto.CashierName,
                    shiftNumber = dto.ShiftNumber,
                    isClosed = dto.IsClosed,
                    isOpen = dto.IsOpen,
                    paidOrderCount = dto.PaidOrderCount,
                    ordersTotal = dto.OrdersTotal,
                    refundsTotal = dto.RefundsTotal,
                    netTotal = dto.NetTotal,
                    expectedBalance = dto.ExpectedBalance,
                    deliveryCost = dto.DeliveryCost,
                    deliveryCollected = dto.DeliveryCollected,
                    deliveryProfit = dto.DeliveryProfit,
                    paymentMethodsBreakdown = dto.PaymentMethodsBreakdown,
                    closingBalance = dto.ClosingBalance,
                    variance = dto.Variance
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cashier shift update notification failed for shift {ShiftId}", dto.Id);
            }
        }

        private static bool NeedsLiveMetrics(CashierBalanceShift shift)
            => !HasStoredSnapshot(shift);

        private static bool HasStoredSnapshot(CashierBalanceShift shift)
            => shift.ClosedAt.HasValue &&
               shift.PaidOrderCount.HasValue &&
               shift.OrdersTotal.HasValue &&
               shift.CashOrdersTotal.HasValue &&
               shift.CardOrdersTotal.HasValue &&
               shift.ExpectedBalance.HasValue &&
               shift.RefundsTotal.HasValue &&
               shift.NetTotal.HasValue;

        /// <summary>
        /// Mirrors CashierShiftRepository.GetLiveMetricsAsync exactly. Documented cash
        /// expenses are subtracted here so the drawer the cashier is asked to count
        /// already accounts for them — a matching count yields zero variance rather
        /// than a phantom shortage.
        /// </summary>
        private static decimal CalculateExpectedBalance(
            decimal openingBalance,
            decimal cashCollected,
            decimal deliveryCost,
            decimal cashExpenses)
            => openingBalance + cashCollected - deliveryCost - cashExpenses;

        private static decimal? CalculateVariance(decimal? actualBalance, decimal expectedBalance)
            => actualBalance.HasValue ? actualBalance.Value - expectedBalance : null;

        private static VarianceClassification ClassifyVariance(decimal variance)
            => variance > 0
                ? VarianceClassification.Surplus
                : variance < 0
                    ? VarianceClassification.Deficit
                    : VarianceClassification.Match;

        private static string? NormalizeOptionalText(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? NormalizeClientActionId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim();
            if (normalized.Length > 120)
                throw new ValidationException("Client action id is too long.");

            return normalized;
        }

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken cancellationToken)
            => (await _branchContext.GetCurrentAsync(cancellationToken)).CurrentBranch.Id;
    }
}
