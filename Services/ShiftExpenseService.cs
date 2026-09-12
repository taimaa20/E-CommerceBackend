using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Records real business expenses paid during a cashier's open shift.
    ///
    /// Financial contract:
    ///   • A cash expense reduces expected drawer cash at the source
    ///     (CashierShiftRepository.GetLiveMetricsAsync) so it is a documented
    ///     payout, never a shortage.
    ///   • A non-cash expense is recorded but leaves drawer cash untouched.
    ///   • Nothing here writes orders, payments, stock or COGS.
    /// </summary>
    public sealed class ShiftExpenseService : IShiftExpenseService
    {
        private const string DefaultCurrency = "USD";

        private readonly IShiftExpenseRepository _repo;
        private readonly ICashierShiftRepository _shiftRepo;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchContext _branchContext;
        private readonly ILogger<ShiftExpenseService> _logger;

        public ShiftExpenseService(
            IShiftExpenseRepository repo,
            ICashierShiftRepository shiftRepo,
            ITenantResolver tenantResolver,
            IBranchContext branchContext,
            ILogger<ShiftExpenseService> logger)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _shiftRepo = shiftRepo ?? throw new ArgumentNullException(nameof(shiftRepo));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ShiftExpenseDto> CreateForCurrentShiftAsync(
            CreateShiftExpenseDto dto,
            Guid currentUserId,
            string? currentUserName,
            bool isArabic,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            if (currentUserId == Guid.Empty)
                throw new UnauthorizedException("You are not allowed to record shift expenses.");

            if (dto.Amount <= 0m)
                throw new ValidationException("Amount must be greater than zero.");

            var tenantId = _tenantResolver.GetTenantId();
            var branchId = await GetCurrentBranchIdAsync(ct);

            // The shift is resolved from the caller — a cashier can never post an
            // expense onto someone else's drawer or another branch's shift.
            var shift = await _shiftRepo.GetActiveShiftAsync(tenantId, branchId, currentUserId, ct)
                ?? throw new ValidationException(
                    "No active shift. Open a shift before recording an expense.");

            var category = await _repo.GetActiveCategoryAsync(tenantId, dto.ExpenseCategoryId, ct)
                ?? throw new ValidationException("Expense category was not found or is inactive.");

            var (paymentMethodId, paymentMethodLabel, isCash) =
                await ResolvePaymentMethodAsync(tenantId, dto.PaymentMethodId, ct);

            var occurredAt = DateTime.UtcNow;
            var amount = OrderPaymentHelper.RoundCurrency(dto.Amount);
            var reference = string.IsNullOrWhiteSpace(dto.ReferenceNumber)
                ? ShiftExpenseRules.BuildGeneratedReference(occurredAt)
                : dto.ReferenceNumber.Trim();

            var expense = new ExpenseInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = branchId,
                CashierShiftId = shift.Id,
                InvoiceNumber = reference,
                ExpenseCategoryId = category.Id,
                CategoryLabel = category.Name,
                InvoiceDate = occurredAt,
                // No supplier document to split, so the whole amount is the subtotal.
                // Tax/discount stay zero — ValidateAmounts' invariant holds by design.
                Subtotal = amount,
                TaxAmount = 0m,
                DiscountAmount = 0m,
                TotalAmount = amount,
                CurrencyCode = await ResolveCurrencyAsync(tenantId, ct),
                PaymentMethod = ShiftExpenseRules.ResolvePaymentMethod(isCash),
                PaymentMethodId = paymentMethodId,
                PaymentMethodDetail = paymentMethodLabel,
                Notes = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                Status = ShiftExpenseRules.RecordedStatus,
                CreatedById = currentUserId,
                CreatedByName = NormalizeName(currentUserName) ?? shift.CashierName
            };

            await _repo.AddAsync(expense, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Shift expense {ExpenseId} recorded on shift {ShiftId} (amount={Amount}, cash={IsCash}, category={Category})",
                expense.Id, shift.Id, expense.TotalAmount, isCash, category.Name);

            return await GetSingleAsync(tenantId, branchId, shift.Id, expense.Id, isArabic, ct);
        }

        public async Task<ShiftExpenseListDto> GetCurrentShiftExpensesAsync(
            Guid currentUserId,
            bool isArabic,
            CancellationToken ct = default)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var branchId = await GetCurrentBranchIdAsync(ct);

            var shift = await _shiftRepo.GetActiveShiftAsync(tenantId, branchId, currentUserId, ct)
                ?? throw new NotFoundException("No active shift.");

            return await BuildListAsync(tenantId, branchId, shift.Id, isArabic, ct);
        }

        public async Task<ShiftExpenseListDto> GetShiftExpensesAsync(
            Guid shiftId,
            Guid currentUserId,
            bool isAdmin,
            bool isArabic,
            CancellationToken ct = default)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var branchId = await GetCurrentBranchIdAsync(ct);

            var shift = await _shiftRepo.GetShiftByIdAsync(tenantId, branchId, shiftId, ct)
                ?? throw new NotFoundException("Shift not found.");

            if (!isAdmin && shift.CashierId != currentUserId)
                throw new ForbiddenException("You are not allowed to access this shift.");

            return await BuildListAsync(tenantId, branchId, shift.Id, isArabic, ct);
        }

        public async Task<ShiftExpenseDto> CancelAsync(
            Guid expenseId,
            CancelShiftExpenseDto dto,
            Guid currentUserId,
            string? currentUserName,
            bool isAdmin,
            bool isArabic,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var tenantId = _tenantResolver.GetTenantId();
            var branchId = await GetCurrentBranchIdAsync(ct);

            var expense = await _repo.GetTrackedShiftExpenseAsync(tenantId, branchId, expenseId, ct)
                ?? throw new NotFoundException("Shift expense not found.");

            if (expense.Status == ExpenseInvoiceStatus.Cancelled)
                throw new ConflictException("This expense is already cancelled.");

            var shift = await _shiftRepo.GetShiftByIdAsync(tenantId, branchId, expense.CashierShiftId!.Value, ct)
                ?? throw new NotFoundException("Shift not found.");

            if (!isAdmin && shift.CashierId != currentUserId)
                throw new ForbiddenException("You are not allowed to access this shift.");

            // A closed shift's expected balance and variance are already settled and
            // audited. Voiding an expense afterwards would silently rewrite that
            // reconciliation, so the window closes with the shift.
            if (shift.IsClosed)
                throw new ConflictException("Expenses of a closed shift can no longer be cancelled.");

            var cancelledAt = DateTime.UtcNow;
            var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();

            // Amount, author, shift and dates are deliberately left untouched — the
            // status flip is what removes it from active totals.
            expense.Status = ExpenseInvoiceStatus.Cancelled;
            expense.CancelledAt = cancelledAt;
            expense.CancelledById = currentUserId;
            expense.CancelledByName = NormalizeName(currentUserName);
            expense.CancellationReason = reason;

            await _repo.AddAuditLogAsync(new ExpenseInvoiceAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = expense.TenantId,
                ExpenseInvoiceId = expense.Id,
                EventType = ExpenseInvoiceAuditEventType.Cancelled,
                ActorUserId = currentUserId,
                ActorUserName = NormalizeName(currentUserName),
                InvoiceNumber = expense.InvoiceNumber,
                SupplierId = expense.SupplierId,
                SupplierName = expense.SupplierName,
                InvoiceDate = expense.InvoiceDate,
                AcknowledgedAt = cancelledAt,
                Reason = reason
            }, ct);

            await _repo.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Shift expense {ExpenseId} cancelled by user {UserId} on shift {ShiftId} (amount={Amount})",
                expense.Id, currentUserId, shift.Id, expense.TotalAmount);

            return await GetSingleAsync(tenantId, branchId, shift.Id, expense.Id, isArabic, ct);
        }

        public async Task<List<ExpenseCategoryDto>> ListCategoriesAsync(
            bool isArabic,
            CancellationToken ct = default)
            => await _repo.ListActiveCategoriesAsync(_tenantResolver.GetTenantId(), isArabic, ct);

        // ───────────────────────── helpers ─────────────────────────────────

        private async Task<ShiftExpenseListDto> BuildListAsync(
            Guid tenantId,
            Guid branchId,
            Guid shiftId,
            bool isArabic,
            CancellationToken ct)
        {
            return new ShiftExpenseListDto
            {
                ShiftId = shiftId,
                Summary = await _repo.GetSummaryAsync(tenantId, branchId, shiftId, isArabic, ct),
                Expenses = await _repo.ListForShiftAsync(tenantId, branchId, shiftId, isArabic, ct)
            };
        }

        private async Task<ShiftExpenseDto> GetSingleAsync(
            Guid tenantId,
            Guid branchId,
            Guid shiftId,
            Guid expenseId,
            bool isArabic,
            CancellationToken ct)
        {
            var rows = await _repo.ListForShiftAsync(tenantId, branchId, shiftId, isArabic, ct);
            return rows.FirstOrDefault(r => r.Id == expenseId)
                ?? throw new NotFoundException("Shift expense not found.");
        }

        /// <summary>
        /// Resolves the drawer impact from the configured PaymentMethods registry.
        /// An omitted method means cash — the same convention
        /// <see cref="OrderPaymentHelper.IsCashMethod"/> applies to order payments,
        /// so a tenant with an empty registry still gets correct drawer arithmetic.
        /// </summary>
        private async Task<(Guid? Id, string? Label, bool IsCash)> ResolvePaymentMethodAsync(
            Guid tenantId,
            Guid? paymentMethodId,
            CancellationToken ct)
        {
            if (!paymentMethodId.HasValue)
                return (null, null, true);

            var method = await _repo.GetActivePaymentMethodAsync(tenantId, paymentMethodId.Value, ct)
                ?? throw new ValidationException("Payment method was not found or is inactive.");

            var isCash = OrderPaymentHelper.IsCashMethod(method.Code)
                      || OrderPaymentHelper.IsCashMethod(method.NameEn);

            return (method.Id, method.NameEn, isCash);
        }

        private async Task<string> ResolveCurrencyAsync(Guid tenantId, CancellationToken ct)
        {
            var currency = await _repo.GetTenantCurrencyAsync(tenantId, ct);
            return string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3
                ? DefaultCurrency
                : currency.Trim().ToUpperInvariant();
        }

        private static string? NormalizeName(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
            => (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
    }
}
