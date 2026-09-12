using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public interface IDeliveryPartnerService
    {
        Task<List<DeliveryPartnerDto>> GetActiveAsync(CancellationToken ct = default);
        Task<DeliveryPartnerPagedResultDto> GetPagedAsync(string? search, DeliveryPartnerStatus? status, int page, int pageSize, CancellationToken ct = default);
        Task<DeliveryPartnerDto> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<DeliveryPartnerDto> CreateAsync(DeliveryPartnerUpsertDto dto, CancellationToken ct = default);
        Task<DeliveryPartnerDto> UpdateAsync(Guid id, DeliveryPartnerUpsertDto dto, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
        Task<List<DeliveryPartnerProductMappingDto>> GetProductMappingsAsync(Guid productId, bool activePartnersOnly, CancellationToken ct = default);
        Task<DeliveryPartnerProductPagedResultDto> GetPartnerProductsAsync(Guid partnerId, string? search, bool? enabledOnly, Guid? categoryId, int page, int pageSize, CancellationToken ct = default);
        Task<DeliveryPartnerProductMappingDto> UpsertProductMappingAsync(Guid productId, Guid partnerId, DeliveryPartnerProductMappingUpsertDto dto, CancellationToken ct = default);
    }
}
