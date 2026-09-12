using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed class ExpenseCategoryRepository : IExpenseCategoryRepository
    {
        private readonly PosDbContext _context;

        public ExpenseCategoryRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken ct = default)
        {
            var normalised = name.Trim();
            return _context.ExpenseCategories
                .AsNoTracking()
                .AnyAsync(c => c.Name == normalised
                            && (excludeId == null || c.Id != excludeId.Value), ct);
        }

        public Task AddAsync(ExpenseCategory entity, CancellationToken ct = default)
            => _context.ExpenseCategories.AddAsync(entity, ct).AsTask();

        public Task<ExpenseCategory?> GetTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

        public async Task<List<ExpenseCategoryDto>> ListAsync(bool isArabic, bool activeOnly, CancellationToken ct = default)
        {
            var q = _context.ExpenseCategories.AsNoTracking();
            if (activeOnly) q = q.Where(c => c.IsActive);

            // Single-query projection — InvoiceCount uses a correlated sub-query
            // so we avoid an N+1 when the picker renders 20+ categories.
            return await q
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new ExpenseCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    NameAr = c.NameAr,
                    DisplayName = isArabic && c.NameAr != null && c.NameAr != string.Empty
                        ? c.NameAr
                        : c.Name,
                    Color = c.Color,
                    IsActive = c.IsActive,
                    SortOrder = c.SortOrder,
                    InvoiceCount = _context.ExpenseInvoices.Count(i => i.ExpenseCategoryId == c.Id)
                })
                .ToListAsync(ct);
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
