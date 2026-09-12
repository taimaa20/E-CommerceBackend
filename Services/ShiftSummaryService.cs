using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    /// <inheritdoc cref="IShiftSummaryService"/>
    public class ShiftSummaryService : IShiftSummaryService
    {
        private readonly ICashierShiftRepository _repository;
        private readonly IShiftCloseValidator _closeValidator;
        private readonly IBranchContext _branchContext;
        private readonly IShiftExpenseRepository _expenseRepository;

        public ShiftSummaryService(
            ICashierShiftRepository repository,
            IShiftCloseValidator closeValidator,
            IBranchContext branchContext,
            IShiftExpenseRepository expenseRepository)
        {
            _repository     = repository     ?? throw new ArgumentNullException(nameof(repository));
            _closeValidator = closeValidator ?? throw new ArgumentNullException(nameof(closeValidator));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _expenseRepository = expenseRepository ?? throw new ArgumentNullException(nameof(expenseRepository));
        }

        public async Task<ShiftSummaryDto> GetSummaryAsync(
            Guid tenantId,
            Guid? shiftId,
            Guid currentUserId,
            bool isAdmin,
            bool isArabic,
            CancellationToken ct)
        {
            var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
            var shift = shiftId.HasValue
                ? await _repository.GetShiftByIdAsync(tenantId, branchId, shiftId.Value, ct)
                : await _repository.GetActiveShiftAsync(tenantId, branchId, currentUserId, ct);

            if (shift is null)
                throw new NotFoundException("Shift not found.");

            if (!isAdmin && shift.CashierId != currentUserId)
                throw new ForbiddenException("You are not allowed to access this shift.");

            var windowEnd = shift.ClosedAt ?? DateTime.UtcNow;

            var counts     = await _repository.GetShiftDashboardCountsAsync(
                tenantId, shift.BranchId, shift.CashierId, shift.OpenedAt, windowEnd, ct);
            var validation = await _closeValidator.ValidateAsync(
                tenantId, shift, windowEnd, ct);
            var expenses   = await _expenseRepository.GetSummaryAsync(
                tenantId, shift.BranchId, shift.Id, isArabic, ct);

            return new ShiftSummaryDto
            {
                ShiftId       = shift.Id,
                OpenedAtUtc   = shift.OpenedAt,
                ClosedAtUtc   = shift.ClosedAt,
                CashierName   = shift.CashierName,
                ShiftNumber   = shift.ShiftNumber,

                TotalOrders     = counts.TotalOrders,
                TotalRevenue    = counts.TotalRevenue,
                CompletedOrders = counts.CompletedOrders,
                ServedOrders    = counts.ServedOrders,
                ReadyOrders     = counts.ReadyOrders,
                PreparingOrders = counts.PreparingOrders,
                PendingOrders   = counts.PendingOrders,
                PaidOrders      = counts.PaidOrders,
                CancelledOrders = counts.CancelledOrders,
                RefundedOrders  = counts.RefundedOrders,

                DeliveryOrders  = counts.DeliveryOrders,
                PartnerOrders   = counts.PartnerOrders,
                TakeawayOrders  = counts.TakeawayOrders,
                DineInOrders    = counts.DineInOrders,
                QrOrders        = counts.QrOrders,

                Expenses        = expenses,

                CloseStatus     = validation,
            };
        }
    }
}
