using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.Domain;

namespace RestaurantPos.Api.Modules.Retail.Import
{
    internal sealed record SnapshotSupplier(Guid Id, string Name, string? Country);

    internal sealed record SnapshotCategory(Guid Id, string Name);

    internal sealed record SnapshotProduct(
        Guid ProductId,
        Guid DetailId,
        string Sku,
        string Name,
        Guid? CategoryId,
        decimal? CostPrice,
        decimal BasePrice,
        string? Barcode,
        string? Brand,
        string? SizeLabel,
        string? CountryOfOrigin,
        Guid? SupplierId,
        decimal? TargetMarginPercent,
        decimal ReceivedQuantity,
        decimal SupplierCostTotal,
        decimal ShippingCostPerUnit,
        bool AssignedToRetailBranch,
        bool HasOpeningMovement,
        decimal WorkbookOriginInflow,
        decimal OnHand);

    internal sealed record SnapshotSale(
        Guid OrderId,
        Guid OrderItemId,
        string OrderNumber,
        DateTime OccurredOn,
        string SkuKey,
        int Quantity,
        decimal UnitPrice,
        decimal TotalAmount,
        string? CustomerName,
        string? CustomerPhone,
        OrderStatus Status,
        string? PaymentMethod,
        int PaymentCount);

    internal sealed record SnapshotPayment(
        Guid Id,
        DateTime CreatedAtUtc,
        decimal Amount,
        string? Method,
        Guid? PaymentMethodId);

    /// <summary>A workbook-imported retail order as the payment rules see it. Covers every
    /// order the importer has ever written to this branch, including ones whose workbook row
    /// has since been deleted — those still have to obey the settlement ruling.</summary>
    internal sealed record SnapshotHistoricalOrder(
        Guid OrderId,
        string OrderNumber,
        DateTime CreatedAtUtc,
        OrderStatus Status,
        decimal TotalAmount,
        string? PaymentMethod,
        IReadOnlyList<SnapshotPayment> Payments);

    internal sealed record SnapshotPurchase(
        Guid Id,
        string Number,
        DateTime OrderDateUtc,
        string? InvoiceNumber,
        decimal QuantityPieces,
        decimal SupplierCost,
        decimal ShippingCustomsClearance,
        string? Status,
        Guid? SupplierId,
        string SupplierNameSnapshot,
        int SourceRowNumber);

    /// <summary>
    /// The database as the sync needs to see it, read once and never written through. Holding
    /// it separately is what lets the plan be computed with zero writes: the planner sees only
    /// this and the workbook.
    /// </summary>
    internal sealed class RetailWorkbookSnapshot
    {
        public required Guid TenantId { get; init; }
        public required Guid RetailBranchId { get; init; }
        public required IReadOnlyDictionary<string, SnapshotSupplier> SuppliersByKey { get; init; }
        public required IReadOnlyDictionary<string, SnapshotCategory> CategoriesByKey { get; init; }
        public required IReadOnlyDictionary<string, SnapshotProduct> ProductsBySkuKey { get; init; }
        public required IReadOnlyDictionary<string, List<SnapshotProduct>> ProductsByBarcodeKey { get; init; }
        public required IReadOnlyList<SnapshotSale> Sales { get; init; }
        public required IReadOnlyList<SnapshotHistoricalOrder> HistoricalOrders { get; init; }
        public required IReadOnlyDictionary<string, SnapshotPurchase> PurchasesByKey { get; init; }
        public required IReadOnlySet<string> UsedOrderNumbers { get; init; }
        public required decimal OnHand { get; init; }
        public required int MovementCount { get; init; }

        /// <summary>Cost of goods the ledger has already snapshotted for imported sales. Read
        /// from the movements rather than from today's product cost, which is the whole point
        /// of a snapshot.</summary>
        public required decimal RecordedCogs { get; init; }
        public required decimal RecordedSales { get; init; }
        public required decimal RecordedUnits { get; init; }

        public static async Task<RetailWorkbookSnapshot> LoadAsync(
            PosDbContext context,
            Guid tenantId,
            Guid retailBranchId,
            string orderNumberPrefix,
            CancellationToken ct)
        {
            var suppliers = await context.Suppliers
                .AsNoTracking()
                .Select(s => new SnapshotSupplier(s.Id, s.Name, s.Country))
                .ToListAsync(ct);

            var categories = await context.Categories
                .AsNoTracking()
                .Select(c => new SnapshotCategory(c.Id, c.Name))
                .ToListAsync(ct);

            var details = await context.RetailProductDetails
                .AsNoTracking()
                .Select(d => new
                {
                    d.Id,
                    d.ProductId,
                    d.Sku,
                    d.Barcode,
                    d.Brand,
                    d.SizeLabel,
                    d.CountryOfOrigin,
                    d.SupplierId,
                    d.TargetMarginPercent,
                    d.ReceivedQuantity,
                    d.SupplierCostTotal,
                    d.ShippingCostPerUnit,
                    ProductName = d.Product!.Name,
                    d.Product.CategoryId,
                    d.Product.CostPrice,
                    d.Product.BasePrice,
                    Assigned = context.Set<BranchProduct>().Any(bp => bp.ProductId == d.ProductId && bp.BranchId == retailBranchId),
                    HasOpening = context.RetailStockMovements.Any(m =>
                        m.ProductId == d.ProductId && m.MovementType == RetailStockMovementType.OpeningBalance),
                    WorkbookInflow = context.RetailStockMovements
                        .Where(m => m.ProductId == d.ProductId && m.SourceDocumentType == RetailStockSourceDocument.WorkbookImport)
                        .Sum(m => (decimal?)m.Quantity) ?? 0m,
                    OnHand = context.RetailStockMovements
                        .Where(m => m.ProductId == d.ProductId)
                        .Sum(m => (decimal?)m.Quantity) ?? 0m
                })
                .ToListAsync(ct);

            var products = details
                .Select(d => new SnapshotProduct(
                    d.ProductId, d.Id, d.Sku, d.ProductName, d.CategoryId, d.CostPrice, d.BasePrice,
                    d.Barcode, d.Brand, d.SizeLabel, d.CountryOfOrigin, d.SupplierId, d.TargetMarginPercent,
                    d.ReceivedQuantity, d.SupplierCostTotal, d.ShippingCostPerUnit,
                    d.Assigned, d.HasOpening, d.WorkbookInflow, d.OnHand))
                .ToList();

            var sales = await context.Orders
                .AsNoTracking()
                .Where(o => o.OrderNumber.StartsWith(orderNumberPrefix))
                .SelectMany(o => o.OrderItems, (o, i) => new
                {
                    o.Id,
                    ItemId = i.Id,
                    o.OrderNumber,
                    o.CreatedAt,
                    i.Quantity,
                    i.Price,
                    o.TotalAmount,
                    o.CustomerName,
                    o.CustomerPhone,
                    o.Status,
                    o.PaymentMethod,
                    Sku = context.RetailProductDetails
                        .Where(d => d.ProductId == i.ProductId)
                        .Select(d => d.Sku)
                        .FirstOrDefault(),
                    Payments = o.Payments.Count
                })
                .ToListAsync(ct);

            // Every order the importer has ever written to this branch, not only the ones a
            // workbook row still matches — the payment ruling applies to all of them.
            var historical = await context.Orders
                .AsNoTracking()
                .Where(o => o.OrderNumber.StartsWith(orderNumberPrefix) && o.BranchId == retailBranchId)
                .Select(o => new SnapshotHistoricalOrder(
                    o.Id,
                    o.OrderNumber,
                    o.CreatedAt,
                    o.Status,
                    o.TotalAmount,
                    o.PaymentMethod,
                    o.Payments
                        .Select(p => new SnapshotPayment(p.Id, p.CreatedAt, p.Amount, p.Method, p.PaymentMethodId))
                        .ToList()))
                .ToListAsync(ct);

            var purchases = await context.RetailLegacyPurchases
                .AsNoTracking()
                .Select(p => new SnapshotPurchase(
                    p.Id, p.PurchaseOrderNumber, p.OrderDateUtc, p.InvoiceNumber, p.QuantityPieces,
                    p.SupplierCost, p.ShippingCustomsClearance, p.Status, p.SupplierId, p.SupplierNameSnapshot,
                    p.SourceRowNumber))
                .ToListAsync(ct);

            var movements = await context.RetailStockMovements
                .AsNoTracking()
                .Select(m => new { m.Quantity, m.MovementType, m.TotalCost })
                .ToListAsync(ct);

            return new RetailWorkbookSnapshot
            {
                TenantId = tenantId,
                RetailBranchId = retailBranchId,
                SuppliersByKey = ByKey(suppliers, s => s.Name),
                CategoriesByKey = ByKey(categories, c => c.Name),
                ProductsBySkuKey = ByKey(products, p => p.Sku, RetailNaming.SkuKey),
                ProductsByBarcodeKey = products
                    .Where(p => p.Barcode is not null)
                    .GroupBy(p => RetailNaming.SkuKey(p.Barcode))
                    .ToDictionary(g => g.Key, g => g.ToList()),
                Sales = sales
                    .Where(s => s.Sku is not null)
                    .Select(s => new SnapshotSale(
                        s.Id, s.ItemId, s.OrderNumber, s.CreatedAt.Date, RetailNaming.SkuKey(s.Sku),
                        s.Quantity, s.Price, s.TotalAmount, s.CustomerName, s.CustomerPhone,
                        s.Status, s.PaymentMethod, s.Payments))
                    .OrderBy(s => s.OrderNumber, StringComparer.Ordinal)
                    .ToList(),
                PurchasesByKey = ByKey(purchases, p => p.Number),
                HistoricalOrders = historical,
                UsedOrderNumbers = historical.Select(o => o.OrderNumber).ToHashSet(StringComparer.OrdinalIgnoreCase),
                OnHand = movements.Sum(m => m.Quantity),
                MovementCount = movements.Count,
                RecordedCogs = Math.Abs(movements
                    .Where(m => m.MovementType == RetailStockMovementType.Sale)
                    .Sum(m => m.TotalCost ?? 0m)),
                RecordedSales = sales.Select(s => new { s.OrderNumber, s.TotalAmount }).Distinct().Sum(s => s.TotalAmount),
                RecordedUnits = sales.Sum(s => s.Quantity)
            };
        }

        private static Dictionary<string, T> ByKey<T>(IEnumerable<T> rows, Func<T, string> name, Func<string, string>? keyOf = null)
        {
            var key = keyOf ?? RetailNaming.Key;
            return rows
                .GroupBy(r => key(name(r)))
                .ToDictionary(g => g.Key, g => g.First());
        }
    }
}
