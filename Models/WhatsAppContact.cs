using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Models
{
    /// <summary>
    /// What a WhatsApp contact row is for. The storefront asks for a purpose and gets the
    /// destination the business assigned to it — which is what keeps the store from being
    /// wired to one permanent phone number.
    ///
    /// Append only: the numeric values travel in API responses.
    /// </summary>
    public enum WhatsAppContactPurpose
    {
        /// <summary>The store's general "talk to us" destination.</summary>
        General = 0,

        /// <summary>Questions about a specific product, from a product card or product page.</summary>
        ProductInquiry = 1,

        /// <summary>After-sale help: an existing order, a return, a complaint.</summary>
        CustomerService = 2
    }

    /// <summary>
    /// One configurable WhatsApp destination: who it is, which number it reaches and what the
    /// prefilled message says.
    ///
    /// Both the number and the message are data, never code — the business changes either from
    /// the back office with no deployment. Several rows may exist so general enquiries, product
    /// enquiries and customer service can reach different numbers later without any storefront
    /// component learning about it; today one General row is enough and the resolution rule
    /// below already handles both cases.
    ///
    /// Resolution for a requested purpose: active rows of that purpose, default first, then
    /// <see cref="SortOrder"/>; if that purpose has none, the General destination answers. A
    /// storefront surface therefore always asks for what it wants, never for a phone number.
    /// </summary>
    public class WhatsAppContact : BaseEntity
    {
        /// <summary>Back-office label — "Sales", "Support". Never shown as the button's own text.</summary>
        [Required]
        [MaxLength(80)]
        public string Label { get; set; } = string.Empty;

        [MaxLength(80)]
        public string? LabelAr { get; set; }

        /// <summary>
        /// The destination in international form. Stored as the business typed it; the digits
        /// are extracted when the click-to-chat link is built, so "+974 1234 5678" and
        /// "97412345678" reach the same chat.
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        public WhatsAppContactPurpose Purpose { get; set; } = WhatsAppContactPurpose.General;

        /// <summary>
        /// The message prefilled into WhatsApp, with placeholders the storefront substitutes
        /// from the product actually being viewed. Null falls back to no prefilled text, which
        /// still opens a working chat.
        /// </summary>
        [MaxLength(1000)]
        public string? MessageTemplate { get; set; }

        [MaxLength(1000)]
        public string? MessageTemplateAr { get; set; }

        /// <summary>False means the storefront never offers this destination.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Wins among several active rows sharing a purpose.</summary>
        public bool IsDefault { get; set; }

        public int SortOrder { get; set; }

        /// <summary>Digits only, as the wa.me click-to-chat URL requires them.</summary>
        public static string NormalizePhone(string? phoneNumber) =>
            new((phoneNumber ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

        /// <summary>
        /// Shortest dialable international number is 7 digits (a few national plans); the
        /// longest an E.164 number may be is 15. Anything outside that cannot reach a chat,
        /// so it is refused at configuration time instead of producing a dead button.
        /// </summary>
        public const int MinPhoneDigits = 7;
        public const int MaxPhoneDigits = 15;
    }
}
