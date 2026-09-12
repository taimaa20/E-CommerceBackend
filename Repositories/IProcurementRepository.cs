using Microsoft.EntityFrameworkCore.Storage;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

public interface IProcurementRepository
{
    bool HasActiveTransaction { get; }
    Task<ProcurementScope> GetCurrentScopeAsync(CancellationToken ct);
    Task<bool> SupplierExistsAsync(Guid supplierId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, PurchaseMaterialInfo>> GetMaterialsAsync(
        IReadOnlyCollection<Guid> materialIds,
        CancellationToken ct);
    Task AddOrderAsync(PurchaseOrder order, CancellationToken ct);
    void RemoveOrder(PurchaseOrder order);
    Task<PurchaseOrder?> GetOrderAsync(Guid id, CancellationToken ct);
    Task<List<StockBatch>> GetUnapprovedBatchesAsync(Guid purchaseOrderId, CancellationToken ct);
    void RemoveBatches(IEnumerable<StockBatch> batches);
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
    Task<SimplePurchaseDetailsDto?> GetSimplePurchaseDetailsAsync(
        Guid purchaseOrderId,
        bool isArabic,
        CancellationToken ct);
}

public sealed record PurchaseMaterialInfo(
    Guid Id,
    string Name,
    string? NameAr,
    Unit Unit);

public sealed record ProcurementScope(Guid TenantId, Guid BranchId);
