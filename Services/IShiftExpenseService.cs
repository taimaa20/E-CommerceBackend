using RestaurantPos.Api.DTOs.ExpenseInvoice;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Cashier-recorded expenses / invoices bound to an open drawer session.
    /// Identity (branch, cashier, shift, timestamp) always comes from the caller's
    /// authenticated context — never from the request body.
    /// </summary>
    public interface IShiftExpenseService
    {
        Task<ShiftExpenseDto> CreateForCurrentShiftAsync(
            CreateShiftExpenseDto dto,
            Guid currentUserId,
            string? currentUserName,
            bool isArabic,
            CancellationToken ct = default);

        Task<ShiftExpenseListDto> GetCurrentShiftExpensesAsync(
            Guid currentUserId,
            bool isArabic,
            CancellationToken ct = default);

        Task<ShiftExpenseListDto> GetShiftExpensesAsync(
            Guid shiftId,
            Guid currentUserId,
            bool isAdmin,
            bool isArabic,
            CancellationToken ct = default);

        Task<ShiftExpenseDto> CancelAsync(
            Guid expenseId,
            CancelShiftExpenseDto dto,
            Guid currentUserId,
            string? currentUserName,
            bool isAdmin,
            bool isArabic,
            CancellationToken ct = default);

        Task<List<ExpenseCategoryDto>> ListCategoriesAsync(
            bool isArabic,
            CancellationToken ct = default);
    }
}
