using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public class WarehouseDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public WarehouseType Type { get; set; }
        public bool IsActive { get; set; }
    }

    public class WarehouseCreateDto
    {
        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? NameAr { get; set; }

        [Range(0, 2)]
        public int Type { get; set; } = 1; // Sub by default

        public bool IsActive { get; set; } = true;
    }

    public class WarehouseUpdateDto
    {
        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? NameAr { get; set; }

        [Range(0, 2)]
        public int Type { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class RawMaterialInventoryDto
    {
        public Guid Id { get; set; }
        public Guid RawMaterialId { get; set; }
        public string RawMaterialName { get; set; } = string.Empty;
        public string? RawMaterialNameAr { get; set; }
        public Guid WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string? WarehouseNameAr { get; set; }
        public string WarehouseCode { get; set; } = string.Empty;
        public WarehouseType WarehouseType { get; set; }
        public decimal Quantity { get; set; }
        public decimal MinimumQuantity { get; set; }
        public decimal ReorderLevel { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class RawMaterialInventoryUpsertDto
    {
        [Required]
        public Guid RawMaterialId { get; set; }

        [Required]
        public Guid WarehouseId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal MinimumQuantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal ReorderLevel { get; set; }
    }

    public class InventoryTransferDto
    {
        public Guid Id { get; set; }
        public Guid FromWarehouseId { get; set; }
        public string FromWarehouseName { get; set; } = string.Empty;
        public Guid ToWarehouseId { get; set; }
        public string ToWarehouseName { get; set; } = string.Empty;
        public Guid RawMaterialId { get; set; }
        public string RawMaterialName { get; set; } = string.Empty;
        public string? RawMaterialNameAr { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public Guid? PerformedById { get; set; }
        public string? PerformedByName { get; set; }
        public InventoryTransferStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class InventoryTransferCreateDto
    {
        [Required]
        public Guid FromWarehouseId { get; set; }

        [Required]
        public Guid ToWarehouseId { get; set; }

        [Required]
        public Guid RawMaterialId { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
        public decimal Quantity { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class StockByWarehouseRowDto
    {
        public Guid WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string WarehouseCode { get; set; } = string.Empty;
        public Guid RawMaterialId { get; set; }
        public string RawMaterialName { get; set; } = string.Empty;
        public string? RawMaterialNameAr { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal MinimumQuantity { get; set; }
        public decimal ReorderLevel { get; set; }
        public bool IsLow { get; set; }
    }
}
