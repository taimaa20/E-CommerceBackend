using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IDeliveryPartnerRepository
    {
        Task<List<DeliveryPartnerDto>> GetActiveAsync(CancellationToken ct = default);
        Task<(List<DeliveryPartnerDto> Items, int TotalCount)> GetPagedAsync(string? search, DeliveryPartnerStatus? status, int page, int pageSize, CancellationToken ct = default);
        Task<DeliveryPartner?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct = default);
        Task<DeliveryPartner> AddAsync(DeliveryPartner partner, CancellationToken ct = default);
        Task<DeliveryPartner> UpdateAsync(DeliveryPartner partner, CancellationToken ct = default);
        Task DeleteAsync(DeliveryPartner partner, CancellationToken ct = default);
        Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct = default);
        Task<bool> PartnerExistsAsync(Guid partnerId, CancellationToken ct = default);
        Task<DeliveryPartnerProduct?> GetMappingAsync(Guid productId, Guid partnerId, CancellationToken ct = default);
        Task<DeliveryPartnerProduct> SaveMappingAsync(DeliveryPartnerProduct mapping, CancellationToken ct = default);
        Task<List<DeliveryPartnerProductMappingDto>> GetProductMappingsAsync(Guid productId, bool activePartnersOnly, CancellationToken ct = default);
        Task<(List<DeliveryPartnerProductMappingDto> Items, int TotalCount)> GetPartnerProductMappingsAsync(Guid partnerId, string? search, bool? enabledOnly, Guid? categoryId, int page, int pageSize, CancellationToken ct = default);
        Task AddActivityLogAsync(DeliveryPartnerActivityLog log, CancellationToken ct = default);
    }
}
