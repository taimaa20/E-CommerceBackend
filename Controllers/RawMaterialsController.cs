using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Services.Caching;
using IMediator = MediatR.IMediator;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RawMaterialsController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly ICacheService _cache;
        private readonly IMediator _mediator;
        private readonly IBranchContext _branchContext;
        private readonly ITenantResolver _tenantResolver;

        public RawMaterialsController(
            PosDbContext context,
            ICacheService cache,
            IMediator mediator,
            IBranchContext branchContext,
            ITenantResolver tenantResolver)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        // GET: api/RawMaterials
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RawMaterialResponseDto>>> GetRawMaterials()
        {
            var isArabic = IsArabicRequested(Request);
            var branchContext = await _branchContext.GetCurrentAsync(HttpContext.RequestAborted);
            var branchId = branchContext.CurrentBranch.Id;
            var isMainBranch = branchContext.CurrentBranch.IsMainBranch;
            var nearExpiryDays = await GetNearExpiryDaysAsync();
            var today = DateTime.UtcNow.Date;
            var nearExpiryThreshold = today.AddDays(nearExpiryDays);
            var items = await _context.RawMaterials
                .OrderBy(m => m.Name)
                .Select(m => new RawMaterialResponseDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    NameAr = m.NameAr,
                    DisplayName = isArabic && m.NameAr != null && m.NameAr != string.Empty ? m.NameAr : m.Name,
                    Unit = m.Unit,
                    CurrentStock = _context.StockBatches.Any(sb => sb.MaterialId == m.Id && sb.BranchId == branchId)
                        ? (_context.StockBatches
                            .Where(sb => sb.MaterialId == m.Id
                                      && sb.BranchId == branchId
                                      && sb.IsApproved
                                      && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                      && sb.Status == BatchStatus.Good)
                            .Sum(sb => (decimal?)sb.RemainingQuantity) ?? 0m)
                        : isMainBranch ? m.CurrentStock : 0m,
                    MinimumAlertLevel = m.MinimumAlertLevel,
                    CostPerUnit = m.CostPerUnit,
                    ShowInMenu = m.ShowInMenu,
                    IsPostPrice = m.IsPostPrice,
                    NextExpiryDate = _context.StockBatches
                        .Where(sb => sb.MaterialId == m.Id
                                  && sb.BranchId == branchId
                                  && sb.IsApproved
                                  && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                  && sb.RemainingQuantity > 0
                                  && sb.Status != BatchStatus.Finished)
                        .OrderBy(sb => sb.ExpiryDate)
                        .Select(sb => (DateTime?)sb.ExpiryDate)
                        .FirstOrDefault(),
                    HasExpiredBatch = _context.StockBatches
                        .Any(sb => sb.MaterialId == m.Id
                                && sb.BranchId == branchId
                                && sb.IsApproved
                                && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                && sb.RemainingQuantity > 0
                                && sb.ExpiryDate < today
                                && sb.Status != BatchStatus.Finished),
                    HasNearExpiryBatch = _context.StockBatches
                        .Any(sb => sb.MaterialId == m.Id
                                && sb.BranchId == branchId
                                && sb.IsApproved
                                && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                && sb.RemainingQuantity > 0
                                && sb.ExpiryDate >= today
                                && sb.ExpiryDate <= nearExpiryThreshold
                                && sb.Status == BatchStatus.Good),
                    DefaultWarehouseId = m.DefaultWarehouseId,
                    DefaultWarehouseName = m.DefaultWarehouse != null ? m.DefaultWarehouse.Name : null,
                    DefaultWarehouseCode = m.DefaultWarehouse != null ? m.DefaultWarehouse.Code : null
                })
                .ToListAsync();

            return Ok(items);
        }

        // GET: api/RawMaterials/paginated
        [HttpGet("paginated")]
        public async Task<ActionResult<PaginatedResponse<RawMaterialResponseDto>>> GetRawMaterialsPaginated(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string searchTerm = "",
            [FromQuery] string sortBy = "name",
            [FromQuery] string sortOrder = "asc")
        {
            var isArabic = IsArabicRequested(Request);
            var branchContext = await _branchContext.GetCurrentAsync(HttpContext.RequestAborted);
            var branchId = branchContext.CurrentBranch.Id;
            var isMainBranch = branchContext.CurrentBranch.IsMainBranch;
            var nearExpiryDays = await GetNearExpiryDaysAsync();
            var today = DateTime.UtcNow.Date;
            var nearExpiryThreshold = today.AddDays(nearExpiryDays);
            var query = _context.RawMaterials.AsQueryable();
            var normalizedSearchTerm = (searchTerm ?? string.Empty).Trim();

            // Search filter
            if (!string.IsNullOrWhiteSpace(normalizedSearchTerm))
            {
                query = query.Where(m =>
                    EF.Functions.ILike(m.Name, $"%{normalizedSearchTerm}%") ||
                    (m.NameAr != null && EF.Functions.ILike(m.NameAr, $"%{normalizedSearchTerm}%")));
            }

            // Sorting
            query = sortBy.ToLower() switch
            {
                "stock" => sortOrder == "desc"
                    ? query.OrderByDescending(m => _context.StockBatches.Any(sb => sb.MaterialId == m.Id && sb.BranchId == branchId)
                        ? (_context.StockBatches
                            .Where(sb => sb.MaterialId == m.Id
                                      && sb.BranchId == branchId
                                      && sb.IsApproved
                                      && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                      && sb.Status == BatchStatus.Good)
                            .Sum(sb => (decimal?)sb.RemainingQuantity) ?? 0m)
                        : isMainBranch ? m.CurrentStock : 0m)
                    : query.OrderBy(m => _context.StockBatches.Any(sb => sb.MaterialId == m.Id && sb.BranchId == branchId)
                        ? (_context.StockBatches
                            .Where(sb => sb.MaterialId == m.Id
                                      && sb.BranchId == branchId
                                      && sb.IsApproved
                                      && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                      && sb.Status == BatchStatus.Good)
                            .Sum(sb => (decimal?)sb.RemainingQuantity) ?? 0m)
                        : isMainBranch ? m.CurrentStock : 0m),
                "cost" => sortOrder == "desc" ? query.OrderByDescending(m => m.CostPerUnit) : query.OrderBy(m => m.CostPerUnit),
                "expiry" => sortOrder == "desc"
                    ? query
                        .OrderByDescending(m => _context.StockBatches
                            .Where(sb => sb.MaterialId == m.Id
                                      && sb.BranchId == branchId
                                      && sb.IsApproved
                                      && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                      && sb.RemainingQuantity > 0
                                      && sb.Status != BatchStatus.Finished)
                            .Select(sb => (DateTime?)sb.ExpiryDate)
                            .Min())
                        .ThenBy(m => m.Name)
                    : query
                        .OrderBy(m => _context.StockBatches
                            .Where(sb => sb.MaterialId == m.Id
                                      && sb.BranchId == branchId
                                      && sb.IsApproved
                                      && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                      && sb.RemainingQuantity > 0
                                      && sb.Status != BatchStatus.Finished)
                            .Select(sb => (DateTime?)sb.ExpiryDate)
                            .Min() == null)
                        .ThenBy(m => _context.StockBatches
                            .Where(sb => sb.MaterialId == m.Id
                                      && sb.BranchId == branchId
                                      && sb.IsApproved
                                      && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                      && sb.RemainingQuantity > 0
                                      && sb.Status != BatchStatus.Finished)
                            .Select(sb => (DateTime?)sb.ExpiryDate)
                            .Min())
                        .ThenBy(m => m.Name),
                _ => sortOrder == "desc" ? query.OrderByDescending(m => m.Name) : query.OrderBy(m => m.Name)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new RawMaterialResponseDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    NameAr = m.NameAr,
                    DisplayName = isArabic && m.NameAr != null && m.NameAr != string.Empty ? m.NameAr : m.Name,
                    Unit = m.Unit,
                    CurrentStock = _context.StockBatches.Any(sb => sb.MaterialId == m.Id && sb.BranchId == branchId)
                        ? (_context.StockBatches
                            .Where(sb => sb.MaterialId == m.Id
                                      && sb.BranchId == branchId
                                      && sb.IsApproved
                                      && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                      && sb.Status == BatchStatus.Good)
                            .Sum(sb => (decimal?)sb.RemainingQuantity) ?? 0m)
                        : isMainBranch ? m.CurrentStock : 0m,
                    MinimumAlertLevel = m.MinimumAlertLevel,
                    CostPerUnit = m.CostPerUnit,
                    ShowInMenu = m.ShowInMenu,
                    IsPostPrice = m.IsPostPrice,
                    NextExpiryDate = _context.StockBatches
                        .Where(sb => sb.MaterialId == m.Id
                                  && sb.BranchId == branchId
                                  && sb.IsApproved
                                  && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                  && sb.RemainingQuantity > 0
                                  && sb.Status != BatchStatus.Finished)
                        .OrderBy(sb => sb.ExpiryDate)
                        .Select(sb => (DateTime?)sb.ExpiryDate)
                        .FirstOrDefault(),
                    HasExpiredBatch = _context.StockBatches
                        .Any(sb => sb.MaterialId == m.Id
                                && sb.BranchId == branchId
                                && sb.IsApproved
                                && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                && sb.RemainingQuantity > 0
                                && sb.ExpiryDate < today
                                && sb.Status != BatchStatus.Finished),
                    HasNearExpiryBatch = _context.StockBatches
                        .Any(sb => sb.MaterialId == m.Id
                                && sb.BranchId == branchId
                                && sb.IsApproved
                                && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                && sb.RemainingQuantity > 0
                                && sb.ExpiryDate >= today
                                && sb.ExpiryDate <= nearExpiryThreshold
                                && sb.Status == BatchStatus.Good),
                    DefaultWarehouseId = m.DefaultWarehouseId,
                    DefaultWarehouseName = m.DefaultWarehouse != null ? m.DefaultWarehouse.Name : null,
                    DefaultWarehouseCode = m.DefaultWarehouse != null ? m.DefaultWarehouse.Code : null
                })
                .ToListAsync();

            return Ok(new PaginatedResponse<RawMaterialResponseDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        // POST: api/RawMaterials
        [HttpPost]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<RawMaterial>> CreateRawMaterial(RawMaterial rawMaterial, CancellationToken ct)
        {
            // Validation: Name required
            if (string.IsNullOrWhiteSpace(rawMaterial.Name))
                return BadRequest("Name is required");
            if (rawMaterial.CostPerUnit < 0m)
                return BadRequest("Cost per unit cannot be negative.");

            // Duplicate EN name check (case-insensitive, same tenant)
            var duplicateEn = await _context.RawMaterials
                .AnyAsync(m => m.Name.ToLower() == rawMaterial.Name.Trim().ToLower());
            if (duplicateEn)
                return Conflict(new { message = "A raw material with this English name already exists" });

            // Duplicate AR name check (if provided)
            if (!string.IsNullOrWhiteSpace(rawMaterial.NameAr))
            {
                var duplicateAr = await _context.RawMaterials
                    .AnyAsync(m => m.NameAr != null && m.NameAr.ToLower() == rawMaterial.NameAr.Trim().ToLower());
                if (duplicateAr)
                    return Conflict(new { message = "A raw material with this Arabic name already exists" });
            }

            rawMaterial.Id = Guid.NewGuid();
            // Default Tenant
            rawMaterial.TenantId = _tenantResolver.GetTenantId();
            var branchId = await GetCurrentBranchIdAsync(ct);
            // New materials start empty; stock and weighted cost are updated after procurement flows.
            rawMaterial.CurrentStock = 0;
            rawMaterial.CostPerUnit = 0;

            // Resolve the warehouse this material lives in. Falls back to Main
            // so legacy/no-selection materials remain consistent with the
            // backfill seed.
            var targetWarehouseId = await ResolveTargetWarehouseIdAsync(rawMaterial.DefaultWarehouseId, rawMaterial.TenantId);
            rawMaterial.DefaultWarehouseId = targetWarehouseId;

            _context.RawMaterials.Add(rawMaterial);

            if (targetWarehouseId.HasValue)
            {
                _context.RawMaterialInventories.Add(new RawMaterialInventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = rawMaterial.TenantId,
                    BranchId = branchId,
                    RawMaterialId = rawMaterial.Id,
                    WarehouseId = targetWarehouseId.Value,
                    Quantity = 0m,
                    MinimumQuantity = rawMaterial.MinimumAlertLevel,
                    ReorderLevel = rawMaterial.MinimumAlertLevel
                });
            }

            await _context.SaveChangesAsync(ct);
            await InvalidateMenuVisibilityCachesAsync(rawMaterial.TenantId);

            return CreatedAtAction("GetRawMaterials", new { id = rawMaterial.Id }, rawMaterial);
        }

        // Helper: resolve the warehouse a material should be associated with.
        // Prefers the explicit value when valid + active; falls back to the
        // tenant's Main warehouse seeded by WarehouseBackfill.
        private async Task<Guid?> ResolveTargetWarehouseIdAsync(Guid? requested, Guid tenantId)
        {
            if (requested.HasValue && requested.Value != Guid.Empty)
            {
                var ok = await _context.Warehouses
                    .AnyAsync(w => w.Id == requested.Value && w.IsActive);
                if (ok) return requested;
            }

            return await _context.Warehouses
                .Where(w => w.Type == WarehouseType.Main && w.IsActive)
                .Select(w => (Guid?)w.Id)
                .FirstOrDefaultAsync();
        }

        // PUT: api/RawMaterials/5
        [HttpPut("{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> UpdateRawMaterial(
            Guid id,
            RawMaterial rawMaterial,
            CancellationToken ct)
        {
            if (id != rawMaterial.Id)
            {
                return BadRequest();
            }

            // Fetch existing record first
            var existingMaterial = await _context.RawMaterials.FindAsync(new object[] { id }, ct);
            if (existingMaterial == null)
            {
                return NotFound();
            }

            // Validation: Name required
            if (string.IsNullOrWhiteSpace(rawMaterial.Name))
                return BadRequest("Name is required");
            if (rawMaterial.CostPerUnit < 0m)
                return BadRequest("Cost per unit cannot be negative.");

            // Duplicate EN name check (exclude self)
            var duplicateEn = await _context.RawMaterials
                .AnyAsync(m => m.Id != id && m.Name.ToLower() == rawMaterial.Name.Trim().ToLower());
            if (duplicateEn)
                return Conflict(new { message = "A raw material with this English name already exists" });

            // Duplicate AR name check (if provided, exclude self)
            if (!string.IsNullOrWhiteSpace(rawMaterial.NameAr))
            {
                var duplicateAr = await _context.RawMaterials
                    .AnyAsync(m => m.Id != id && m.NameAr != null && m.NameAr.ToLower() == rawMaterial.NameAr.Trim().ToLower());
                if (duplicateAr)
                    return Conflict(new { message = "A raw material with this Arabic name already exists" });
            }

            // Update only the fields that should change
            var branchContext = await _branchContext.GetCurrentAsync(ct);
            var branchId = branchContext.CurrentBranch.Id;
            var previousCostPerUnit = existingMaterial.CostPerUnit;
            existingMaterial.Name = rawMaterial.Name;
            existingMaterial.NameAr = rawMaterial.NameAr;
            existingMaterial.Unit = rawMaterial.Unit;
            if (branchContext.CurrentBranch.IsMainBranch)
            {
                existingMaterial.CurrentStock = rawMaterial.CurrentStock;
                existingMaterial.CostPerUnit = rawMaterial.CostPerUnit;
            }
            existingMaterial.MinimumAlertLevel = rawMaterial.MinimumAlertLevel;
            existingMaterial.ShowInMenu = rawMaterial.ShowInMenu;
            existingMaterial.IsPostPrice = rawMaterial.IsPostPrice;

            // Warehouse reassignment is additive: we never delete the old
            // RawMaterialInventory row (its quantity/history is preserved) but
            // ensure a row exists at the newly assigned warehouse so transfers
            // and stock reports light up immediately.
            var resolvedWarehouseId = await ResolveTargetWarehouseIdAsync(rawMaterial.DefaultWarehouseId, existingMaterial.TenantId);
            existingMaterial.DefaultWarehouseId = resolvedWarehouseId;

            if (resolvedWarehouseId.HasValue)
            {
                var inventoryExists = await _context.RawMaterialInventories
                    .AnyAsync(i => i.RawMaterialId == existingMaterial.Id && i.WarehouseId == resolvedWarehouseId.Value && i.BranchId == branchId, ct);
                if (!inventoryExists)
                {
                    _context.RawMaterialInventories.Add(new RawMaterialInventory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = existingMaterial.TenantId,
                        BranchId = branchId,
                        RawMaterialId = existingMaterial.Id,
                        WarehouseId = resolvedWarehouseId.Value,
                        Quantity = 0m,
                        MinimumQuantity = existingMaterial.MinimumAlertLevel,
                        ReorderLevel = existingMaterial.MinimumAlertLevel
                    });
                }
            }

            try
            {
                if (previousCostPerUnit != existingMaterial.CostPerUnit)
                {
                    await _mediator.Publish(
                        new RawMaterialCostChangedEvent(existingMaterial.Id, existingMaterial.TenantId),
                        ct);
                }

                await _context.SaveChangesAsync(ct);
                await InvalidateMenuVisibilityCachesAsync(existingMaterial.TenantId);
                return Ok();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!RawMaterialExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // DELETE: api/RawMaterials/5
        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> DeleteRawMaterial(Guid id)
        {
            var rawMaterial = await _context.RawMaterials.FindAsync(id);
            if (rawMaterial == null)
            {
                return NotFound();
            }

            _context.RawMaterials.Remove(rawMaterial);
            await _context.SaveChangesAsync();
            await InvalidateMenuVisibilityCachesAsync(rawMaterial.TenantId);

            return NoContent();
        }

        private bool RawMaterialExists(Guid id)
        {
            return _context.RawMaterials.Any(e => e.Id == id);
        }

        private static bool IsArabicRequested(HttpRequest request)
        {
            var language = request.Headers["Accept-Language"].ToString();
            return language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<int> GetNearExpiryDaysAsync()
        {
            return Math.Clamp(
                await _context.SystemSettings
                    .AsNoTracking()
                    .Select(s => (int?)s.NearExpiryDays)
                    .FirstOrDefaultAsync() ?? 3,
                1,
                30);
        }

        private async Task InvalidateMenuVisibilityCachesAsync(Guid tenantId)
        {
            await _cache.RemoveAsync(CacheKeys.ProductsAll(tenantId, isArabic: false));
            await _cache.RemoveAsync(CacheKeys.ProductsAll(tenantId, isArabic: true));
            await _cache.RemoveAsync(CacheKeys.ProductsActive(tenantId, isArabic: false));
            await _cache.RemoveAsync(CacheKeys.ProductsActive(tenantId, isArabic: true));
            await _cache.RemoveAsync(CacheKeys.ProductsPublic(tenantId, isArabic: false));
            await _cache.RemoveAsync(CacheKeys.ProductsPublic(tenantId, isArabic: true));
            await _cache.RemoveByPatternAsync($"pos:menu:*:{tenantId}:*");
        }

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct = default)
            => (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

        public class RawMaterialResponseDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string? NameAr { get; set; }
            public string DisplayName { get; set; }
            public Unit Unit { get; set; }
            public decimal CurrentStock { get; set; }
            public decimal MinimumAlertLevel { get; set; }
            public decimal CostPerUnit { get; set; }
            public bool ShowInMenu { get; set; }
            public bool IsPostPrice { get; set; }
            public DateTime? NextExpiryDate { get; set; }
            public bool HasExpiredBatch { get; set; }
            public bool HasNearExpiryBatch { get; set; }
            public Guid? DefaultWarehouseId { get; set; }
            public string? DefaultWarehouseName { get; set; }
            public string? DefaultWarehouseCode { get; set; }
        }
    }
}
