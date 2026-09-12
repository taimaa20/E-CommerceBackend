using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class WasteLogRepository : IWasteLogRepository
    {
        private readonly PosDbContext _context;
        private readonly ILogger<WasteLogRepository> _logger;

        public WasteLogRepository(PosDbContext context, ILogger<WasteLogRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
            => _context.Database.BeginTransactionAsync(ct);

        public async Task AddAsync(WasteLog wasteLog, CancellationToken ct = default)
            => await _context.WasteLogs.AddAsync(wasteLog, ct);

        public async Task AddAuditAsync(WasteLogAudit audit, CancellationToken ct = default)
            => await _context.WasteLogAudits.AddAsync(audit, ct);

        public async Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken ct = default)
            => await _context.InventoryTransactions.AddAsync(transaction, ct);

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);

        public async Task<WasteLog?> GetByIdAsync(Guid id, bool includeAudit, CancellationToken ct = default)
        {
            var query = _context.WasteLogs
                .Include(w => w.SourceOrder)
                .Include(w => w.SourceBatch)
                .Include(w => w.SourceOrderItem)
                    .ThenInclude(i => i!.Product)
                .Include(w => w.Product)
                .Include(w => w.Material)
                .Include(w => w.InventoryTransactions)
                .Include(w => w.Employees)
                    .ThenInclude(e => e.Employee)
                .AsSplitQuery()
                .AsQueryable();

            if (includeAudit)
            {
                query = query.Include(w => w.AuditTrail.OrderBy(a => a.CreatedAt));
            }

            return await query.FirstOrDefaultAsync(w => w.Id == id, ct);
        }

        public Task<Product?> GetProductAsync(Guid id, CancellationToken ct = default)
            => _context.Products
                .AsNoTracking()
                .Include(p => p.RecipeItems)
                    .ThenInclude(ri => ri.RawMaterial)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

        public Task<RawMaterial?> GetMaterialAsync(Guid id, CancellationToken ct = default)
            => _context.RawMaterials
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id, ct);

        public Task<Order?> GetOrderAsync(Guid id, CancellationToken ct = default)
            => _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id, ct);

        public Task<WasteEmployeeOptionDto?> GetEmployeeOptionAsync(Guid id, CancellationToken ct = default)
            => BuildEmployeeOptionQuery(_context.Users.AsNoTracking().Where(u => u.Id == id))
                .FirstOrDefaultAsync(ct);

        public async Task<IReadOnlyList<WasteEmployeeOptionDto>> GetEmployeeOptionsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken ct = default)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<WasteEmployeeOptionDto>();

            return await BuildEmployeeOptionQuery(_context.Users.AsNoTracking().Where(u => ids.Contains(u.Id)))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<WasteEmployeeOptionDto>> SearchEmployeesAsync(string? search, int limit, CancellationToken ct = default)
        {
            var take = Math.Clamp(limit, 1, 50);

            // Active = no HR profile yet, or a profile that is not terminated.
            var query = _context.Users
                .AsNoTracking()
                .Where(u => u.StaffProfile == null || u.StaffProfile.ContractStatus != ContractStatus.Terminated);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(u =>
                    EF.Functions.ILike(u.FullName ?? string.Empty, pattern) ||
                    EF.Functions.ILike(u.FullNameAr ?? string.Empty, pattern) ||
                    EF.Functions.ILike(u.Username, pattern) ||
                    (u.StaffProfile != null && u.StaffProfile.StaffNo != null && EF.Functions.ILike(u.StaffProfile.StaffNo, pattern)));
            }

            return await BuildEmployeeOptionQuery(query)
                .OrderBy(e => e.Name)
                .Take(take)
                .ToListAsync(ct);
        }

        private static IQueryable<WasteEmployeeOptionDto> BuildEmployeeOptionQuery(IQueryable<User> users)
            => users.Select(u => new WasteEmployeeOptionDto
            {
                Id = u.Id,
                Name = u.FullName ?? u.Username,
                NameAr = u.FullNameAr,
                Username = u.Username,
                Role = u.Role.ToString(),
                StaffNo = u.StaffProfile != null ? u.StaffProfile.StaffNo : null
            });

        public async Task<WasteLogPageDto> GetPagedAsync(WasteLogQueryDto query, CancellationToken ct = default)
        {
            var page = Math.Max(query.Page, 1);
            var limit = Math.Clamp(query.Limit, 1, 200);
            var rowsQuery = ApplyFilters(_context.WasteLogs.AsNoTracking(), query);

            var total = await rowsQuery.CountAsync(ct);
            var rows = await rowsQuery
                .OrderByDescending(w => w.WasteDate ?? w.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(w => new WasteLogDto
                {
                    Id = w.Id,
                    WasteNumber = w.WasteNumber,
                    WasteType = w.WasteType.HasValue ? w.WasteType.Value.ToString() : null,
                    Category = w.Category.ToString(),
                    WasteCategory = w.Category.ToString(),
                    ItemId = w.ItemId,
                    ProductId = w.ProductId,
                    MaterialId = w.MaterialId,
                    ItemName = w.ItemName,
                    ItemNameAr = w.ItemNameAr
                        ?? (w.Product != null ? w.Product.NameAr : null)
                        ?? (w.Material != null ? w.Material.NameAr : null)
                        ?? (w.SourceOrderItem != null && w.SourceOrderItem.SelectedOptionNameAr != null ? w.SourceOrderItem.SelectedOptionNameAr : null)
                        ?? (w.SourceOrderItem != null && w.SourceOrderItem.Product != null ? w.SourceOrderItem.Product.NameAr : null),
                    Quantity = w.Quantity,
                    Unit = w.Unit,
                    Reason = w.Reason,
                    ReasonAr = w.Reason,
                    Notes = w.Notes,
                    CostAmount = w.CostAmount > 0m
                        ? w.CostAmount
                        : (w.InventoryTransactions.Sum(t => (decimal?)t.CostAmount) ?? 0m) > 0m
                            ? w.InventoryTransactions.Sum(t => t.CostAmount)
                            : w.Category == WasteCategory.ExpiryProduct && w.SourceBatch != null
                                ? w.Quantity * w.SourceBatch.UnitCost
                                : w.Category == WasteCategory.CancelProduct && w.SourceOrderItem != null
                                    ? w.SourceOrderItem.StockDeductedCost
                                    : (w.Category == WasteCategory.ManualProduct || w.Category == WasteCategory.ManualMaterial) && w.Amount > 0m
                                        ? w.Amount
                                        : 0m,
                    SalePriceLoss = w.SalePriceLoss > 0m
                        ? w.SalePriceLoss
                        : w.Category == WasteCategory.CancelProduct && w.Amount > 0m
                            ? w.Amount
                            : 0m,
                    Status = w.Status.ToString(),
                    CreatedById = w.LoggedById,
                    CreatedBy = w.CreatedBy ?? w.LoggedByName,
                    LoggedBy = w.LoggedByName ?? "System",
                    Employees = w.Employees.Select(e => new WasteParticipantDto
                    {
                        EmployeeId = e.EmployeeId,
                        Name = e.EmployeeNameSnapshot,
                        NameAr = e.Employee != null ? e.Employee.FullNameAr : null
                    }).ToList(),
                    ApprovedById = w.ApprovedById,
                    ApprovedBy = w.ApprovedBy,
                    DecisionAt = w.DecisionAt,
                    BranchId = w.BranchId,
                    OrderId = w.OrderId ?? w.SourceOrderId,
                    SourceOrderId = w.SourceOrderId,
                    OrderNumber = w.SourceOrder != null ? w.SourceOrder.OrderNumber : null,
                    SourceBatchId = w.SourceBatchId,
                    BatchNumber = w.SourceBatchNumber ?? (w.SourceBatch != null ? w.SourceBatch.BatchNumber : null),
                    AttachmentUrl = w.AttachmentUrl,
                    IsAffectingInventory = w.IsAffectingInventory,
                    WasteDate = w.WasteDate ?? w.CreatedAt,
                    CreatedAt = w.CreatedAt,
                    UpdatedAt = w.UpdatedAt
                })
                .ToListAsync(ct);

            await ApplyReasonLabelsAsync(rows, ct);
            return new WasteLogPageDto { Data = rows, Total = total, Page = page, Limit = limit };
        }

        public async Task<WasteLogSummaryDto> GetSummaryAsync(WasteLogQueryDto query, CancellationToken ct = default)
        {
            var rows = BuildMetricRows(ApplyFilters(_context.WasteLogs.AsNoTracking(), query));
            var summary = await BuildSummaryQuery(rows).FirstOrDefaultAsync(ct) ?? new WasteLogSummaryDto();

            _logger.LogDebug(
                "Waste summary query returned {TotalCount} filtered records and {TotalWasteCost} approved cost.",
                summary.TotalCount,
                summary.TotalWasteCost);

            return summary;
        }

        public async Task<WasteLogAnalyticsDto> GetAnalyticsAsync(WasteLogQueryDto query, CancellationToken ct = default)
        {
            var rows = BuildMetricRows(ApplyFilters(_context.WasteLogs.AsNoTracking(), query));
            var approved = rows.Where(w => w.Status == WasteLogStatus.Approved);
            var summary = await BuildSummaryQuery(rows).FirstOrDefaultAsync(ct) ?? new WasteLogSummaryDto();

            var wasteByType = await approved
                .GroupBy(w => new { w.Category, w.WasteType })
                .Select(g => new WasteMetricDto
                {
                    Key = g.Key.WasteType.HasValue
                        ? g.Key.WasteType.Value.ToString()
                        : g.Key.Category == WasteCategory.CancelProduct
                            ? "cancel"
                            : g.Key.Category == WasteCategory.ExpiryProduct ? "expired" : "manual",
                    Label = g.Key.WasteType.HasValue
                        ? g.Key.WasteType.Value.ToString()
                        : g.Key.Category == WasteCategory.CancelProduct
                            ? "cancel"
                            : g.Key.Category == WasteCategory.ExpiryProduct ? "expired" : "manual",
                    Quantity = g.Sum(w => w.Quantity),
                    CostAmount = g.Sum(w => w.CostAmount),
                    SalePriceLoss = g.Sum(w => w.SalePriceLoss),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.CostAmount)
                .ToListAsync(ct);

            var wasteByEmployee = await approved
                .GroupBy(w => new { w.LoggedById, w.LoggedByName, w.LoggedByNameAr })
                .Select(g => new WasteMetricDto
                {
                    Key = g.Key.LoggedById.HasValue ? g.Key.LoggedById.Value.ToString() : "system",
                    Label = g.Key.LoggedByName,
                    LabelAr = g.Key.LoggedByNameAr,
                    Quantity = g.Sum(w => w.Quantity),
                    CostAmount = g.Sum(w => w.CostAmount),
                    SalePriceLoss = g.Sum(w => w.SalePriceLoss),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.CostAmount)
                .Take(10)
                .ToListAsync(ct);

            var topWastedProducts = await approved
                .GroupBy(w => new { w.ItemId, w.ItemName, w.ItemNameAr })
                .Select(g => new WasteMetricDto
                {
                    Key = g.Key.ItemId.HasValue ? g.Key.ItemId.Value.ToString() : g.Key.ItemName,
                    Label = g.Key.ItemName,
                    LabelAr = g.Key.ItemNameAr,
                    Quantity = g.Sum(w => w.Quantity),
                    CostAmount = g.Sum(w => w.CostAmount),
                    SalePriceLoss = g.Sum(w => w.SalePriceLoss),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.CostAmount)
                .Take(10)
                .ToListAsync(ct);

            // Staff-meal totals are computed from the parent WasteLog (one row per meal event).
            var staffMealLogs = ApplyFilters(_context.WasteLogs.AsNoTracking(), query)
                .Where(w => w.Status == WasteLogStatus.Approved && w.WasteType == ManualWasteType.STAFF_MEAL);

            var staffMealTotals = await staffMealLogs
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Quantity = g.Sum(w => w.Quantity),
                    Cost = g.Sum(w => w.CostAmount > 0m
                        ? w.CostAmount
                        : (w.InventoryTransactions.Sum(t => (decimal?)t.CostAmount) ?? 0m))
                })
                .FirstOrDefaultAsync(ct);

            // Per-employee attribution: each participant is credited the meal's full cost/quantity.
            // Summing across employees therefore exceeds StaffMealCost when meals are shared —
            // by design, this measures "how much waste each employee was part of", not a split.
            var staffMealsByEmployee = await _context.WasteLogEmployees
                .AsNoTracking()
                .Where(e => staffMealLogs.Any(w => w.Id == e.WasteLogId))
                .GroupBy(e => new
                {
                    e.EmployeeId,
                    SnapshotName = e.EmployeeNameSnapshot,
                    LiveNameAr = e.Employee != null ? e.Employee.FullNameAr : null
                })
                .Select(g => new WasteMetricDto
                {
                    Key = g.Key.EmployeeId.HasValue ? g.Key.EmployeeId.Value.ToString() : "unknown",
                    Label = g.Key.SnapshotName,
                    LabelAr = g.Key.LiveNameAr,
                    Quantity = g.Sum(e => e.WasteLog.Quantity),
                    CostAmount = g.Sum(e => e.WasteLog.CostAmount > 0m
                        ? e.WasteLog.CostAmount
                        : (e.WasteLog.InventoryTransactions.Sum(t => (decimal?)t.CostAmount) ?? 0m)),
                    SalePriceLoss = g.Sum(e => e.WasteLog.SalePriceLoss),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.CostAmount)
                .Take(10)
                .ToListAsync(ct);

            return new WasteLogAnalyticsDto
            {
                StaffMealCount = staffMealTotals?.Count ?? 0,
                StaffMealQuantity = staffMealTotals?.Quantity ?? 0m,
                StaffMealCost = staffMealTotals?.Cost ?? 0m,
                StaffMealsByEmployee = staffMealsByEmployee,
                TotalCount = summary.TotalCount,
                ManualCount = summary.ManualCount,
                ExpiryCount = summary.ExpiryCount,
                CancellationCount = summary.CancelCount,
                TotalWasteCost = summary.TotalWasteCost,
                TotalSalePriceLoss = summary.TotalSalePriceLoss,
                TotalQuantity = summary.TotalQuantity,
                PendingApprovalCount = summary.ManualPendingCount,
                ApprovedCount = summary.ManualApprovedCount,
                RejectedCount = await rows.CountAsync(w =>
                    (w.Category == WasteCategory.ManualProduct || w.Category == WasteCategory.ManualMaterial)
                    && w.Status == WasteLogStatus.Rejected, ct),
                WasteByType = wasteByType,
                WasteByEmployee = wasteByEmployee,
                TopWastedProducts = topWastedProducts
            };
        }

        public async Task<IReadOnlyList<WasteRecipeRequirement>> GetProductRecipeRequirementsAsync(
            Guid productId,
            decimal quantity,
            CancellationToken ct = default)
        {
            var rows = await _context.RecipeItems
                .AsNoTracking()
                .Where(r => r.ProductId == productId)
                .GroupBy(r => new
                {
                    r.RawMaterialId,
                    r.RawMaterial.Name,
                    r.RawMaterial.NameAr,
                    r.RawMaterial.Unit
                })
                .Select(g => new WasteRecipeRequirement
                {
                    MaterialId = g.Key.RawMaterialId,
                    MaterialName = g.Key.Name,
                    MaterialNameAr = g.Key.NameAr,
                    Unit = g.Key.Unit.ToString(),
                    Quantity = g.Sum(x => x.Amount) * quantity
                })
                .ToListAsync(ct);

            return rows;
        }

        public async Task<decimal> GetAvailableStockAsync(Guid materialId, Guid branchId, bool allowLegacyFallback, CancellationToken ct = default)
        {
            var hasBatches = await _context.StockBatches.AnyAsync(batch => batch.MaterialId == materialId && batch.BranchId == branchId, ct);
            if (!hasBatches)
            {
                if (!allowLegacyFallback)
                    return 0m;

                return await _context.RawMaterials
                    .Where(m => m.Id == materialId)
                    .Select(m => m.CurrentStock)
                    .FirstOrDefaultAsync(ct);
            }

            return await _context.StockBatches
                .Where(batch => batch.MaterialId == materialId
                             && batch.BranchId == branchId
                             && batch.IsApproved
                             && (batch.PurchaseOrderId == null || batch.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                             && batch.Status == BatchStatus.Good)
                .SumAsync(batch => batch.RemainingQuantity, ct);
        }

        private static IQueryable<WasteMetricRow> BuildMetricRows(IQueryable<WasteLog> query)
        {
            return query.Select(w => new WasteMetricRow
            {
                Category = w.Category,
                Status = w.Status,
                WasteType = w.WasteType,
                LoggedById = w.LoggedById,
                LoggedByName = w.LoggedBy != null ? (w.LoggedBy.FullName ?? w.LoggedBy.Username) : (w.LoggedByName ?? w.CreatedBy ?? "System"),
                LoggedByNameAr = w.LoggedBy != null ? w.LoggedBy.FullNameAr : (w.LoggedById == null && w.LoggedByName == null && w.CreatedBy == null ? "النظام" : null),
                ItemId = w.ProductId ?? w.MaterialId ?? w.ItemId,
                ItemName = w.ItemName,
                ItemNameAr = w.ItemNameAr
                    ?? (w.Product != null ? w.Product.NameAr : null)
                    ?? (w.Material != null ? w.Material.NameAr : null)
                    ?? (w.SourceOrderItem != null && w.SourceOrderItem.SelectedOptionNameAr != null ? w.SourceOrderItem.SelectedOptionNameAr : null)
                    ?? (w.SourceOrderItem != null && w.SourceOrderItem.Product != null ? w.SourceOrderItem.Product.NameAr : null),
                WasteDate = w.WasteDate ?? w.CreatedAt,
                Quantity = w.Quantity,
                CostAmount = w.CostAmount > 0m
                    ? w.CostAmount
                    : (w.InventoryTransactions.Sum(t => (decimal?)t.CostAmount) ?? 0m) > 0m
                        ? w.InventoryTransactions.Sum(t => t.CostAmount)
                        : w.Category == WasteCategory.ExpiryProduct && w.SourceBatch != null
                            ? w.Quantity * w.SourceBatch.UnitCost
                            : w.Category == WasteCategory.CancelProduct && w.SourceOrderItem != null
                                ? w.SourceOrderItem.StockDeductedCost
                                : (w.Category == WasteCategory.ManualProduct || w.Category == WasteCategory.ManualMaterial) && w.Amount > 0m
                                    ? w.Amount
                                    : 0m,
                SalePriceLoss = w.SalePriceLoss > 0m
                    ? w.SalePriceLoss
                    : w.Category == WasteCategory.CancelProduct && w.Amount > 0m
                        ? w.Amount
                        : 0m
            });
        }

        private static IQueryable<WasteLogSummaryDto> BuildSummaryQuery(IQueryable<WasteMetricRow> rows)
        {
            return rows.GroupBy(_ => 1).Select(g => new WasteLogSummaryDto
            {
                TotalCount = g.Count(),
                CancelCount = g.Sum(w => w.Category == WasteCategory.CancelProduct ? 1 : 0),
                ExpiryCount = g.Sum(w => w.Category == WasteCategory.ExpiryProduct ? 1 : 0),
                ManualCount = g.Sum(w => w.Category == WasteCategory.ManualProduct || w.Category == WasteCategory.ManualMaterial ? 1 : 0),
                ManualPendingCount = g.Sum(w => (w.Category == WasteCategory.ManualProduct || w.Category == WasteCategory.ManualMaterial)
                    && w.Status == WasteLogStatus.Pending ? 1 : 0),
                ManualApprovedCount = g.Sum(w => (w.Category == WasteCategory.ManualProduct || w.Category == WasteCategory.ManualMaterial)
                    && w.Status == WasteLogStatus.Approved ? 1 : 0),
                TotalWasteCost = g.Sum(w => w.Status == WasteLogStatus.Approved ? w.CostAmount : 0m),
                TotalSalePriceLoss = g.Sum(w => w.Status == WasteLogStatus.Approved ? w.SalePriceLoss : 0m),
                TotalQuantity = g.Sum(w => w.Quantity)
            });
        }

        private IQueryable<WasteLog> ApplyFilters(IQueryable<WasteLog> query, WasteLogQueryDto filters)
        {
            if (!string.IsNullOrWhiteSpace(filters.Category) &&
                Enum.TryParse<WasteCategory>(filters.Category, ignoreCase: true, out var category))
            {
                query = query.Where(w => w.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(filters.WasteType) &&
                Enum.TryParse<ManualWasteType>(filters.WasteType, ignoreCase: true, out var wasteType))
            {
                query = query.Where(w => w.WasteType == wasteType);
            }

            if (!string.IsNullOrWhiteSpace(filters.Status) &&
                Enum.TryParse<WasteLogStatus>(filters.Status, ignoreCase: true, out var status))
            {
                query = query.Where(w => w.Status == status);
            }

            if (filters.EmployeeId.HasValue)
                query = query.Where(w => w.LoggedById == filters.EmployeeId.Value);

            if (filters.StaffMealEmployeeId.HasValue)
                query = query.Where(w => w.Employees.Any(e => e.EmployeeId == filters.StaffMealEmployeeId.Value));

            if (filters.ProductId.HasValue)
                query = query.Where(w => w.ProductId == filters.ProductId.Value || w.ItemId == filters.ProductId.Value);

            if (filters.MaterialId.HasValue)
                query = query.Where(w => w.MaterialId == filters.MaterialId.Value || w.ItemId == filters.MaterialId.Value);

            if (filters.BranchId.HasValue)
                query = query.Where(w => w.BranchId == filters.BranchId.Value);

            if (filters.DateFrom.HasValue)
                query = query.Where(w => (w.WasteDate ?? w.CreatedAt) >= NormalizeDateFilter(filters.DateFrom.Value));

            if (filters.DateTo.HasValue)
                query = query.Where(w => (w.WasteDate ?? w.CreatedAt) < NormalizeDateFilter(filters.DateTo.Value).AddDays(1));

            return query;
        }

        private static DateTime NormalizeDateFilter(DateTime value)
            => value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

        private async Task ApplyReasonLabelsAsync(List<WasteLogDto> rows, CancellationToken ct)
        {
            var rawReasons = rows
                .Select(r => GetReasonCode(r.Reason))
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct()
                .ToList();

            if (rawReasons.Count == 0)
                return;

            var reasonLookup = await _context.CancelReasons
                .AsNoTracking()
                .Where(r => rawReasons.Contains(r.Code))
                .ToDictionaryAsync(r => r.Code, r => new ReasonLabels(r.Name, r.NameAr), ct);

            foreach (var row in rows)
            {
                var (label, labelAr) = FormatReason(row.Reason, reasonLookup);
                row.Reason = label;
                row.ReasonAr = labelAr;
            }
        }

        private static string? GetReasonCode(string? rawReason)
        {
            if (string.IsNullOrWhiteSpace(rawReason))
                return null;

            var idx = rawReason.IndexOf(':');
            return (idx >= 0 ? rawReason[..idx] : rawReason).Trim();
        }

        private static (string Label, string LabelAr) FormatReason(string? rawReason, IReadOnlyDictionary<string, ReasonLabels> reasonLookup)
        {
            if (string.IsNullOrWhiteSpace(rawReason))
                return (string.Empty, string.Empty);

            var idx = rawReason.IndexOf(':');
            var code = GetReasonCode(rawReason);
            var note = idx >= 0 ? rawReason[(idx + 1)..].Trim() : string.Empty;

            if (!string.IsNullOrWhiteSpace(code) && reasonLookup.TryGetValue(code, out var mapped))
            {
                var en = string.IsNullOrWhiteSpace(note) ? mapped.Name : $"{mapped.Name}: {note}";
                var ar = string.IsNullOrWhiteSpace(note) ? mapped.NameAr : $"{mapped.NameAr}: {note}";
                return (en, ar);
            }

            return (rawReason, rawReason);
        }

        private sealed class WasteMetricRow
        {
            public WasteCategory Category { get; set; }
            public WasteLogStatus Status { get; set; }
            public ManualWasteType? WasteType { get; set; }
            public Guid? LoggedById { get; set; }
            public string LoggedByName { get; set; } = string.Empty;
            public string? LoggedByNameAr { get; set; }
            public Guid? ItemId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public string? ItemNameAr { get; set; }
            public DateTime WasteDate { get; set; }
            public decimal Quantity { get; set; }
            public decimal CostAmount { get; set; }
            public decimal SalePriceLoss { get; set; }
        }

        private sealed record ReasonLabels(string Name, string NameAr);
    }
}
