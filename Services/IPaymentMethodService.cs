using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IPaymentMethodService
    {
        Task<List<PaymentMethodDto>> GetActiveAsync(CancellationToken ct = default);
        Task<List<PaymentMethodDto>> GetAllAsync(CancellationToken ct = default);
        Task<PaymentMethodDto> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<PaymentMethodDto> CreateAsync(PaymentMethodCreateDto dto, CancellationToken ct = default);
        Task<PaymentMethodDto> UpdateAsync(Guid id, PaymentMethodUpdateDto dto, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
    }
}
