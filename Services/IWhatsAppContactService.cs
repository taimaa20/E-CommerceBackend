using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services;

/// <summary>
/// Configurable WhatsApp destinations. Both the number and the prefilled message are data the
/// business edits from the back office, so neither ever needs a deployment to change.
/// </summary>
public interface IWhatsAppContactService
{
    Task<IReadOnlyList<WhatsAppContactDto>> ListAsync(Guid tenantId, CancellationToken ct);

    Task<WhatsAppContactDto> CreateAsync(Guid tenantId, WhatsAppContactUpsertDto dto, CancellationToken ct);

    Task<WhatsAppContactDto> UpdateAsync(Guid tenantId, Guid id, WhatsAppContactUpsertDto dto, CancellationToken ct);

    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// Active destinations for the public storefront, one per purpose, in both languages. An
    /// empty list means the store simply shows no WhatsApp action.
    Task<IReadOnlyList<WhatsAppContactPublicDto>> GetPublicAsync(Guid tenantId, CancellationToken ct);
}
