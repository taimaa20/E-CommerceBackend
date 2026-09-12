using Microsoft.EntityFrameworkCore.Storage;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IWasteLogRepository
    {
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
        Task AddAsync(WasteLog wasteLog, CancellationToken ct = default);
        Task AddAuditAsync(WasteLogAudit audit, CancellationToken ct = default);
        Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken ct = default);
        Task<int> SaveChangesAsync(CancellationToken ct = default);
        Task<WasteLog?> GetByIdAsync(Guid id, bool includeAudit, CancellationToken ct = default);
        Task<Product?> GetProductAsync(Guid id, CancellationToken ct = default);
        Task<RawMaterial?> GetMaterialAsync(Guid id, CancellationToken ct = default);
        Task<Order?> GetOrderAsync(Guid id, CancellationToken ct = default);
        Task<WasteEmployeeOptionDto?> GetEmployeeOptionAsync(Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<WasteEmployeeOptionDto>> GetEmployeeOptionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
        Task<IReadOnlyList<WasteEmployeeOptionDto>> SearchEmployeesAsync(string? search, int limit, CancellationToken ct = default);
        Task<WasteLogPageDto> GetPagedAsync(WasteLogQueryDto query, CancellationToken ct = default);
        Task<WasteLogSummaryDto> GetSummaryAsync(WasteLogQueryDto query, CancellationToken ct = default);
        Task<WasteLogAnalyticsDto> GetAnalyticsAsync(WasteLogQueryDto query, CancellationToken ct = default);
        Task<IReadOnlyList<WasteRecipeRequirement>> GetProductRecipeRequirementsAsync(Guid productId, decimal quantity, CancellationToken ct = default);
        Task<decimal> GetAvailableStockAsync(Guid materialId, Guid branchId, bool allowLegacyFallback, CancellationToken ct = default);
    }

    public sealed class WasteRecipeRequirement
    {
        public Guid MaterialId { get; init; }
        public string MaterialName { get; init; } = string.Empty;
        public string? MaterialNameAr { get; init; }
        public decimal Quantity { get; init; }
        public string Unit { get; init; } = string.Empty;
    }
}
