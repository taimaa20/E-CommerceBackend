using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.DTOs
{
    public class StockBatchDto
    {
        public Guid Id { get; set; }
        public Guid MaterialId { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public string? MaterialNameAr { get; set; }
        public decimal Quantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime ExpiryDate { get; set; }
        public Guid? PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsApproved { get; set; }
        public string Status { get; set; } = "Good";

        // Optional enrichment fields (null for legacy callers, populated by the materials/batches endpoint)
        public string? SupplierName { get; set; }
        public string? SupplierNameAr { get; set; }
        // Proxy for "finished date": last UpdatedAt — bumped when RemainingQuantity hit 0 and Status flipped to Finished.
        public DateTime? FinishedAt { get; set; }
    }

    public class StockBatchCreateDto
    {
        [Required]
        public Guid MaterialId { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        public Guid? PurchaseOrderId { get; set; }

        [Required]
        [MaxLength(100)]
        public string BatchNumber { get; set; } = string.Empty;

        public decimal? UnitCost { get; set; }
    }

    public class StockBatchUpdateDto
    {
        [Range(0.0, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        [Required]
        [MaxLength(100)]
        public string BatchNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comprehensive response DTO for StockBatches with full material and order information.
    /// Used when returning batch details with material names and cost tracking.
    /// </summary>
    public class StockBatchDetailDto
    {
        public Guid Id { get; set; }
        public Guid MaterialId { get; set; }
        
        // Material information with bilingual support
        public string MaterialName { get; set; } = string.Empty;
        public string? MaterialNameAr { get; set; }
        
        // Batch information
        public string BatchNumber { get; set; } = string.Empty;
        public Guid? PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        
        // Quantity and cost tracking
        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingQuantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }
        
        // Status and dates
        public DateTime ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsApproved { get; set; }
    }

    /// <summary>
    /// Simplified DTO for batch listings with essential information.
    /// </summary>
    public class StockBatchListDto
    {
        public Guid Id { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public string? PurchaseOrderNumber { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public string? MaterialNameAr { get; set; }
        public decimal Quantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsApproved { get; set; }
    }
}
