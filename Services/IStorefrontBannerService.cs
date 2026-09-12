using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services;

/// Promotional banner management and the public read the storefront renders.
public interface IStorefrontBannerService
{
    Task<IReadOnlyList<StorefrontBannerDto>> GetAllAsync(Guid tenantId, CancellationToken ct);

    /// Banners the storefront may show right now: active, in-window and with a link the
    /// storefront is allowed to follow. Never returns a scheduled-but-not-started banner.
    Task<IReadOnlyList<StorefrontBannerPublicDto>> GetLiveAsync(
        Guid tenantId,
        StorefrontBannerPlacement placement,
        CancellationToken ct);

    Task<StorefrontBannerDto> CreateAsync(Guid tenantId, StorefrontBannerUpsertDto dto, CancellationToken ct);

    Task<StorefrontBannerDto> UpdateAsync(Guid tenantId, Guid id, StorefrontBannerUpsertDto dto, CancellationToken ct);

    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);
}
