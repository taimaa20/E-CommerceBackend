using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs;

/// Back-office view of one WhatsApp destination.
public sealed class WhatsAppContactDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? LabelAr { get; init; }
    public string PhoneNumber { get; init; } = string.Empty;
    public WhatsAppContactPurpose Purpose { get; init; }
    public string? MessageTemplate { get; init; }
    public string? MessageTemplateAr { get; init; }
    public bool IsActive { get; init; }
    public bool IsDefault { get; init; }
    public int SortOrder { get; init; }
}

public sealed class WhatsAppContactUpsertDto
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string Label { get; init; } = string.Empty;

    [StringLength(80)]
    public string? LabelAr { get; init; }

    [Required, StringLength(30, MinimumLength = 1)]
    public string PhoneNumber { get; init; } = string.Empty;

    public WhatsAppContactPurpose Purpose { get; init; } = WhatsAppContactPurpose.General;

    [StringLength(1000)]
    public string? MessageTemplate { get; init; }

    [StringLength(1000)]
    public string? MessageTemplateAr { get; init; }

    public bool IsActive { get; init; } = true;

    public bool IsDefault { get; init; }

    public int SortOrder { get; init; }
}

/// What the public storefront is told about a destination: enough to open a chat, nothing more.
/// The back-office label and the row's identity stay inside the admin API.
///
/// Both languages travel together, exactly like every other bilingual storefront payload. The
/// store lets a shopper switch language without reloading, so a template resolved server-side
/// would go stale the moment they did.
public sealed class WhatsAppContactPublicDto
{
    /// Digits only — the storefront concatenates this into the click-to-chat URL verbatim.
    public string Phone { get; init; } = string.Empty;

    public WhatsAppContactPurpose Purpose { get; init; }

    /// Empty means "open the chat with no prefilled text" — a working link, not a broken one.
    public string MessageTemplate { get; init; } = string.Empty;

    /// Falls back to <see cref="MessageTemplate"/> when the business configured only one.
    public string MessageTemplateAr { get; init; } = string.Empty;
}
