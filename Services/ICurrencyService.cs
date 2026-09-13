using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services;

/// <summary>
/// Manageable currency lookup. It governs which currency codes may be OFFERED for selection and
/// how each is named — it does not store, convert or interpret any monetary amount.
/// </summary>
public interface ICurrencyService
{
    Task<IReadOnlyList<CurrencyDto>> ListAsync(Guid tenantId, bool activeOnly, bool isArabic, CancellationToken ct);

    Task<CurrencyDto> CreateAsync(Guid tenantId, CurrencyCreateDto dto, bool isArabic, CancellationToken ct);

    /// The code is not part of the update contract — it is stable identity once created.
    Task<CurrencyDto> UpdateAsync(Guid tenantId, Guid id, CurrencyUpdateDto dto, bool isArabic, CancellationToken ct);
}
