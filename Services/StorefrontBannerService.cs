using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services;

public sealed class StorefrontBannerService : IStorefrontBannerService
{
    private readonly IStorefrontBannerRepository _repository;
    private readonly ILogger<StorefrontBannerService> _logger;

    public StorefrontBannerService(
        IStorefrontBannerRepository repository,
        ILogger<StorefrontBannerService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<StorefrontBannerDto>> GetAllAsync(Guid tenantId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return (await _repository.GetAllAsync(tenantId, ct))
            .Select(banner => MapAdmin(banner, now))
            .ToList();
    }

    public async Task<IReadOnlyList<StorefrontBannerPublicDto>> GetLiveAsync(
        Guid tenantId,
        StorefrontBannerPlacement placement,
        CancellationToken ct)
    {
        // One clock for the whole request, so two banners cannot disagree about "now".
        var now = DateTime.UtcNow;
        return (await _repository.GetActiveAsync(tenantId, placement, ct))
            .Where(banner => IsLive(banner, now))
            .Select(MapPublic)
            .ToList();
    }

    public async Task<StorefrontBannerDto> CreateAsync(Guid tenantId, StorefrontBannerUpsertDto dto, CancellationToken ct)
    {
        Validate(dto);

        var banner = new StorefrontBanner { Id = Guid.NewGuid(), TenantId = tenantId };
        Apply(banner, dto);

        await _repository.AddAsync(banner, ct);
        await _repository.SaveAsync(ct);

        _logger.LogInformation("Storefront banner {BannerId} created for tenant {TenantId}", banner.Id, tenantId);
        return MapAdmin(banner, DateTime.UtcNow);
    }

    public async Task<StorefrontBannerDto> UpdateAsync(Guid tenantId, Guid id, StorefrontBannerUpsertDto dto, CancellationToken ct)
    {
        Validate(dto);

        var banner = await _repository.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("Banner", id);
        Apply(banner, dto);
        await _repository.SaveAsync(ct);

        _logger.LogInformation("Storefront banner {BannerId} updated for tenant {TenantId}", id, tenantId);
        return MapAdmin(banner, DateTime.UtcNow);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var banner = await _repository.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("Banner", id);

        // Soft delete: the global query filter hides it and no history is destroyed.
        banner.DeletedAt = DateTime.UtcNow;
        banner.IsActive = false;
        await _repository.SaveAsync(ct);

        _logger.LogInformation("Storefront banner {BannerId} removed for tenant {TenantId}", id, tenantId);
    }

    // -- Rules ---------------------------------------------------------------

    private static void Validate(StorefrontBannerUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ValidationException("Banner title is required.");
        if (string.IsNullOrWhiteSpace(dto.ImageUrl))
            throw new ValidationException("Banner image is required.");
        if (!Enum.IsDefined(dto.LinkType))
            throw new ValidationException("Unknown banner destination type.");
        if (!Enum.IsDefined(dto.Placement))
            throw new ValidationException("Unknown banner placement.");
        if (dto.StartsAtUtc.HasValue && dto.EndsAtUtc.HasValue && dto.EndsAtUtc.Value <= dto.StartsAtUtc.Value)
            throw new ValidationException("The banner end time must be after its start time.");
        if (!IsLinkValueValid(dto.LinkType, dto.LinkValue))
            throw new ValidationException("The banner destination is incomplete or not allowed.");
    }

    /// <summary>
    /// A destination is stored only when it is a shape the storefront knows how to build a
    /// route from. External links are limited to absolute https, so a stored value can never
    /// become a script or data URL on the page.
    /// </summary>
    private static bool IsLinkValueValid(StorefrontBannerLinkType type, string? value)
    {
        var trimmed = value?.Trim();
        return type switch
        {
            StorefrontBannerLinkType.None or StorefrontBannerLinkType.Offers => true,
            StorefrontBannerLinkType.Category or StorefrontBannerLinkType.Product => Guid.TryParse(trimmed, out _),
            StorefrontBannerLinkType.Search => !string.IsNullOrWhiteSpace(trimmed),
            StorefrontBannerLinkType.External => Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps,
            _ => false
        };
    }

    private static bool IsLive(StorefrontBanner banner, DateTime nowUtc)
        => (!banner.StartsAtUtc.HasValue || banner.StartsAtUtc.Value <= nowUtc)
            && (!banner.EndsAtUtc.HasValue || banner.EndsAtUtc.Value > nowUtc);

    private static void Apply(StorefrontBanner banner, StorefrontBannerUpsertDto dto)
    {
        banner.Title = dto.Title.Trim();
        banner.TitleAr = Clean(dto.TitleAr);
        banner.Subtitle = Clean(dto.Subtitle);
        banner.SubtitleAr = Clean(dto.SubtitleAr);
        banner.ImageUrl = dto.ImageUrl.Trim();
        banner.ImageKey = Clean(dto.ImageKey);
        banner.MobileImageUrl = Clean(dto.MobileImageUrl);
        banner.MobileImageKey = Clean(dto.MobileImageKey);
        banner.CtaLabel = Clean(dto.CtaLabel);
        banner.CtaLabelAr = Clean(dto.CtaLabelAr);
        banner.LinkType = dto.LinkType;
        banner.LinkValue = dto.LinkType == StorefrontBannerLinkType.None ? null : Clean(dto.LinkValue);
        banner.Placement = dto.Placement;
        banner.IsActive = dto.IsActive;
        banner.StartsAtUtc = Utc(dto.StartsAtUtc);
        banner.EndsAtUtc = Utc(dto.EndsAtUtc);
        banner.SortOrder = dto.SortOrder;
    }

    /// <summary>
    /// Npgsql rejects a timestamptz whose Kind is Unspecified, and a client may post either a
    /// Z-suffixed instant or a bare string. Normalising here keeps that out of the entity.
    /// </summary>
    private static DateTime? Utc(DateTime? value)
        => value is null
            ? null
            : value.Value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StorefrontBannerDto MapAdmin(StorefrontBanner banner, DateTime nowUtc) => new()
    {
        Id = banner.Id,
        Title = banner.Title,
        TitleAr = banner.TitleAr,
        Subtitle = banner.Subtitle,
        SubtitleAr = banner.SubtitleAr,
        ImageUrl = banner.ImageUrl,
        ImageKey = banner.ImageKey,
        MobileImageUrl = banner.MobileImageUrl,
        MobileImageKey = banner.MobileImageKey,
        CtaLabel = banner.CtaLabel,
        CtaLabelAr = banner.CtaLabelAr,
        LinkType = banner.LinkType,
        LinkValue = banner.LinkValue,
        Placement = banner.Placement,
        IsActive = banner.IsActive,
        StartsAtUtc = banner.StartsAtUtc,
        EndsAtUtc = banner.EndsAtUtc,
        SortOrder = banner.SortOrder,
        IsLiveNow = banner.IsActive && IsLive(banner, nowUtc),
    };

    private static StorefrontBannerPublicDto MapPublic(StorefrontBanner banner) => new()
    {
        Id = banner.Id,
        Title = banner.Title,
        TitleAr = banner.TitleAr,
        Subtitle = banner.Subtitle,
        SubtitleAr = banner.SubtitleAr,
        ImageUrl = banner.ImageUrl,
        ImageKey = banner.ImageKey,
        MobileImageUrl = banner.MobileImageUrl,
        MobileImageKey = banner.MobileImageKey,
        CtaLabel = banner.CtaLabel,
        CtaLabelAr = banner.CtaLabelAr,
        LinkType = banner.LinkType,
        LinkValue = banner.LinkValue,
    };
}
