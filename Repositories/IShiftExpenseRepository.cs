using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    /// <summary>
    /// Read/write access for the shift-scoped slice of <see cref="ExpenseInvoice"/>.
    /// Rows with a null CashierShiftId are back-office supplier bills and are never
    /// returned here.
    /// </summary>
    public interface IShiftExpenseRepository
    {
        Task AddAsync(ExpenseInvoice expense, CancellationToken ct = default);

        Task AddAuditLogAsync(ExpenseInvoiceAuditLog auditLog, CancellationToken ct = default);

        Task<ExpenseInvoice?> GetTrackedShiftExpenseAsync(
            Guid tenantId,
            Guid branchId,
            Guid expenseId,
            CancellationToken ct = default);

        Task<List<ShiftExpenseDto>> ListForShiftAsync(
            Guid tenantId,
            Guid branchId,
            Guid shiftId,
            bool isArabic,
            CancellationToken ct = default);

        Task<ShiftExpenseSummaryDto> GetSummaryAsync(
            Guid tenantId,
            Guid branchId,
            Guid shiftId,
            bool isArabic,
            CancellationToken ct = default);

        Task<ExpenseCategory?> GetActiveCategoryAsync(
            Guid tenantId,
            Guid categoryId,
            CancellationToken ct = default);

        Task<List<ExpenseCategoryDto>> ListActiveCategoriesAsync(
            Guid tenantId,
            bool isArabic,
            CancellationToken ct = default);

        Task<PaymentMethod?> GetActivePaymentMethodAsync(
            Guid tenantId,
            Guid paymentMethodId,
            CancellationToken ct = default);

        Task<string?> GetTenantCurrencyAsync(Guid tenantId, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
