using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services;

/// <summary>
/// Configurable WhatsApp destinations.
///
/// The storefront never asks for a phone number — it asks for a PURPOSE and is handed whichever
/// destination the business assigned to it. That is the whole reason this is a small table
/// rather than three more columns on settings: adding a second number later changes one row
/// here and no storefront component at all.
///
/// Compatibility with what already exists: a tenant that has never configured a destination
/// still gets a working General one, synthesised from the <see cref="SystemSettings.WhatsAppNumber"/>
/// they already have (the same number the public Follow-Us page uses). From the moment they
/// create their first destination onwards the table is the only authority — deactivating or
/// removing every destination really does turn WhatsApp off, instead of silently reviving the
/// settings number.
/// </summary>
public sealed class WhatsAppContactService : IWhatsAppContactService
{
    /// Used only for the settings-derived fallback destination, never written to the database.
    private const string FallbackTemplateEn = "Hello, I would like to ask about your products.";
    private const string FallbackTemplateAr = "مرحباً، أود الاستفسار عن منتجاتكم.";

    private readonly IWhatsAppContactRepository _repository;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<WhatsAppContactService> _logger;

    public WhatsAppContactService(
        IWhatsAppContactRepository repository,
        ISettingsService settingsService,
        ILogger<WhatsAppContactService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<WhatsAppContactDto>> ListAsync(Guid tenantId, CancellationToken ct)
        => (await _repository.ListAsync(tenantId, activeOnly: false, ct)).Select(Map).ToList();

    public async Task<WhatsAppContactDto> CreateAsync(
        Guid tenantId,
        WhatsAppContactUpsertDto dto,
        CancellationToken ct)
    {
        var phone = ValidatePhone(dto.PhoneNumber);
        var contact = new WhatsAppContact
        {
            Id = Guid.NewGuid(),
            // Stamped explicitly — the tenant query filter would hide the row otherwise.
            TenantId = tenantId
        };
        Apply(contact, dto, phone);

        await _repository.AddAsync(contact, ct);
        await DemoteOtherDefaultsAsync(tenantId, contact, ct);
        await _repository.SaveAsync(ct);

        _logger.LogInformation(
            "WhatsApp contact {ContactId} created for tenant {TenantId} (purpose={Purpose})",
            contact.Id,
            tenantId,
            contact.Purpose);
        return Map(contact);
    }

    public async Task<WhatsAppContactDto> UpdateAsync(
        Guid tenantId,
        Guid id,
        WhatsAppContactUpsertDto dto,
        CancellationToken ct)
    {
        var contact = await _repository.GetTrackedAsync(tenantId, id, ct)
            ?? throw new NotFoundException(nameof(WhatsAppContact), id);

        var phone = ValidatePhone(dto.PhoneNumber);
        Apply(contact, dto, phone);

        await DemoteOtherDefaultsAsync(tenantId, contact, ct);
        await _repository.SaveAsync(ct);

        _logger.LogInformation("WhatsApp contact {ContactId} updated for tenant {TenantId}", id, tenantId);
        return Map(contact);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var contact = await _repository.GetTrackedAsync(tenantId, id, ct)
            ?? throw new NotFoundException(nameof(WhatsAppContact), id);

        // Soft delete, in line with every other lookup here. A destination carries no history,
        // so removing one only withdraws an option.
        contact.DeletedAt = DateTime.UtcNow;
        contact.IsActive = false;
        await _repository.SaveAsync(ct);

        _logger.LogInformation("WhatsApp contact {ContactId} removed for tenant {TenantId}", id, tenantId);
    }

    public async Task<IReadOnlyList<WhatsAppContactPublicDto>> GetPublicAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        if (!await _repository.HasEverConfiguredAsync(tenantId, ct))
            return await BuildSettingsFallbackAsync(tenantId, ct);

        // One destination per purpose. The repository already orders default-first within a
        // purpose, so the first row of each group is the one the resolver would pick.
        return (await _repository.ListAsync(tenantId, activeOnly: true, ct))
            .Where(IsUsable)
            .GroupBy(contact => contact.Purpose)
            .Select(group => ToPublic(group.First()))
            .ToList();
    }

    /// A tenant that has never configured a destination keeps the WhatsApp number they already
    /// have in settings, so the storefront works before anyone opens the new admin screen.
    private async Task<IReadOnlyList<WhatsAppContactPublicDto>> BuildSettingsFallbackAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var settings = await _settingsService.GetSettingsAsync(tenantId, ct);
        var phone = WhatsAppContact.NormalizePhone(settings.WhatsAppNumber);
        if (!IsUsablePhone(phone)) return [];

        return
        [
            new WhatsAppContactPublicDto
            {
                Phone = phone,
                Purpose = WhatsAppContactPurpose.General,
                MessageTemplate = FallbackTemplateEn,
                MessageTemplateAr = FallbackTemplateAr
            }
        ];
    }

    /// A destination whose number cannot open a chat is withheld rather than rendered as a
    /// button that goes nowhere. Configuration is validated on write; this guards rows written
    /// before that validation, and settings-derived numbers.
    private static bool IsUsable(WhatsAppContact contact)
        => IsUsablePhone(WhatsAppContact.NormalizePhone(contact.PhoneNumber));

    private static bool IsUsablePhone(string digits)
        => digits.Length is >= WhatsAppContact.MinPhoneDigits and <= WhatsAppContact.MaxPhoneDigits;

    private static string ValidatePhone(string? phoneNumber)
    {
        var digits = WhatsAppContact.NormalizePhone(phoneNumber);
        if (!IsUsablePhone(digits))
        {
            throw new ValidationException(
                "Enter the WhatsApp number in international form, for example +974 1234 5678.");
        }
        return (phoneNumber ?? string.Empty).Trim();
    }

    /// Exactly one default per purpose, so the resolver's choice is never ambiguous.
    private async Task DemoteOtherDefaultsAsync(Guid tenantId, WhatsAppContact contact, CancellationToken ct)
    {
        if (!contact.IsDefault) return;

        foreach (var sibling in await _repository.GetTrackedByPurposeAsync(tenantId, contact.Purpose, ct))
        {
            if (sibling.Id != contact.Id) sibling.IsDefault = false;
        }
    }

    private static void Apply(WhatsAppContact contact, WhatsAppContactUpsertDto dto, string phoneNumber)
    {
        var label = (dto.Label ?? string.Empty).Trim();
        if (label.Length == 0)
            throw new ValidationException("A label is required so the destination can be told apart.");
        if (!Enum.IsDefined(dto.Purpose))
            throw new ValidationException("Unknown WhatsApp destination purpose.");

        contact.Label = label;
        contact.LabelAr = TrimToNull(dto.LabelAr);
        contact.PhoneNumber = phoneNumber;
        contact.Purpose = dto.Purpose;
        contact.MessageTemplate = TrimToNull(dto.MessageTemplate);
        contact.MessageTemplateAr = TrimToNull(dto.MessageTemplateAr);
        contact.IsActive = dto.IsActive;
        contact.IsDefault = dto.IsDefault;
        contact.SortOrder = dto.SortOrder;
    }

    private static WhatsAppContactDto Map(WhatsAppContact contact) => new()
    {
        Id = contact.Id,
        Label = contact.Label,
        LabelAr = contact.LabelAr,
        PhoneNumber = contact.PhoneNumber,
        Purpose = contact.Purpose,
        MessageTemplate = contact.MessageTemplate,
        MessageTemplateAr = contact.MessageTemplateAr,
        IsActive = contact.IsActive,
        IsDefault = contact.IsDefault,
        SortOrder = contact.SortOrder
    };

    private static WhatsAppContactPublicDto ToPublic(WhatsAppContact contact) => new()
    {
        Phone = WhatsAppContact.NormalizePhone(contact.PhoneNumber),
        Purpose = contact.Purpose,
        MessageTemplate = contact.MessageTemplate ?? string.Empty,
        // A business that wrote only one template gets it in both languages rather than an
        // empty Arabic message.
        MessageTemplateAr = contact.MessageTemplateAr ?? contact.MessageTemplate ?? string.Empty
    };

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
