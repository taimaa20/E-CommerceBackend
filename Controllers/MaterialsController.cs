using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/materials")]
    [ApiController]
    public class MaterialsController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly IBranchContext _branchContext;

        public MaterialsController(PosDbContext context, IBranchContext branchContext)
        {
            _context = context;
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        }

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
            => (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

        // GET /api/materials/{materialId}/batches?filter=all|active|expired|finished
        // filter is optional. Omitted/"all" preserves legacy behavior (Good + Expired, hides Finished).
        [HttpGet("{materialId}/batches")]
        public async Task<ActionResult<List<StockBatchDto>>> GetMaterialBatches(
            Guid materialId,
            [FromQuery] string? filter = null,
            CancellationToken ct = default)
        {
            var exists = await _context.RawMaterials.AnyAsync(m => m.Id == materialId, ct);
            if (!exists) return NotFound();

            var today = DateTime.UtcNow.Date;
            var branchId = await GetCurrentBranchIdAsync(ct);

            // Base query — tenant + branch + approved + PO-approved guard. FIFO layers are
            // branch-owned; the list must never mix another branch's batches.
            var baseQuery = _context.StockBatches
                .Where(sb => sb.MaterialId == materialId
                           && sb.BranchId == branchId
                           && sb.IsApproved
                           && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved));

            var normalized = (filter ?? "all").Trim().ToLowerInvariant();
            IQueryable<StockBatch> filtered = normalized switch
            {
                "active" => baseQuery
                    .Where(sb => sb.Status == BatchStatus.Good
                              && sb.RemainingQuantity > 0
                              && sb.ExpiryDate >= today)
                    .OrderBy(sb => sb.ExpiryDate)
                    .ThenBy(sb => sb.CreatedAt),

                "expired" => baseQuery
                    .Where(sb => sb.ExpiryDate < today || sb.Status == BatchStatus.Expired)
                    .OrderByDescending(sb => sb.RemainingQuantity > 0) // expired-with-stock first
                    .ThenBy(sb => sb.ExpiryDate),

                "finished" => baseQuery
                    .Where(sb => sb.Status == BatchStatus.Finished || sb.RemainingQuantity == 0)
                    .OrderByDescending(sb => sb.UpdatedAt)
                    .Take(5),

                _ => baseQuery
                    .Where(sb => sb.Status != BatchStatus.Finished) // legacy default
                    .OrderBy(sb => sb.ExpiryDate)
                    .ThenBy(sb => sb.CreatedAt)
            };

            var batches = await filtered
                .Select(sb => new StockBatchDto
                {
                    Id = sb.Id,
                    MaterialId = sb.MaterialId,
                    MaterialName = sb.RawMaterial.Name,
                    MaterialNameAr = sb.RawMaterial.NameAr,
                    Quantity = sb.Quantity,
                    RemainingQuantity = sb.RemainingQuantity,
                    UnitCost = sb.UnitCost,
                    TotalCost = sb.TotalCost,
                    ExpiryDate = sb.ExpiryDate,
                    PurchaseOrderId = sb.PurchaseOrderId,
                    PurchaseOrderNumber = sb.PurchaseOrderNumber ?? (sb.PurchaseOrder != null ? sb.PurchaseOrder.OrderNumber : null),
                    BatchNumber = sb.BatchNumber,
                    CreatedAt = sb.CreatedAt,
                    IsApproved = sb.IsApproved,
                    Status = sb.Status.ToString(),
                    SupplierName = sb.PurchaseOrder != null && sb.PurchaseOrder.Supplier != null ? sb.PurchaseOrder.Supplier.Name : null,
                    SupplierNameAr = sb.PurchaseOrder != null && sb.PurchaseOrder.Supplier != null ? sb.PurchaseOrder.Supplier.NameAr : null,
                    FinishedAt = (sb.Status == BatchStatus.Finished || sb.RemainingQuantity == 0) ? (DateTime?)sb.UpdatedAt : null
                })
                .ToListAsync(ct);

            return Ok(batches);
        }

        /// <summary>
        /// Returns expiry alerts for admin: expired batches and batches expiring within N days.
        /// </summary>
        [HttpGet("expiry-alerts")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult> GetExpiryAlerts(
            [FromQuery] int daysThreshold = 3,
            CancellationToken ct = default)
        {
            var today = DateTime.UtcNow.Date;
            var thresholdDate = today.AddDays(daysThreshold);
            var mainBranchId = await _context.Branches
                .AsNoTracking()
                .Where(branch => branch.IsMainBranch)
                .Select(branch => (Guid?)branch.Id)
                .FirstOrDefaultAsync(ct);

            // Mark any Good batches that have expired
            var newlyExpired = await _context.StockBatches
                .Where(b => b.IsApproved
                         && (b.PurchaseOrderId == null || b.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                         && b.Status == BatchStatus.Good
                         && b.ExpiryDate < today)
                .ToListAsync(ct);

            foreach (var batch in newlyExpired)
            {
                batch.Status = BatchStatus.Expired;
            }

            if (newlyExpired.Any())
            {
                await _context.SaveChangesAsync(ct);
            }

            // Fetch expired batches (with remaining stock) for the active branch only.
            var currentBranchId = await GetCurrentBranchIdAsync(ct);
            var expiredBatches = await _context.StockBatches
                .Include(b => b.RawMaterial)
                .Where(b => b.BranchId == currentBranchId
                         && b.IsApproved
                         && (b.PurchaseOrderId == null || b.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                         && b.Status == BatchStatus.Expired
                         && b.RemainingQuantity > 0)
                .OrderBy(b => b.ExpiryDate)
                .Select(b => new
                {
                    b.Id,
                    b.BatchNumber,
                    MaterialName = b.RawMaterial.Name,
                    MaterialNameAr = b.RawMaterial.NameAr,
                    b.RemainingQuantity,
                    b.ExpiryDate,
                    Status = "Expired"
                })
                .ToListAsync(ct);

            // Fetch batches expiring soon (Good, expiry <= threshold) for the active branch only.
            var expiringSoon = await _context.StockBatches
                .Include(b => b.RawMaterial)
                .Where(b => b.BranchId == currentBranchId
                         && b.IsApproved
                         && (b.PurchaseOrderId == null || b.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                         && b.Status == BatchStatus.Good
                         && b.ExpiryDate >= today
                         && b.ExpiryDate <= thresholdDate
                         && b.RemainingQuantity > 0)
                .OrderBy(b => b.ExpiryDate)
                .Select(b => new
                {
                    b.Id,
                    b.BatchNumber,
                    MaterialName = b.RawMaterial.Name,
                    MaterialNameAr = b.RawMaterial.NameAr,
                    b.RemainingQuantity,
                    b.ExpiryDate,
                    Status = "ExpiringSoon"
                })
                .ToListAsync(ct);

            // Recompute CurrentStock for affected materials
            var affectedMaterialIds = mainBranchId.HasValue
                ? newlyExpired
                    .Where(b => b.BranchId == mainBranchId.Value)
                    .Select(b => b.MaterialId)
                    .Distinct()
                    .ToList()
                : new List<Guid>();
            if (affectedMaterialIds.Any())
            {
                var mainBranchIdValue = mainBranchId.Value;
                var materials = await _context.RawMaterials
                    .Where(m => affectedMaterialIds.Contains(m.Id))
                    .ToListAsync(ct);

                foreach (var material in materials)
                {
                    material.CurrentStock = await _context.StockBatches
                        .Where(b => b.MaterialId == material.Id
                                 && b.BranchId == mainBranchIdValue
                                 && b.IsApproved
                                 && (b.PurchaseOrderId == null || b.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                 && b.Status == BatchStatus.Good)
                        .SumAsync(b => b.RemainingQuantity, ct);
                }

                await _context.SaveChangesAsync(ct);
            }

            return Ok(new
            {
                ExpiredCount = expiredBatches.Count,
                ExpiringSoonCount = expiringSoon.Count,
                Expired = expiredBatches,
                ExpiringSoon = expiringSoon
            });
        }
    }
}
