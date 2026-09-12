using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed class ShiftExpenseRepository : IShiftExpenseRepository
    {
        private readonly PosDbContext _context;

        public ShiftExpenseRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task AddAsync(ExpenseInvoice expense, CancellationToken ct = default)
            => _context.ExpenseInvoices.AddAsync(expense, ct).AsTask();

        public Task AddAuditLogAsync(ExpenseInvoiceAuditLog auditLog, CancellationToken ct = default)
            => _context.ExpenseInvoiceAuditLogs.AddAsync(auditLog, ct).AsTask();

        public Task<ExpenseInvoice?> GetTrackedShiftExpenseAsync(
            Guid tenantId,
            Guid branchId,
            Guid expenseId,
            CancellationToken ct = default)
        {
            return _context.ExpenseInvoices
                .FirstOrDefaultAsync(
                    e => e.Id == expenseId
                      && e.TenantId == tenantId
                      && e.BranchId == branchId
                      && e.CashierShiftId != null,
                    ct);
        }

        public async Task<List<ShiftExpenseDto>> ListForShiftAsync(
            Guid tenantId,
            Guid branchId,
            Guid shiftId,
            bool isArabic,
            CancellationToken ct = default)
        {
            return await ShiftScope(tenantId, branchId, shiftId)
                .OrderByDescending(e => e.InvoiceDate)
                .ThenByDescending(e => e.CreatedAt)
                .Select(e => new ShiftExpenseDto
                {
                    Id = e.Id,
                    CashierShiftId = e.CashierShiftId,
                    BranchId = e.BranchId,
                    ExpenseCategoryId = e.ExpenseCategoryId,
                    // Live category row wins so a rename propagates; the write-time
                    // snapshot keeps the row readable after the category is removed.
                    CategoryLabel = e.ExpenseCategory == null
                        ? e.CategoryLabel
                        : isArabic && e.ExpenseCategory.NameAr != null && e.ExpenseCategory.NameAr != ""
                            ? e.ExpenseCategory.NameAr
                            : e.ExpenseCategory.Name,
                    Amount = e.TotalAmount,
                    CurrencyCode = e.CurrencyCode,
                    Description = e.Notes,
                    ReferenceNumber = e.InvoiceNumber,
                    PaymentMethod = e.PaymentMethod.ToString(),
                    PaymentMethodId = e.PaymentMethodId,
                    PaymentMethodName = e.PaymentMethodDetail,
                    IsCash = e.PaymentMethod == ExpensePaymentMethod.Cash,
                    ExpenseDate = e.InvoiceDate,
                    Status = e.Status.ToString(),
                    IsCancelled = e.Status == ExpenseInvoiceStatus.Cancelled,
                    CreatedById = e.CreatedById,
                    CreatedByName = e.CreatedByName,
                    CreatedAt = e.CreatedAt,
                    CancelledAt = e.CancelledAt,
                    CancelledByName = e.CancelledByName,
                    CancellationReason = e.CancellationReason
                })
                .ToListAsync(ct);
        }

        public async Task<ShiftExpenseSummaryDto> GetSummaryAsync(
            Guid tenantId,
            Guid branchId,
            Guid shiftId,
            bool isArabic,
            CancellationToken ct = default)
        {
            // Cancelled rows are excluded at the source so no consumer can
            // accidentally count a voided expense against the drawer.
            var rows = await ShiftScope(tenantId, branchId, shiftId)
                .Where(e => e.Status != ExpenseInvoiceStatus.Cancelled)
                .Select(e => new
                {
                    e.ExpenseCategoryId,
                    Label = (e.ExpenseCategory == null
                        ? e.CategoryLabel
                        : isArabic && e.ExpenseCategory.NameAr != null && e.ExpenseCategory.NameAr != ""
                            ? e.ExpenseCategory.NameAr
                            : e.ExpenseCategory.Name) ?? string.Empty,
                    Amount = e.TotalAmount,
                    IsCash = e.PaymentMethod == ExpensePaymentMethod.Cash
                })
                .ToListAsync(ct);

            return new ShiftExpenseSummaryDto
            {
                ExpenseCount = rows.Count,
                ExpensesTotal = rows.Sum(r => r.Amount),
                CashExpensesTotal = rows.Where(r => r.IsCash).Sum(r => r.Amount),
                NonCashExpensesTotal = rows.Where(r => !r.IsCash).Sum(r => r.Amount),
                ByCategory = rows
                    .GroupBy(r => new { r.ExpenseCategoryId, r.Label })
                    .Select(g => new ShiftExpenseCategoryBreakdownDto
                    {
                        ExpenseCategoryId = g.Key.ExpenseCategoryId,
                        CategoryLabel = g.Key.Label,
                        Count = g.Count(),
                        Total = g.Sum(r => r.Amount),
                        CashTotal = g.Where(r => r.IsCash).Sum(r => r.Amount)
                    })
                    .OrderByDescending(g => g.Total)
                    .ToList()
            };
        }

        public Task<ExpenseCategory?> GetActiveCategoryAsync(
            Guid tenantId,
            Guid categoryId,
            CancellationToken ct = default)
        {
            return _context.ExpenseCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.TenantId == tenantId && c.IsActive, ct);
        }

        public Task<List<ExpenseCategoryDto>> ListActiveCategoriesAsync(
            Guid tenantId,
            bool isArabic,
            CancellationToken ct = default)
        {
            return _context.ExpenseCategories
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new ExpenseCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    NameAr = c.NameAr,
                    DisplayName = isArabic && c.NameAr != null && c.NameAr != "" ? c.NameAr : c.Name,
                    Color = c.Color,
                    IsActive = c.IsActive,
                    SortOrder = c.SortOrder
                })
                .ToListAsync(ct);
        }

        public Task<PaymentMethod?> GetActivePaymentMethodAsync(
            Guid tenantId,
            Guid paymentMethodId,
            CancellationToken ct = default)
        {
            return _context.PaymentMethods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == paymentMethodId && p.TenantId == tenantId && p.IsActive, ct);
        }

        public Task<string?> GetTenantCurrencyAsync(Guid tenantId, CancellationToken ct = default)
        {
            return _context.SystemSettings
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId)
                .Select(s => s.Currency)
                .FirstOrDefaultAsync(ct);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);

        private IQueryable<ExpenseInvoice> ShiftScope(Guid tenantId, Guid branchId, Guid shiftId)
            => _context.ExpenseInvoices
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId
                         && e.BranchId == branchId
                         && e.CashierShiftId == shiftId);
    }
}
