using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

/// EF access for <see cref="StorefrontBanner"/>. Schedule and link rules live in the
/// service; this seam only reads and writes rows.
public interface IStorefrontBannerRepository
{
    Task<IReadOnlyList<StorefrontBanner>> GetAllAsync(Guid tenantId, CancellationToken ct);

    /// Active banners of a placement, ordered for display. The caller still applies the
    /// schedule — the window is evaluated once per request against one clock.
    Task<IReadOnlyList<StorefrontBanner>> GetActiveAsync(
        Guid tenantId,
        StorefrontBannerPlacement placement,
        CancellationToken ct);

    Task<StorefrontBanner?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task AddAsync(StorefrontBanner banner, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
