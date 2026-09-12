using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public class OrderNumberingConfigDto
    {
        public OrderNumberResetStrategy ResetStrategy { get; set; } = OrderNumberResetStrategy.Monthly;
        public OrderNumberSequenceScope Scope         { get; set; } = OrderNumberSequenceScope.PerOrderType;

        [MaxLength(16)]
        public string? Prefix { get; set; }

        public bool IncludeDate        { get; set; }
        public bool IncludeMonth       { get; set; } = true;
        public bool IncludeYear        { get; set; } = true;
        public bool IncludeShiftNumber { get; set; }
        public bool IncludeBranchCode  { get; set; }

        [MaxLength(16)]
        public string? BranchCode { get; set; }

        /// <summary>
        /// Server-rendered sample using the supplied flags. Filled by GET only.
        /// Lets the settings UI show "TA-20260609-1" without round-tripping.
        /// </summary>
        public string? PreviewExample { get; set; }
    }
}
