using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    public sealed class StockAdjustmentCreateDto
    {
        [Range(0, 9999999999.99, ErrorMessage = "New stock cannot be negative.")]
        public decimal NewStock { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "Adjustment reason is required.")]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class UnitCostAdjustmentCreateDto
    {
        // Strictly positive; unit cost is decimal(18,3) so 0.001 is the smallest step.
        [Range(0.001, 9999999999.999, ErrorMessage = "New unit cost must be greater than zero.")]
        public decimal NewUnitCost { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "Adjustment reason is required.")]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class StockAdjustmentResultDto
    {
        public Guid Id { get; set; }
        public Guid MaterialId { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public string? MaterialNameAr { get; set; }

        /// <summary>"StockQuantity" or "UnitCost".</summary>
        public string Kind { get; set; } = string.Empty;

        public decimal PreviousStock { get; set; }
        public decimal NewStock { get; set; }

        /// <summary>Signed delta: NewStock - PreviousStock (+ increase / - decrease).</summary>
        public decimal AdjustmentQuantity { get; set; }

        // Populated only for UnitCost adjustments.
        public decimal? PreviousUnitCost { get; set; }
        public decimal? NewUnitCost { get; set; }

        public string Unit { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
