using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

public interface IWhatsAppContactRepository
{
    /// <param name="activeOnly">True for the public storefront read; false for back-office
    /// management, which must keep seeing a deactivated destination to re-enable it.</param>
    Task<IReadOnlyList<WhatsAppContact>> ListAsync(Guid tenantId, bool activeOnly, CancellationToken ct);

    /// True when this tenant has EVER configured a destination, removed ones included. It is
    /// what separates "never set up" — where the store's settings number still applies — from
    /// "deliberately emptied", where WhatsApp is off.
    Task<bool> HasEverConfiguredAsync(Guid tenantId, CancellationToken ct);

    Task<WhatsAppContact?> GetTrackedAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// Tracked rows of one purpose, so promoting a new default can demote the previous one in
    /// the same unit of work rather than leaving two defaults behind.
    Task<IReadOnlyList<WhatsAppContact>> GetTrackedByPurposeAsync(
        Guid tenantId,
        WhatsAppContactPurpose purpose,
        CancellationToken ct);

    Task AddAsync(WhatsAppContact contact, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
