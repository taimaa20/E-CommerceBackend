using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Warehouse CRUD + per-warehouse inventory + transfers. All operations are
    /// tenant-scoped via the global query filter on PosDbContext.
    /// </summary>
    public interface IWarehouseService
    {
        Task<List<WarehouseDto>> GetAllAsync(bool isArabic, CancellationToken ct);
        Task<WarehouseDto?> GetByIdAsync(Guid id, bool isArabic, CancellationToken ct);
        Task<WarehouseDto> CreateAsync(WarehouseCreateDto dto, Guid tenantId, bool isArabic, CancellationToken ct);
        Task<WarehouseDto> UpdateAsync(Guid id, WarehouseUpdateDto dto, bool isArabic, CancellationToken ct);
        Task DeleteAsync(Guid id, CancellationToken ct);

        Task<List<RawMaterialInventoryDto>> GetInventoryAsync(Guid? warehouseId, Guid? rawMaterialId, bool isArabic, CancellationToken ct);
        Task<RawMaterialInventoryDto> UpsertInventoryAsync(RawMaterialInventoryUpsertDto dto, Guid tenantId, bool isArabic, CancellationToken ct);

        Task<InventoryTransferDto> CreateTransferAsync(InventoryTransferCreateDto dto, Guid tenantId, Guid? performedById, bool isArabic, CancellationToken ct);
        Task<List<InventoryTransferDto>> GetTransfersAsync(Guid? warehouseId, Guid? rawMaterialId, DateTime? from, DateTime? to, bool isArabic, CancellationToken ct);

        Task<List<StockByWarehouseRowDto>> GetStockByWarehouseAsync(Guid? warehouseId, bool lowOnly, bool isArabic, CancellationToken ct);

        /// <summary>
        /// Choose the warehouse from which a raw material is consumed. Prefers
        /// the material's <see cref="RawMaterial.DefaultWarehouseId"/>; falls
        /// back to the tenant's Main warehouse. Returns null only when the
        /// tenant has no Main warehouse seeded yet.
        /// </summary>
        Task<Guid?> ResolveSourceWarehouseIdAsync(Guid rawMaterialId, CancellationToken ct);

        /// <summary>
        /// Idempotently decrement the on-hand quantity for a (material, warehouse).
        /// No-op when no inventory row exists — the FIFO costing path continues
        /// to work for legacy materials without warehouse-level rows.
        /// </summary>
        Task DecrementInventoryAsync(Guid rawMaterialId, Guid warehouseId, decimal quantity, CancellationToken ct);
        Task DecrementInventoryAsync(Guid rawMaterialId, Guid warehouseId, decimal quantity, Guid branchId, CancellationToken ct);

        /// <summary>
        /// Idempotently increment the on-hand quantity for a (material, warehouse).
        /// No-op when no inventory row exists.
        /// </summary>
        Task IncrementInventoryAsync(Guid rawMaterialId, Guid warehouseId, decimal quantity, CancellationToken ct);
        Task IncrementInventoryAsync(Guid rawMaterialId, Guid warehouseId, decimal quantity, Guid branchId, CancellationToken ct);
    }
}
