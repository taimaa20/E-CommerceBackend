using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Models
{
    /// <summary>
    /// A currency the business may select, as a manageable lookup row.
    ///
    /// This table is deliberately NOT referenced by a foreign key from anything. Money in this
    /// system is a plain <c>decimal(18,2)</c> and the currency it is expressed in is the single
    /// code on <see cref="SystemSettings.Currency"/>; payment records carry their own ISO code
    /// inside the payment engine's Money value object. Those columns keep storing codes exactly
    /// as they always have — this row only says which codes may be OFFERED for selection and
    /// how one is named and symbolised in the back office.
    ///
    /// Consequences that matter:
    /// <list type="bullet">
    /// <item>Deactivating a row removes it from new selection. It never invalidates history:
    /// a payment or a settings row that already names the code stays readable, because nothing
    /// resolves through this table to be read.</item>
    /// <item><see cref="Code"/> is business identity and is immutable once created — the update
    /// contract carries no Code field. A symbol is never identity; several currencies use "$".</item>
    /// <item>No exchange rate, no conversion factor, no base-currency flag lives here. Adding a
    /// currency adds an option, never a calculation.</item>
    /// </list>
    /// </summary>
    public class Currency : BaseEntity
    {
        /// <summary>ISO-4217 code, upper-cased. Unique per tenant and stable once created.</summary>
        [Required]
        [MaxLength(CodeLength)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(60)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(60)]
        public string? NameAr { get; set; }

        /// <summary>Display symbol. Presentation only — never an identifier.</summary>
        [MaxLength(10)]
        public string? Symbol { get; set; }

        /// <summary>False means "not available for new selection", never "does not exist".</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Lower shows first wherever currencies are listed.</summary>
        public int SortOrder { get; set; }

        /// <summary>ISO-4217 codes are three letters — the same length the payment engine enforces.</summary>
        public const int CodeLength = 3;

        /// <summary>Trims and upper-cases a code so "qar " and "QAR" can never become two rows.</summary>
        public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

        /// <summary>True when the value is a well-formed ISO-4217 code (three A–Z letters).</summary>
        public static bool IsValidCode(string normalizedCode) =>
            normalizedCode.Length == CodeLength && normalizedCode.All(c => c is >= 'A' and <= 'Z');
    }
}
