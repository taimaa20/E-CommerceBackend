using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed class RawMaterialConsumptionRepository : IRawMaterialConsumptionRepository
    {
        private const string Uncategorized = "Uncategorized";
        private const string UncategorizedAr = "غير مصنف";
        private readonly PosDbContext _context;

        public RawMaterialConsumptionRepository(PosDbContext context)
            => _context = context ?? throw new ArgumentNullException(nameof(context));

        public async Task<RawMaterialConsumptionReportDto?> GetAsync(RawMaterialConsumptionQueryDto query, CancellationToken ct)
        {
            var material = await _context.RawMaterials.AsNoTracking()
                .Where(m => m.Id == query.RawMaterialId)
                .Select(m => new { m.Id, m.Name, m.NameAr, m.Unit }).FirstOrDefaultAsync(ct);
            if (material is null) return null;

            var snapshots = BuildSnapshots(query);
            var itemRows = BuildItemRows(snapshots);
            var total = await snapshots.SumAsync(s => (decimal?)s.Quantity, ct) ?? 0m;
            var productCount = await itemRows.Select(r => r.ProductId).Distinct().CountAsync(ct);
            var orderCount = await itemRows.Select(r => r.OrderId).Distinct().CountAsync(ct);
            var categories = await BuildCategoriesAsync(itemRows, total, ct);
            var products = await BuildProductsAsync(itemRows, total, query, ct);
            var details = await BuildDetailsAsync(snapshots, query, ct);
            var detailCount = await snapshots.CountAsync(ct);

            return new RawMaterialConsumptionReportDto
            {
                RawMaterialId = material.Id, RawMaterialName = material.Name, RawMaterialNameAr = material.NameAr,
                Unit = material.Unit.ToString(), FromUtc = query.FromUtc, ToUtc = query.ToUtc,
                TotalConsumed = total, ProductCount = productCount, CategoryCount = categories.Count,
                OrderCount = orderCount, TotalSalesQuantity = await itemRows.SumAsync(r => (int?)r.QuantitySold, ct) ?? 0,
                AverageDailyConsumption = Average(total, Math.Max(1m, (decimal)(query.ToUtc - query.FromUtc).TotalDays)),
                AverageConsumptionPerOrder = Average(total, orderCount),
                HighestConsumingProduct = await BuildHighestProductAsync(itemRows, ct),
                HighestConsumingCategory = categories.FirstOrDefault() is { } category ? ToLeader(category) : null,
                Categories = categories, Products = products, Details = details,
                Timeline = await BuildTimelineAsync(snapshots, query.TimelineGrouping, ct),
                Page = query.Page, PageSize = query.PageSize,
                TotalPages = Pages(productCount, query.PageSize), DetailPage = query.DetailPage,
                DetailPageSize = query.DetailPageSize, DetailTotalCount = detailCount,
                DetailTotalPages = Pages(detailCount, query.DetailPageSize)
            };
        }

        public async Task<IReadOnlyList<RawMaterialConsumptionDetailDto>> GetAllDetailsAsync(
            RawMaterialConsumptionQueryDto query, CancellationToken ct)
            => await ProjectDetails(BuildSnapshots(query))
                .OrderByDescending(r => r.DeductedAtUtc).ToListAsync(ct);

        public async Task<(string CompanyName, string? LogoUrl)> GetBrandingAsync(Guid tenantId, CancellationToken ct)
        {
            var tenant = await _context.Tenants.AsNoTracking().Where(t => t.Id == tenantId)
                .Select(t => new { t.Name, t.BusinessName, t.LogoUrl }).FirstOrDefaultAsync(ct);
            return (tenant?.BusinessName ?? tenant?.Name ?? "Restaurant POS", tenant?.LogoUrl);
        }

        private IQueryable<OrderItemRecipeSnapshot> BuildSnapshots(RawMaterialConsumptionQueryDto query)
        {
            var rows = _context.OrderItemRecipeSnapshots.AsNoTracking().Where(s =>
                s.RawMaterialId == query.RawMaterialId && s.OrderItem.StockDeductedAt >= query.FromUtc
                && s.OrderItem.StockDeductedAt < query.ToUtc);
            if (query.BranchId.HasValue) rows = rows.Where(s => s.OrderItem.BranchId == query.BranchId);
            if (query.CategoryId.HasValue) rows = rows.Where(s => s.OrderItem.Product.CategoryId == query.CategoryId);
            if (query.ProductId.HasValue) rows = rows.Where(s => s.OrderItem.ProductId == query.ProductId);
            if (Enum.TryParse<OrderStatus>(query.OrderStatus, true, out var status)) rows = rows.Where(s => s.OrderItem.Order.Status == status);
            rows = ApplyOrderChannel(rows, query.OrderChannel);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim().ToLower();
                rows = rows.Where(s => s.OrderItem.Order.OrderNumber.ToLower().Contains(term)
                    || (s.OrderItem.Order.DisplayOrderNumber != null && s.OrderItem.Order.DisplayOrderNumber.ToLower().Contains(term))
                    || s.OrderItem.ProductName.ToLower().Contains(term)
                    || (s.OrderItem.Product.NameAr != null && s.OrderItem.Product.NameAr.ToLower().Contains(term))
                    || (s.OrderItem.Product.Category != null && (s.OrderItem.Product.Category.Name.ToLower().Contains(term)
                        || (s.OrderItem.Product.Category.NameAr != null && s.OrderItem.Product.Category.NameAr.ToLower().Contains(term))))
                    || s.RawMaterialName.ToLower().Contains(term)
                    || (s.RawMaterialNameAr != null && s.RawMaterialNameAr.ToLower().Contains(term)));
            }
            return rows;
        }

        private static IQueryable<OrderItemRecipeSnapshot> ApplyOrderChannel(IQueryable<OrderItemRecipeSnapshot> rows, string? channel)
            => channel?.ToLowerInvariant() switch
            {
                "dinein" => rows.Where(s => s.OrderItem.Order.OrderType == OrderType.DineIn),
                "takeaway" => rows.Where(s => s.OrderItem.Order.OrderType == OrderType.Takeaway),
                "delivery" => rows.Where(s => s.OrderItem.Order.OrderType == OrderType.Delivery && s.OrderItem.Order.OrderSource != OrderSource.DeliveryPartner),
                "deliverypartner" => rows.Where(s => s.OrderItem.Order.OrderSource == OrderSource.DeliveryPartner),
                _ => rows
            };

        private static IQueryable<ItemRow> BuildItemRows(IQueryable<OrderItemRecipeSnapshot> snapshots)
            => snapshots.GroupBy(s => new { s.OrderItemId, s.OrderItem.OrderId, s.OrderItem.ProductId,
                    s.OrderItem.ProductName, ProductNameAr = s.OrderItem.Product.NameAr,
                    s.OrderItem.Product.CategoryId,
                    CategoryName = s.OrderItem.Product.Category != null ? s.OrderItem.Product.Category.Name : Uncategorized,
                    CategoryNameAr = s.OrderItem.Product.Category != null ? s.OrderItem.Product.Category.NameAr : UncategorizedAr,
                    s.OrderItem.Quantity })
                .Select(g => new ItemRow
                {
                    OrderId = g.Key.OrderId,
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    ProductNameAr = g.Key.ProductNameAr,
                    CategoryId = g.Key.CategoryId,
                    CategoryName = g.Key.CategoryName,
                    CategoryNameAr = g.Key.CategoryNameAr,
                    QuantitySold = g.Key.Quantity,
                    Consumed = g.Sum(s => s.Quantity)
                });

        private static async Task<List<RawMaterialCategoryConsumptionDto>> BuildCategoriesAsync(IQueryable<ItemRow> rows, decimal total, CancellationToken ct)
        {
            var result = await rows.GroupBy(r => new { r.CategoryId, r.CategoryName, r.CategoryNameAr })
                .Select(g => new RawMaterialCategoryConsumptionDto { CategoryId = g.Key.CategoryId,
                    CategoryName = g.Key.CategoryName, CategoryNameAr = g.Key.CategoryNameAr,
                    TotalConsumed = g.Sum(r => r.Consumed), OrderCount = g.Select(r => r.OrderId).Distinct().Count() })
                .OrderByDescending(r => r.TotalConsumed).ToListAsync(ct);
            result.ForEach(row => row.Percentage = Percentage(row.TotalConsumed, total));
            return result;
        }

        private static async Task<List<RawMaterialProductConsumptionDto>> BuildProductsAsync(IQueryable<ItemRow> rows, decimal total, RawMaterialConsumptionQueryDto query, CancellationToken ct)
        {
            var grouped = ProductGroups(rows).OrderByDescending(r => r.TotalConsumed);
            var result = query.ExportAll
                ? await grouped.ToListAsync(ct)
                : await grouped.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
            result.ForEach(row => CompleteProduct(row, total));
            return result;
        }

        private static IQueryable<RawMaterialProductConsumptionDto> ProductGroups(IQueryable<ItemRow> rows)
            => rows.GroupBy(r => new { r.ProductId, r.ProductName, r.ProductNameAr, r.CategoryId, r.CategoryName, r.CategoryNameAr })
                .Select(g => new RawMaterialProductConsumptionDto { ProductId = g.Key.ProductId, ProductName = g.Key.ProductName,
                    ProductNameAr = g.Key.ProductNameAr, CategoryId = g.Key.CategoryId, CategoryName = g.Key.CategoryName,
                    CategoryNameAr = g.Key.CategoryNameAr, QuantitySold = g.Sum(r => r.QuantitySold),
                    TotalConsumed = g.Sum(r => r.Consumed), OrderCount = g.Select(r => r.OrderId).Distinct().Count() });

        private static async Task<RawMaterialConsumptionLeaderDto?> BuildHighestProductAsync(IQueryable<ItemRow> rows, CancellationToken ct)
        {
            var row = await ProductGroups(rows).OrderByDescending(r => r.TotalConsumed).FirstOrDefaultAsync(ct);
            return row is null ? null : new() { Id = row.ProductId, Name = row.ProductName, NameAr = row.ProductNameAr, Consumed = row.TotalConsumed };
        }

        private static async Task<List<RawMaterialConsumptionDetailDto>> BuildDetailsAsync(IQueryable<OrderItemRecipeSnapshot> rows, RawMaterialConsumptionQueryDto query, CancellationToken ct)
        {
            var projected = ProjectDetails(rows);
            projected = ApplySorting(projected, query.SortBy, query.SortDirection);
            return await projected.Skip((query.DetailPage - 1) * query.DetailPageSize).Take(query.DetailPageSize).ToListAsync(ct);
        }

        private static IQueryable<RawMaterialConsumptionDetailDto> ProjectDetails(IQueryable<OrderItemRecipeSnapshot> rows)
            => rows.Select(s => new RawMaterialConsumptionDetailDto { SnapshotId = s.Id,
                DeductedAtUtc = s.OrderItem.StockDeductedAt!.Value, OrderId = s.OrderItem.OrderId,
                OrderNumber = s.OrderItem.Order.DisplayOrderNumber ?? s.OrderItem.Order.OrderNumber,
                ProductId = s.OrderItem.ProductId, ProductName = s.OrderItem.ProductName, ProductNameAr = s.OrderItem.Product.NameAr,
                CategoryName = s.OrderItem.Product.Category != null ? s.OrderItem.Product.Category.Name : Uncategorized,
                CategoryNameAr = s.OrderItem.Product.Category != null ? s.OrderItem.Product.Category.NameAr : UncategorizedAr,
                RawMaterialName = s.RawMaterialName, RawMaterialNameAr = s.RawMaterialNameAr,
                RecipeQuantity = s.OrderItem.Quantity == 0 ? 0m : s.Quantity / s.OrderItem.Quantity,
                SoldQuantity = s.OrderItem.Quantity, TotalConsumed = s.Quantity,
                BranchName = s.OrderItem.Branch.Name, BranchNameAr = s.OrderItem.Branch.NameAr,
                OrderType = s.OrderItem.Order.OrderSource == OrderSource.DeliveryPartner ? "DeliveryPartner" : s.OrderItem.Order.OrderType.ToString(),
                OrderStatus = s.OrderItem.Order.Status.ToString() });

        private static IQueryable<RawMaterialConsumptionDetailDto> ApplySorting(IQueryable<RawMaterialConsumptionDetailDto> rows, string? sortBy, string? direction)
        {
            var asc = string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
            return sortBy?.ToLowerInvariant() switch
            {
                "order" => asc ? rows.OrderBy(r => r.OrderNumber) : rows.OrderByDescending(r => r.OrderNumber),
                "product" => asc ? rows.OrderBy(r => r.ProductName) : rows.OrderByDescending(r => r.ProductName),
                "category" => asc ? rows.OrderBy(r => r.CategoryName) : rows.OrderByDescending(r => r.CategoryName),
                "consumed" => asc ? rows.OrderBy(r => r.TotalConsumed) : rows.OrderByDescending(r => r.TotalConsumed),
                _ => asc ? rows.OrderBy(r => r.DeductedAtUtc) : rows.OrderByDescending(r => r.DeductedAtUtc)
            };
        }

        private static async Task<List<RawMaterialConsumptionTimelineDto>> BuildTimelineAsync(IQueryable<OrderItemRecipeSnapshot> rows, string grouping, CancellationToken ct)
        {
            var hourly = string.Equals(grouping, "hour", StringComparison.OrdinalIgnoreCase);
            var monthly = string.Equals(grouping, "month", StringComparison.OrdinalIgnoreCase);
            var buckets = await rows.GroupBy(s => new { Year = s.OrderItem.StockDeductedAt!.Value.Year,
                    Month = s.OrderItem.StockDeductedAt.Value.Month, Day = monthly ? 1 : s.OrderItem.StockDeductedAt.Value.Day,
                    Hour = hourly ? s.OrderItem.StockDeductedAt.Value.Hour : 0 })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour,
                    TotalConsumed = g.Sum(s => s.Quantity), OrderCount = g.Select(s => s.OrderItem.OrderId).Distinct().Count() })
                .OrderBy(r => r.Year).ThenBy(r => r.Month).ThenBy(r => r.Day).ThenBy(r => r.Hour).ToListAsync(ct);
            var daily = buckets.Select(r => new RawMaterialConsumptionTimelineDto
                { BucketStartUtc = new DateTime(r.Year, r.Month, r.Day, r.Hour, 0, 0, DateTimeKind.Utc),
                    TotalConsumed = r.TotalConsumed, OrderCount = r.OrderCount }).ToList();
            if (!string.Equals(grouping, "week", StringComparison.OrdinalIgnoreCase)) return daily;
            return daily.GroupBy(r => StartOfWeek(r.BucketStartUtc)).Select(g => new RawMaterialConsumptionTimelineDto
                { BucketStartUtc = g.Key, TotalConsumed = g.Sum(r => r.TotalConsumed), OrderCount = g.Sum(r => r.OrderCount) }).OrderBy(r => r.BucketStartUtc).ToList();
        }

        private static DateTime StartOfWeek(DateTime date) => date.Date.AddDays(-((int)date.DayOfWeek + 6) % 7);
        private static int Pages(int count, int size) => count == 0 ? 0 : (int)Math.Ceiling(count / (double)size);
        private static decimal Average(decimal value, decimal count) => count == 0 ? 0m : Math.Round(value / count, 3);
        private static decimal Percentage(decimal value, decimal total) => total == 0 ? 0m : Math.Round(value / total * 100m, 2);
        private static void CompleteProduct(RawMaterialProductConsumptionDto row, decimal total) { row.RecipeQuantityPerProduct = Average(row.TotalConsumed, row.QuantitySold); row.Percentage = Percentage(row.TotalConsumed, total); }
        private static RawMaterialConsumptionLeaderDto ToLeader(RawMaterialCategoryConsumptionDto row) => new() { Id = row.CategoryId, Name = row.CategoryName, NameAr = row.CategoryNameAr, Consumed = row.TotalConsumed };

        private sealed class ItemRow
        {
            public Guid OrderId { get; init; }
            public Guid ProductId { get; init; }
            public string ProductName { get; init; } = string.Empty;
            public string? ProductNameAr { get; init; }
            public Guid? CategoryId { get; init; }
            public string CategoryName { get; init; } = string.Empty;
            public string? CategoryNameAr { get; init; }
            public int QuantitySold { get; init; }
            public decimal Consumed { get; init; }
        }
    }
}
