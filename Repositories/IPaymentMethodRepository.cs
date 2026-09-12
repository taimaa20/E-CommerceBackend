using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IPaymentMethodRepository
    {
        Task<List<PaymentMethod>> GetActiveAsync(CancellationToken ct = default);
        Task<List<PaymentMethod>> GetAllAsync(CancellationToken ct = default);
        Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct = default);
        Task ClearDefaultAsync(Guid? excludeId, CancellationToken ct = default);
        Task<PaymentMethod> AddAsync(PaymentMethod method, CancellationToken ct = default);
        Task<PaymentMethod> UpdateAsync(PaymentMethod method, CancellationToken ct = default);
        Task DeleteAsync(PaymentMethod method, CancellationToken ct = default);
    }
}
