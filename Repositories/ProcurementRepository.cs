using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Repositories;

public sealed class ProcurementRepository : IProcurementRepository
{
    private readonly PosDbContext _context;
    private readonly IBranchContext _branchContext;
    private readonly ITenantResolver _tenantResolver;

    public ProcurementRepository(
        PosDbContext context,
        IBranchContext branchContext,
        ITenantResolver tenantResolver)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
    }

    public bool HasActiveTransaction => _context.Database.CurrentTransaction is not null;

    public async Task<ProcurementScope> GetCurrentScopeAsync(CancellationToken ct)
        => new(
            _tenantResolver.GetTenantId(),
            (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id);

    public Task<bool> SupplierExistsAsync(Guid supplierId, CancellationToken ct)
        => _context.Suppliers.AsNoTracking().AnyAsync(supplier => supplier.Id == supplierId, ct);

    public async Task<IReadOnlyDictionary<Guid, PurchaseMaterialInfo>> GetMaterialsAsync(
        IReadOnlyCollection<Guid> materialIds,
        CancellationToken ct)
        => await _context.RawMaterials
            .AsNoTracking()
            .Where(material => materialIds.Contains(material.Id))
            .Select(material => new PurchaseMaterialInfo(
                material.Id,
                material.Name,
                material.NameAr,
                material.Unit))
            .ToDictionaryAsync(material => material.Id, ct);

    public Task AddOrderAsync(PurchaseOrder order, CancellationToken ct)
        => _context.PurchaseOrders.AddAsync(order, ct).AsTask();

    public void RemoveOrder(PurchaseOrder order)
        => _context.PurchaseOrders.Remove(order);

    public async Task<PurchaseOrder?> GetOrderAsync(Guid id, CancellationToken ct)
    {
        var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
        return await _context.PurchaseOrders
            .Include(order => order.Supplier)
            .Include(order => order.Items)
                .ThenInclude(item => item.RawMaterial)
            .FirstOrDefaultAsync(order => order.Id == id && order.BranchId == branchId, ct);
    }

    public async Task<List<StockBatch>> GetUnapprovedBatchesAsync(
        Guid purchaseOrderId,
        CancellationToken ct)
    {
        var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
        return await _context.StockBatches
            .Include(batch => batch.RawMaterial)
            .Where(batch =>
                batch.PurchaseOrderId == purchaseOrderId
                && batch.BranchId == branchId
                && !batch.IsApproved)
            .ToListAsync(ct);
    }

    public void RemoveBatches(IEnumerable<StockBatch> batches)
        => _context.StockBatches.RemoveRange(batches);

    public Task<int> SaveChangesAsync(CancellationToken ct)
        => _context.SaveChangesAsync(ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct)
        => _context.Database.BeginTransactionAsync(ct);

    public async Task<SimplePurchaseDetailsDto?> GetSimplePurchaseDetailsAsync(
        Guid purchaseOrderId,
        bool isArabic,
        CancellationToken ct)
    {
        var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
        return await _context.PurchaseOrders
            .AsNoTracking()
            .Where(order => order.Id == purchaseOrderId && order.BranchId == branchId)
            .Select(order => new SimplePurchaseDetailsDto
            {
                Id = order.Id,
                PurchaseNumber = order.OrderNumber,
                SupplierId = order.SupplierId,
                SupplierName = isArabic
                    && order.Supplier.NameAr != null
                    && order.Supplier.NameAr != string.Empty
                        ? order.Supplier.NameAr
                        : order.Supplier.Name,
                BranchId = order.BranchId,
                BranchName = isArabic
                    && order.Branch != null
                    && order.Branch.NameAr != null
                    && order.Branch.NameAr != string.Empty
                        ? order.Branch.NameAr
                        : order.Branch != null ? order.Branch.Name : string.Empty,
                InvoiceId = order.SupplierInvoice != null ? order.SupplierInvoice.Id : null,
                InvoiceNumber = order.SupplierInvoice != null
                    ? order.SupplierInvoice.InvoiceNumber
                    : order.InvoiceNumber ?? string.Empty,
                InvoiceDate = order.SupplierInvoice != null
                    ? order.SupplierInvoice.InvoiceDate
                    : order.ExpectedDate,
                Subtotal = order.SupplierInvoice != null
                    ? order.SupplierInvoice.Subtotal
                    : decimal.Round(order.TotalAmount, 2),
                TaxAmount = order.SupplierInvoice != null ? order.SupplierInvoice.TaxAmount : 0,
                DiscountAmount = order.SupplierInvoice != null ? order.SupplierInvoice.DiscountAmount : 0,
                TotalAmount = order.SupplierInvoice != null
                    ? order.SupplierInvoice.TotalAmount
                    : decimal.Round(order.TotalAmount, 2),
                PaidAmount = order.SupplierInvoice != null ? order.SupplierInvoice.PaidAmount : 0,
                Balance = order.SupplierInvoice != null
                    ? order.SupplierInvoice.TotalAmount - order.SupplierInvoice.PaidAmount
                    : decimal.Round(order.TotalAmount, 2),
                PaymentStatus = order.SupplierInvoice != null
                    ? order.SupplierInvoice.Status.ToString()
                    : "Unpaid",
                CurrencyCode = order.SupplierInvoice != null
                    ? order.SupplierInvoice.CurrencyCode
                    : string.Empty,
                ReceivingStatus = order.Status == PurchaseOrderStatus.Approved
                    ? "Approved"
                    : order.Status == PurchaseOrderStatus.Received
                        ? "PendingApproval"
                        : order.Status == PurchaseOrderStatus.Cancelled
                            ? "Cancelled"
                            : "NotReceived",
                ReceivedDate = order.ReceivedDate,
                Items = order.Items
                    .OrderBy(item => item.CreatedAt)
                    .Select(item => new SimplePurchaseItemDto
                    {
                        RawMaterialId = item.RawMaterialId,
                        ItemName = item.RawMaterial != null
                            ? isArabic
                                && item.RawMaterial.NameAr != null
                                && item.RawMaterial.NameAr != string.Empty
                                    ? item.RawMaterial.NameAr
                                    : item.RawMaterial.Name
                            : item.RawMaterialName ?? string.Empty,
                        Unit = item.RawMaterial != null ? item.RawMaterial.Unit.ToString() : string.Empty,
                        Quantity = item.Quantity,
                        UnitCost = item.UnitPrice,
                        LineTotal = item.Quantity * item.UnitPrice
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
    }
}
