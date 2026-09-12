using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IExpenseCategoryRepository
    {
        Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken ct = default);
        Task AddAsync(ExpenseCategory entity, CancellationToken ct = default);
        Task<ExpenseCategory?> GetTrackedAsync(Guid id, CancellationToken ct = default);
        Task<List<ExpenseCategoryDto>> ListAsync(bool isArabic, bool activeOnly, CancellationToken ct = default);
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
