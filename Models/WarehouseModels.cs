using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    public enum WarehouseType
    {
        Main = 0,
        Sub = 1,
        Production = 2
    }

    /// <summary>
    /// A physical or logical storage location for raw materials. Every tenant
    /// has exactly one <see cref="WarehouseType.Main"/> warehouse — auto-seeded
    /// by the data migration so legacy materials route there by default.
    /// </summary>
    public class Warehouse : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? NameAr { get; set; }

        public WarehouseType Type { get; set; } = WarehouseType.Sub;

        public bool IsActive { get; set; } = true;

        public ICollection<RawMaterialInventory> Inventories { get; set; } = new List<RawMaterialInventory>();
    }

    /// <summary>
    /// Per-warehouse stock balance for a raw material. Quantity is the on-hand
    /// physical inventory at this location. Existing FIFO costing on
    /// <see cref="StockBatch"/> is unchanged; this table is the warehouse-level
    /// ledger that transfers and reports operate on.
    /// </summary>
    public class RawMaterialInventory : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid RawMaterialId { get; set; }
        public RawMaterial? RawMaterial { get; set; }

        public Guid WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumQuantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ReorderLevel { get; set; }
    }

    public enum InventoryTransferStatus
    {
        Completed = 0,
        Cancelled = 1
    }

    /// <summary>
    /// Atomic move of one raw material between two warehouses. Persisted as a
    /// single audit row; the corresponding RawMaterialInventory.Quantity
    /// adjustments are made in the same transaction.
    /// </summary>
    public class InventoryTransfer : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid FromWarehouseId { get; set; }
        public Warehouse? FromWarehouse { get; set; }

        public Guid ToWarehouseId { get; set; }
        public Warehouse? ToWarehouse { get; set; }

        public Guid RawMaterialId { get; set; }
        public RawMaterial? RawMaterial { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        public Guid? PerformedById { get; set; }
        public User? PerformedBy { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public InventoryTransferStatus Status { get; set; } = InventoryTransferStatus.Completed;
    }

    /// <summary>
    /// Junction table assigning a product to one or more kitchen printers.
    /// Empty for legacy products — routing falls back to the existing
    /// Kitchen → first-active-printer rule.
    /// </summary>
    public class ProductKitchenPrinter : BaseEntity
    {
        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        public Guid KitchenPrinterId { get; set; }
        public Printer? KitchenPrinter { get; set; }
    }
}
