using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.DTOs;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    public sealed class RetailReportingService : IRetailReportingService
    {
        /// <summary>Order states whose goods have left the shelf. Cancelled and
        /// payment-cancelled orders never count as sales.</summary>
        private static readonly OrderStatus[] SoldStatuses =
        {
            OrderStatus.New, OrderStatus.Preparing, OrderStatus.Ready,
            OrderStatus.Served, OrderStatus.Paid, OrderStatus.Completed
        };

        /// <summary>Order-number prefix stamped by the workbook importer. Used only to label
        /// a row as imported history in the UI — never to change how it is counted.</summary>
        private const string ImportedOrderPrefix = "DL-";

        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;

        public RetailReportingService(PosDbContext context, ITenantResolver tenantResolver)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        public async Task<RetailPerformanceReportDto> GetPerformanceAsync(
            DateTime? fromUtc,
            DateTime? toExclusiveUtc,
            CancellationToken ct = default)
        {
            var tenantId = _tenantResolver.GetTenantId();

            var lines = _context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.TenantId == tenantId
                    && SoldStatuses.Contains(oi.Order.Status)
                    && _context.RetailProductDetails.Any(d => d.ProductId == oi.ProductId));

            if (fromUtc.HasValue)
                lines = lines.Where(oi => oi.Order.CreatedAt >= fromUtc.Value);
            if (toExclusiveUtc.HasValue)
                lines = lines.Where(oi => oi.Order.CreatedAt < toExclusiveUtc.Value);

            var grouped = await lines
                .GroupBy(oi => oi.ProductId)
                .Select(g => new SoldTotals(
                    g.Key,
                    // Corrected definitions: units are summed quantities, revenue is
                    // Σ(unit price × quantity) — not the workbook's row count / Σ unit price.
                    g.Sum(oi => (decimal)oi.Quantity),
                    g.Sum(oi => oi.Price * oi.Quantity),
                    // COGS is the cost captured when the goods left the shelf, so re-pricing a
                    // product tomorrow cannot move the profit reported for last month.
                    g.Sum(oi => oi.StockDeductedCost),
                    g.Count(oi => oi.StockDeductedAt == null),
                    g.Where(oi => oi.StockDeductedAt == null).Sum(oi => (decimal)oi.Quantity)))
                .ToListAsync(ct);

            var orderCount = await lines.Select(oi => oi.OrderId).Distinct().CountAsync(ct);
            var products = await LoadProductFactsAsync(tenantId, ct);
            var rows = BuildRows(grouped.ToDictionary(g => g.ProductId, g => g), products);
            var outstanding = await GetOutstandingAsync(tenantId, fromUtc, toExclusiveUtc, ct);

            return BuildReport(rows, orderCount, outstanding);
        }

        public async Task<RetailSalesPageDto> GetSalesAsync(RetailSalesQuery query, CancellationToken ct = default)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var lines = BuildSalesQuery(tenantId, query);

            var totalCount = await lines.CountAsync(ct);
            var totals = await lines
                .Select(x => new { x.Item.Quantity, Amount = x.Item.Price * x.Item.Quantity })
                .ToListAsync(ct);

            var pageNumber = Math.Max(query.PageNumber, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 500);

            var rows = await lines
                .OrderByDescending(x => x.Item.Order.CreatedAt)
                .ThenBy(x => x.Item.Order.OrderNumber)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Item.Id,
                    x.Item.OrderId,
                    x.Item.Order.OrderNumber,
                    x.Item.Order.CreatedAt,
                    x.Item.Order.CustomerName,
                    x.Item.Order.CustomerPhone,
                    x.Item.Order.Status,
                    x.Item.Order.PaymentMethod,
                    PaidAmount = x.Item.Order.Payments.Sum(p => (decimal?)p.Amount) ?? 0m,
                    x.Item.Order.TotalAmount,
                    x.Item.ProductId,
                    x.Item.ProductName,
                    x.Item.Quantity,
                    x.Item.Price,
                    x.Item.UnitPriceSnapshot,
                    x.Item.StockDeductedCost,
                    x.Item.StockDeductedAt,
                    x.Detail.Sku,
                    x.Detail.Barcode,
                    x.Detail.Brand,
                    SupplierName = x.Detail.Supplier != null ? x.Detail.Supplier.Name : null
                })
                .ToListAsync(ct);

            return new RetailSalesPageDto
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalAmount = decimal.Round(totals.Sum(t => t.Amount), 2, MidpointRounding.AwayFromZero),
                TotalUnits = totals.Sum(t => (decimal)t.Quantity),
                Items = rows.Select(r =>
                {
                    var lineTotal = decimal.Round(r.Price * r.Quantity, 2, MidpointRounding.AwayFromZero);
                    var lineCost = decimal.Round(r.StockDeductedCost, 2, MidpointRounding.AwayFromZero);
                    return new RetailSaleLineDto
                    {
                        OrderId = r.OrderId,
                        OrderItemId = r.Id,
                        SoldAtUtc = r.CreatedAt,
                        OrderNumber = r.OrderNumber,
                        CustomerName = r.CustomerName,
                        CustomerPhone = r.CustomerPhone,
                        ProductId = r.ProductId,
                        Sku = r.Sku,
                        Barcode = r.Barcode,
                        ProductName = r.ProductName,
                        Brand = r.Brand,
                        SupplierName = r.SupplierName,
                        Quantity = r.Quantity,
                        ApprovedUnitPrice = r.UnitPriceSnapshot,
                        SoldUnitPrice = r.Price,
                        LineTotal = lineTotal,
                        LineCost = lineCost,
                        UnitCost = r.Quantity <= 0
                            ? null
                            : decimal.Round(lineCost / r.Quantity, 4, MidpointRounding.AwayFromZero),
                        LineProfit = decimal.Round(lineTotal - lineCost, 2, MidpointRounding.AwayFromZero),
                        HasCostSnapshot = r.StockDeductedAt.HasValue,
                        OrderStatus = r.Status.ToString(),
                        PaymentMethod = r.PaymentMethod,
                        IsPaid = r.PaidAmount >= r.TotalAmount && r.TotalAmount > 0m,
                        IsGift = lineTotal <= 0m,
                        IsPriceOverride = r.UnitPriceSnapshot.HasValue && r.UnitPriceSnapshot.Value != r.Price,
                        IsImportedHistory = r.OrderNumber.StartsWith(ImportedOrderPrefix)
                    };
                }).ToList()
            };
        }

        private IQueryable<SaleLineRow> BuildSalesQuery(Guid tenantId, RetailSalesQuery query)
        {
            var lines = from item in _context.OrderItems.AsNoTracking()
                        join detail in _context.RetailProductDetails.AsNoTracking()
                            on item.ProductId equals detail.ProductId
                        where item.TenantId == tenantId && SoldStatuses.Contains(item.Order.Status)
                        select new SaleLineRow { Item = item, Detail = detail };

            if (query.FromUtc.HasValue)
                lines = lines.Where(x => x.Item.Order.CreatedAt >= query.FromUtc.Value);
            if (query.ToExclusiveUtc.HasValue)
                lines = lines.Where(x => x.Item.Order.CreatedAt < query.ToExclusiveUtc.Value);
            if (query.OrderId.HasValue)
                lines = lines.Where(x => x.Item.OrderId == query.OrderId.Value);
            if (query.ProductId.HasValue)
                lines = lines.Where(x => x.Item.ProductId == query.ProductId.Value);
            if (!string.IsNullOrWhiteSpace(query.Sku))
                lines = lines.Where(x => x.Detail.Sku == query.Sku);
            if (!string.IsNullOrWhiteSpace(query.Brand))
                lines = lines.Where(x => x.Detail.Brand == query.Brand);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = $"%{query.Search.Trim()}%";
                lines = lines.Where(x =>
                    EF.Functions.ILike(x.Detail.Sku, pattern)
                    || EF.Functions.ILike(x.Item.ProductName, pattern)
                    || EF.Functions.ILike(x.Item.Order.OrderNumber, pattern)
                    || (x.Detail.Brand != null && EF.Functions.ILike(x.Detail.Brand, pattern))
                    || (x.Item.Order.CustomerName != null && EF.Functions.ILike(x.Item.Order.CustomerName, pattern))
                    || (x.Item.Order.CustomerPhone != null && EF.Functions.ILike(x.Item.Order.CustomerPhone, pattern)));
            }

            return ApplyPaymentStatusFilter(lines, query.PaymentStatus);
        }

        private static IQueryable<SaleLineRow> ApplyPaymentStatusFilter(
            IQueryable<SaleLineRow> lines,
            string? paymentStatus)
            => paymentStatus?.Trim().ToLowerInvariant() switch
            {
                "paid" => lines.Where(x => x.Item.Order.Payments.Any()),
                "unpaid" => lines.Where(x => !x.Item.Order.Payments.Any() && x.Item.Order.TotalAmount > 0m),
                "gift" => lines.Where(x => x.Item.Price <= 0m),
                _ => lines
            };

        private sealed class SaleLineRow
        {
            public OrderItem Item { get; init; } = null!;
            public Domain.RetailProductDetail Detail { get; init; } = null!;
        }

        public async Task<IReadOnlyList<RetailLegacyPurchaseDto>> GetLegacyPurchasesAsync(CancellationToken ct = default)
            => await _context.RetailLegacyPurchases
                .AsNoTracking()
                .OrderByDescending(p => p.OrderDateUtc)
                .ThenBy(p => p.PurchaseOrderNumber)
                .Select(p => new RetailLegacyPurchaseDto
                {
                    Id = p.Id,
                    OrderDateUtc = p.OrderDateUtc,
                    PurchaseOrderNumber = p.PurchaseOrderNumber,
                    InvoiceNumber = p.InvoiceNumber,
                    SupplierId = p.SupplierId,
                    SupplierName = p.Supplier != null ? p.Supplier.Name : p.SupplierNameSnapshot,
                    QuantityPieces = p.QuantityPieces,
                    SupplierCost = p.SupplierCost,
                    ShippingCustomsClearance = p.ShippingCustomsClearance,
                    TotalCost = p.TotalCost,
                    Status = p.Status
                })
                .ToListAsync(ct);

        public async Task<IReadOnlyList<RetailSupplierDto>> GetSuppliersAsync(
            bool retailOnly,
            CancellationToken ct = default)
        {
            var query = _context.Suppliers.AsNoTracking();

            if (retailOnly)
            {
                query = query.Where(s =>
                    _context.RetailProductDetails.Any(d => d.SupplierId == s.Id)
                    || _context.RetailLegacyPurchases.Any(p => p.SupplierId == s.Id)
                    || s.Country != null
                    || s.Brands != null);
            }

            return await query
                .OrderBy(s => s.Name)
                .Select(s => new RetailSupplierDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    NameAr = s.NameAr,
                    Brands = s.Brands,
                    BestSellers = s.BestSellers,
                    Country = s.Country,
                    Website = s.Website,
                    Email = s.Email,
                    MobileNumber = s.MobileNumber,
                    RetailProductCount = _context.RetailProductDetails.Count(d => d.SupplierId == s.Id),
                    PurchaseCount = _context.RetailLegacyPurchases.Count(p => p.SupplierId == s.Id),
                    PurchaseValue = _context.RetailLegacyPurchases
                        .Where(p => p.SupplierId == s.Id)
                        .Sum(p => (decimal?)p.TotalCost) ?? 0m
                })
                .ToListAsync(ct);
        }

        private sealed record ProductFacts(
            Guid ProductId, string Sku, string Name, string? Brand,
            string? CategoryName, string? SupplierName, decimal? CostPrice);

        private async Task<List<ProductFacts>> LoadProductFactsAsync(Guid tenantId, CancellationToken ct)
            => await _context.RetailProductDetails
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId)
                .Select(d => new ProductFacts(
                    d.ProductId,
                    d.Sku,
                    d.Product!.Name,
                    d.Brand,
                    d.Product.Category != null ? d.Product.Category.Name : null,
                    d.Supplier != null ? d.Supplier.Name : null,
                    d.Product.CostPrice))
                .ToListAsync(ct);

        private async Task<decimal> GetOutstandingAsync(
            Guid tenantId,
            DateTime? fromUtc,
            DateTime? toExclusiveUtc,
            CancellationToken ct)
        {
            var orders = _context.Orders
                .AsNoTracking()
                .Where(o => o.TenantId == tenantId
                    && SoldStatuses.Contains(o.Status)
                    && o.OrderItems.Any(oi => _context.RetailProductDetails.Any(d => d.ProductId == oi.ProductId)));

            if (fromUtc.HasValue)
                orders = orders.Where(o => o.CreatedAt >= fromUtc.Value);
            if (toExclusiveUtc.HasValue)
                orders = orders.Where(o => o.CreatedAt < toExclusiveUtc.Value);

            var totals = await orders
                .Select(o => new
                {
                    o.TotalAmount,
                    Paid = o.Payments.Sum(p => (decimal?)p.Amount) ?? 0m
                })
                .ToListAsync(ct);

            return decimal.Round(totals.Sum(t => Math.Max(0m, t.TotalAmount - t.Paid)), 2, MidpointRounding.AwayFromZero);
        }

        private static List<RetailProductPerformanceDto> BuildRows(
            IReadOnlyDictionary<Guid, SoldTotals> sold,
            IReadOnlyList<ProductFacts> products)
        {
            var rows = new List<RetailProductPerformanceDto>(products.Count);

            foreach (var product in products)
            {
                var totals = sold.GetValueOrDefault(product.ProductId)
                    ?? new SoldTotals(product.ProductId, 0m, 0m, 0m, 0, 0m);

                // Lines relieved before the ledger existed have no snapshot; their cost falls
                // back to today's, and the count says how much of the figure that is.
                var fallbackCost = (product.CostPrice ?? 0m) * totals.QuantityWithoutSnapshot;
                var cost = decimal.Round(totals.SnapshotCost + fallbackCost, 2, MidpointRounding.AwayFromZero);
                var sales = decimal.Round(totals.Sales, 2, MidpointRounding.AwayFromZero);
                var profit = sales - cost;

                rows.Add(new RetailProductPerformanceDto
                {
                    ProductId = product.ProductId,
                    Sku = product.Sku,
                    ProductName = product.Name,
                    Brand = product.Brand,
                    CategoryName = product.CategoryName,
                    SupplierName = product.SupplierName,
                    QuantitySold = totals.Quantity,
                    TotalSales = sales,
                    CostPerItem = product.CostPrice,
                    TotalCost = cost,
                    LinesWithoutCostSnapshot = totals.LinesWithoutSnapshot,
                    GrossProfit = profit,
                    ProfitPercent = sales <= 0m
                        ? 0m
                        : decimal.Round(profit / sales * 100m, 2, MidpointRounding.AwayFromZero)
                });
            }

            return rows.OrderByDescending(r => r.TotalSales).ThenBy(r => r.ProductName).ToList();
        }

        /// <summary>Per-product sold totals, including how much of the cost is a snapshot and
        /// how much had to fall back to the current product cost.</summary>
        private sealed record SoldTotals(
            Guid ProductId,
            decimal Quantity,
            decimal Sales,
            decimal SnapshotCost,
            int LinesWithoutSnapshot,
            decimal QuantityWithoutSnapshot);

        private static RetailPerformanceReportDto BuildReport(
            List<RetailProductPerformanceDto> rows,
            int orderCount,
            decimal outstanding)
        {
            var totalSales = rows.Sum(r => r.TotalSales);
            var totalCost = rows.Sum(r => r.TotalCost);
            var profit = totalSales - totalCost;

            return new RetailPerformanceReportDto
            {
                TotalSales = totalSales,
                TotalUnitsSold = rows.Sum(r => r.QuantitySold),
                TotalCost = totalCost,
                GrossProfit = profit,
                GrossMarginPercent = totalSales <= 0m
                    ? 0m
                    : decimal.Round(profit / totalSales * 100m, 2, MidpointRounding.AwayFromZero),
                ProductCount = rows.Count(r => r.QuantitySold > 0m),
                OrderCount = orderCount,
                OutstandingUnpaid = outstanding,
                LinesWithoutCostSnapshot = rows.Sum(r => r.LinesWithoutCostSnapshot),
                Products = rows
            };
        }
    }
}
