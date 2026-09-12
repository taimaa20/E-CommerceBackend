using RestaurantPos.Api.DTOs.ExpenseInvoice;

namespace RestaurantPos.Api.Services
{
    public interface IExpenseCategoryService
    {
        Task<ExpenseCategoryDto> CreateAsync(CreateExpenseCategoryDto dto, bool isArabic, CancellationToken ct);
        Task<ExpenseCategoryDto> UpdateAsync(Guid id, UpdateExpenseCategoryDto dto, bool isArabic, CancellationToken ct);
        Task SoftDeleteAsync(Guid id, CancellationToken ct);
        Task<List<ExpenseCategoryDto>> ListAsync(bool isArabic, bool activeOnly, CancellationToken ct);
    }
}
