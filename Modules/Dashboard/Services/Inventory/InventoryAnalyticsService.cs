using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Inventory;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Predicates;
using RestaurantPos.Api.Modules.Dashboard.Queries;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Inventory
{
    public sealed class InventoryAnalyticsService : IInventoryAnalyticsService
    {
        // "Expiring" widgets share one business definition: a batch is expiring
        // when its expiry date is between now (inclusive) and now + N days —
        // already-expired batches are deliberately excluded (they are reviewed
        // via the separate Expired-Inventory backlog item).
        private const int ExpiringKpiDays  = 3;  // "Expiring < 3 days" KPI horizon
        private const int ExpiringListDays = 7;  // "Expiring soon" list horizon

        private readonly IMetricQueryBuilder _q;
        private readonly IDashboardFilterContext _ctx;
        private readonly IBranchContext _branchContext;
        private readonly ILogger<InventoryAnalyticsService> _logger;

        public InventoryAnalyticsService(
            IMetricQueryBuilder q,
            IDashboardFilterContext ctx,
            IBranchContext branchContext,
            ILogger<InventoryAnalyticsService> logger)
        {
            _q   = q   ?? throw new ArgumentNullException(nameof(q));
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InventorySnapshotDto> BuildSnapshotAsync(CancellationToken ct)
        {
            var stock  = await SafeAsync.RunAsync(_logger, "inv.stock",      () => BuildStockHealthAsync(ct),         new StockHealthDto());
            var waste  = await SafeAsync.RunAsync(_logger, "inv.waste",      () => BuildWasteAsync(ct),               new WasteBreakdownDto());
            var topW   = await SafeAsync.RunAsync(_logger, "inv.topWasted",  () => BuildTopWastedProductsAsync(ct),   (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var byEmp  = await SafeAsync.RunAsync(_logger, "inv.byEmployee", () => BuildWasteByEmployeeAsync(ct),     (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var trend  = await SafeAsync.RunAsync(_logger, "inv.trend",      () => BuildWasteDailyTrendAsync(ct),     (IReadOnlyList<TimeSeriesPointDto>)Array.Empty<TimeSeriesPointDto>());
            var expiry = await SafeAsync.RunAsync(_logger, "inv.expiring",   () => BuildExpiringSoonAsync(ct),        (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var byCat  = await SafeAsync.RunAsync(_logger, "inv.byCategory", () => BuildWasteByCategoryAsync(ct),     (IReadOnlyList<CategorySliceDto>)Array.Empty<CategorySliceDto>());

            return new InventorySnapshotDto
            {
                Scope            = _ctx.Scope,
                WindowStartUtc   = _ctx.Window.StartUtc,
                WindowEndUtc     = _ctx.Window.EndUtc,
                AppliedFilter    = _ctx.Filter,
                StockHealth      = stock,
                Waste            = waste,
                TopWastedProducts= topW,
                WasteByEmployee  = byEmp,
                WasteDailyTrend  = trend,
                ExpiringSoon     = expiry,
                WasteByCategory  = byCat
            };
        }

        private async Task<StockHealthDto> BuildStockHealthAsync(CancellationToken ct)
        {
            // Warehouse-aware when a specific warehouse is selected (per-warehouse
            // ledger with its own Minimum/Reorder thresholds); otherwise the aggregate
            // material snapshot across all warehouses, where MinimumAlertLevel serves as
            // both thresholds — byte-identical to the previous behaviour for the Main
            // Branch and All-Branches scopes. Either way the shared StockLevelPolicy
            // classifies every row so the whole system agrees.
            var rows = _ctx.Filter.WarehouseId.HasValue
                ? await _q.RawMaterialInventories()
                    .Select(i => new StockRow(i.Quantity, i.MinimumQuantity, i.ReorderLevel, i.RawMaterial!.CostPerUnit))
                    .ToListAsync(ct)
                : await BuildAggregateStockRowsAsync(ct);

            var out_    = rows.Count(r => StockLevelPolicy.IsOutOfStock(r.Quantity));
            var low     = rows.Count(r => StockLevelPolicy.IsLowStock(r.Quantity, r.MinimumQuantity));
            var reorder = rows.Count(r => StockLevelPolicy.IsReorderRequired(r.Quantity, r.ReorderLevel));
            var value   = rows.Sum(r => r.Quantity * r.CostPerUnit);

            // Expiry is batch-level and StockBatch carries no WarehouseId, so this count
            // stays global (all warehouses) until the schema gains warehouse ownership.
            var nowU = DateTime.UtcNow;
            var soon = nowU.AddDays(ExpiringKpiDays);
            var expiring = await _q.StockBatches()
                .CountAsync(b => b.ExpiryDate >= nowU && b.ExpiryDate < soon, ct);

            // Stock turnover = COGS consumed in the window ÷ current inventory value.
            // COGS reuses the shared RecordedSale predicate so it ties out to the
            // financial dashboard's COGS to the cent.
            var cogs = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .SumAsync(o => (decimal?)o.TotalCost, ct) ?? 0m;
            var turnover = value == 0 ? 0m : Math.Round(cogs / value, 2);
            var windowDays = (decimal)Math.Max(1, (_ctx.Window.EndUtc - _ctx.Window.StartUtc).TotalDays);
            var daysOnHand = cogs == 0 ? 0m : Math.Round(value / (cogs / windowDays), 1);

            return new StockHealthDto
            {
                OutOfStockCount     = out_,
                LowStockCount       = low,
                ExpiringWithin3Days = expiring,
                TotalInventoryValue = Math.Round(value, 2),
                ReorderNeededCount  = reorder,
                PeriodCogs          = Math.Round(cogs, 2),
                StockTurnoverRatio  = turnover,
                DaysInventoryOnHand = daysOnHand
            };
        }

        private readonly record struct StockRow(
            decimal Quantity, decimal MinimumQuantity, decimal ReorderLevel, decimal CostPerUnit);

        // RawMaterial.CurrentStock/CostPerUnit are a Main-Branch-only mirror (see
        // InventoryService's IsMainBranch write guards) — correct when the dashboard
        // is scoped to the Main Branch (or All Branches, which historically read the
        // same mirror), but wrong for any other specific branch: it would silently
        // show Main Branch's numbers under that branch's name. For a non-Main branch
        // selection, aggregate the true per-branch ledger (RawMaterialInventory)
        // across all warehouses instead.
        private async Task<List<StockRow>> BuildAggregateStockRowsAsync(CancellationToken ct)
        {
            if (_ctx.Filter.BranchId.HasValue)
            {
                var context = await _branchContext.GetCurrentAsync(ct);
                var isMainBranch = context.AssignedBranches
                    .FirstOrDefault(b => b.Id == _ctx.Filter.BranchId.Value)?.IsMainBranch
                    ?? context.CurrentBranch.IsMainBranch;

                if (!isMainBranch)
                {
                    return await _q.RawMaterialInventories()
                        .GroupBy(i => new { i.RawMaterialId, i.RawMaterial!.CostPerUnit, i.RawMaterial.MinimumAlertLevel })
                        .Select(g => new StockRow(g.Sum(i => i.Quantity), g.Key.MinimumAlertLevel, g.Key.MinimumAlertLevel, g.Key.CostPerUnit))
                        .ToListAsync(ct);
                }
            }

            return await _q.RawMaterials()
                .Select(m => new StockRow(m.CurrentStock, m.MinimumAlertLevel, m.MinimumAlertLevel, m.CostPerUnit))
                .ToListAsync(ct);
        }

        private async Task<WasteBreakdownDto> BuildWasteAsync(CancellationToken ct)
        {
            var rows = BuildWasteMetricRows()
                .Where(w => w.Status == WasteLogStatus.Approved);

            var rollup = await rows.GroupBy(_ => 1)
                .Select(g => new
                {
                    Manual = g.Sum(w => w.Category == WasteCategory.ManualProduct || w.Category == WasteCategory.ManualMaterial ? 1 : 0),
                    Expiry = g.Sum(w => w.Category == WasteCategory.ExpiryProduct ? 1 : 0),
                    Cancel = g.Sum(w => w.Category == WasteCategory.CancelProduct ? 1 : 0),
                    Cost = g.Sum(w => w.CostAmount),
                    Sale = g.Sum(w => w.SalePriceLoss),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync(ct);

            // % of revenue computed against confirmed revenue within the same window.
            var revenue = await _q.Orders()
                .Where(OrderLifecyclePredicates.Confirmed)
                .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
            var cost = rollup?.Cost ?? 0m;
            var wastePct = revenue == 0 ? 0m : Math.Round(cost / revenue * 100m, 2);

            _logger.LogDebug(
                "Inventory dashboard waste aggregation included {Count} approved records. Cost={Cost}, SaleLoss={SaleLoss}, Revenue={Revenue}",
                rollup?.Count ?? 0,
                cost,
                rollup?.Sale ?? 0m,
                revenue);

            return new WasteBreakdownDto
            {
                ManualCount           = rollup?.Manual ?? 0,
                ExpiryCount           = rollup?.Expiry ?? 0,
                CancellationCount     = rollup?.Cancel ?? 0,
                TotalCost             = Math.Round(cost, 2),
                SalePriceLoss         = Math.Round(rollup?.Sale ?? 0m, 2),
                WastePercentOfRevenue = wastePct
            };
        }

        private async Task<IReadOnlyList<RankedRowDto>> BuildTopWastedProductsAsync(CancellationToken ct)
        {
            var rows = await BuildWasteMetricRows()
                .Where(w => w.Status == WasteLogStatus.Approved)
                .GroupBy(w => new { w.ItemId, w.ItemName, w.ItemNameAr })
                .Select(g => new
                {
                    g.Key.ItemId,
                    g.Key.ItemName,
                    g.Key.ItemNameAr,
                    Cost  = g.Sum(w => w.CostAmount),
                    Loss  = g.Sum(w => w.SalePriceLoss),
                    Count = g.Count(),
                })
                .OrderByDescending(r => r.Cost)
                .Take(10)
                .ToListAsync(ct);

            var materialNamesAr = await BuildMaterialNameArMapAsync(rows.Select(r => r.ItemId), ct);
            return rows.Select(r => new RankedRowDto(
                r.ItemId,
                r.ItemName,
                FirstText(r.ItemNameAr, r.ItemId.HasValue ? materialNamesAr.GetValueOrDefault(r.ItemId.Value) : null),
                r.Cost,
                r.Loss,
                r.Count)).ToList();
        }

        private async Task<IReadOnlyList<RankedRowDto>> BuildWasteByEmployeeAsync(CancellationToken ct)
        {
            var rows = await BuildWasteMetricRows()
                .Where(w => w.Status == WasteLogStatus.Approved)
                .GroupBy(w => new { w.LoggedById, w.LoggedByName, w.LoggedByNameAr })
                .Select(g => new
                {
                    Id = g.Key.LoggedById,
                    Name = g.Key.LoggedByName,
                    NameAr = g.Key.LoggedByNameAr,
                    Cost  = g.Sum(w => w.CostAmount),
                    Count = g.Count(),
                })
                .OrderByDescending(r => r.Cost)
                .Take(10)
                .ToListAsync(ct);
            return rows.Select(r => new RankedRowDto(r.Id, r.Name, r.NameAr, r.Cost, null, r.Count)).ToList();
        }

        private async Task<IReadOnlyList<TimeSeriesPointDto>> BuildWasteDailyTrendAsync(CancellationToken ct)
        {
            var rows = await BuildWasteMetricRows()
                .Where(w => w.Status == WasteLogStatus.Approved)
                .GroupBy(w => new { w.CreatedAt.Year, w.CreatedAt.Month, w.CreatedAt.Day })
                .Select(g => new
                {
                    g.Key.Year, g.Key.Month, g.Key.Day,
                    Cost = g.Sum(w => w.CostAmount),
                    Count = g.Count()
                })
                .ToListAsync(ct);

            return rows.Select(r => new TimeSeriesPointDto(
                    new DateTime(r.Year, r.Month, r.Day, 0, 0, 0, DateTimeKind.Utc),
                    Math.Round(r.Cost, 2),
                    r.Count))
                .OrderBy(p => p.BucketStartUtc)
                .ToList();
        }

        private async Task<IReadOnlyList<RankedRowDto>> BuildExpiringSoonAsync(CancellationToken ct)
        {
            var nowU = DateTime.UtcNow;
            var horizon = nowU.AddDays(ExpiringListDays);
            var rows = await _q.StockBatches()
                .Where(b => b.ExpiryDate >= nowU && b.ExpiryDate < horizon)
                .OrderBy(b => b.ExpiryDate)
                .Take(10)
                .Select(b => new
                {
                    b.Id,
                    Name   = b.RawMaterial.Name,
                    NameAr = b.RawMaterial.NameAr,
                    Value  = b.RemainingQuantity * b.UnitCost,
                    Qty    = b.RemainingQuantity,
                })
                .ToListAsync(ct);
            return rows.Select(r => new RankedRowDto(r.Id, r.Name, r.NameAr, r.Value, r.Qty, null)).ToList();
        }

        private async Task<IReadOnlyList<CategorySliceDto>> BuildWasteByCategoryAsync(CancellationToken ct)
        {
            var rows = await BuildWasteMetricRows()
                .Where(w => w.Status == WasteLogStatus.Approved)
                .GroupBy(w => w.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Cost = g.Sum(w => w.CostAmount),
                    Count = g.Count()
                })
                .ToListAsync(ct);

            var total = rows.Sum(r => r.Cost);
            return rows.Select(r => new CategorySliceDto(
                r.Category.ToString(), WasteCategoryNameAr(r.Category), Math.Round(r.Cost, 2),
                total == 0 ? 0 : Math.Round(r.Cost / total * 100m, 2), r.Count)).ToList();
        }

        private IQueryable<WasteMetricRow> BuildWasteMetricRows()
            => WasteMetricProjection.Project(_q.WasteLogs());

        private static string WasteCategoryNameAr(WasteCategory category)
            => category switch
            {
                WasteCategory.CancelProduct => "إلغاء منتج",
                WasteCategory.ExpiryProduct => "انتهاء منتج",
                WasteCategory.ManualProduct => "هدر منتج يدوي",
                WasteCategory.ManualMaterial => "هدر مادة يدوي",
                _ => category.ToString()
            };

        private async Task<Dictionary<Guid, string?>> BuildMaterialNameArMapAsync(IEnumerable<Guid?> ids, CancellationToken ct)
        {
            var keys = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
            if (keys.Length == 0) return new Dictionary<Guid, string?>();

            return await _q.RawMaterials()
                .Where(m => keys.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.NameAr, ct);
        }

        private static string? FirstText(params string?[] values)
            => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }
}
