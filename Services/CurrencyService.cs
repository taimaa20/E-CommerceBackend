using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services;

/// <summary>
/// Manageable currency lookup.
///
/// Two rules keep this safe to run beside live trading:
/// <list type="number">
/// <item>A code is never edited. <see cref="CurrencyUpdateDto"/> has no Code field, so the
/// spelling a payment or a settings row already carries can never be re-pointed at a different
/// currency after the fact.</item>
/// <item>A row is never deleted, only deactivated — and the currency the business is currently
/// operating in cannot even be deactivated, because that would leave the store unable to
/// re-select its own currency.</item>
/// </list>
/// Nothing here reads or writes a monetary amount.
/// </summary>
public sealed class CurrencyService : ICurrencyService
{
    private readonly ICurrencyRepository _repository;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<CurrencyService> _logger;

    public CurrencyService(
        ICurrencyRepository repository,
        ISettingsService settingsService,
        ILogger<CurrencyService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<CurrencyDto>> ListAsync(
        Guid tenantId,
        bool activeOnly,
        bool isArabic,
        CancellationToken ct)
    {
        var storeCurrency = await GetStoreCurrencyAsync(tenantId, ct);
        var rows = await _repository.ListAsync(tenantId, activeOnly, ct);
        return rows.Select(row => Map(row, storeCurrency, isArabic)).ToList();
    }

    public async Task<CurrencyDto> CreateAsync(
        Guid tenantId,
        CurrencyCreateDto dto,
        bool isArabic,
        CancellationToken ct)
    {
        var code = Currency.Normalize(dto.Code);
        if (!Currency.IsValidCode(code))
            throw new ValidationException("A currency code must be three letters, as in QAR or USD.");
        if (await _repository.CodeExistsAsync(tenantId, code, exceptId: null, ct))
            throw new ValidationException($"Currency '{code}' already exists.");

        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            // Stamped explicitly: SaveChanges only auto-fills the audit timestamps, and without
            // this the row is invisible to the next read through the tenant query filter.
            TenantId = tenantId,
            Code = code
        };
        Apply(currency, dto.Name, dto.NameAr, dto.Symbol, dto.IsActive, dto.SortOrder);

        await _repository.AddAsync(currency, ct);
        await _repository.SaveAsync(ct);

        _logger.LogInformation("Currency {CurrencyCode} created for tenant {TenantId}", code, tenantId);
        return Map(currency, await GetStoreCurrencyAsync(tenantId, ct), isArabic);
    }

    public async Task<CurrencyDto> UpdateAsync(
        Guid tenantId,
        Guid id,
        CurrencyUpdateDto dto,
        bool isArabic,
        CancellationToken ct)
    {
        var currency = await _repository.GetTrackedAsync(tenantId, id, ct)
            ?? throw new NotFoundException(nameof(Currency), id);

        var storeCurrency = await GetStoreCurrencyAsync(tenantId, ct);
        var isStoreCurrency = string.Equals(currency.Code, storeCurrency, StringComparison.Ordinal);
        if (isStoreCurrency && !dto.IsActive)
        {
            throw new ValidationException(
                $"'{currency.Code}' is the currency this store operates in and cannot be deactivated. " +
                "Change the store currency in Settings first.");
        }

        Apply(currency, dto.Name, dto.NameAr, dto.Symbol, dto.IsActive, dto.SortOrder);
        await _repository.SaveAsync(ct);

        _logger.LogInformation(
            "Currency {CurrencyCode} updated for tenant {TenantId} (active={IsActive})",
            currency.Code,
            tenantId,
            currency.IsActive);
        return Map(currency, storeCurrency, isArabic);
    }

    /// The code the business operates in today. It is read straight off settings and is never
    /// written here — this service has no authority over the store's currency, only over which
    /// codes may be offered.
    private async Task<string> GetStoreCurrencyAsync(Guid tenantId, CancellationToken ct)
        => Currency.Normalize((await _settingsService.GetSettingsAsync(tenantId, ct)).Currency);

    private static void Apply(
        Currency currency,
        string name,
        string? nameAr,
        string? symbol,
        bool isActive,
        int sortOrder)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
            throw new ValidationException("A currency name is required.");

        currency.Name = trimmedName;
        currency.NameAr = TrimToNull(nameAr);
        currency.Symbol = TrimToNull(symbol);
        currency.IsActive = isActive;
        currency.SortOrder = sortOrder;
    }

    private static CurrencyDto Map(Currency currency, string storeCurrency, bool isArabic) => new()
    {
        Id = currency.Id,
        Code = currency.Code,
        Name = currency.Name,
        NameAr = currency.NameAr,
        DisplayName = isArabic && !string.IsNullOrEmpty(currency.NameAr) ? currency.NameAr! : currency.Name,
        Symbol = currency.Symbol,
        IsActive = currency.IsActive,
        SortOrder = currency.SortOrder,
        IsStoreCurrency = string.Equals(currency.Code, storeCurrency, StringComparison.Ordinal)
    };

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
