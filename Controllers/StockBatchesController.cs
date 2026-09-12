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
    [Route("api/stock-batches")]
    [ApiController]
    public class StockBatchesController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly IInventoryService _inventory;
        private readonly IBranchContext _branchContext;

        public StockBatchesController(
            PosDbContext context,
            IInventoryService inventory,
            IBranchContext branchContext)
        {
            _context = context;
            _inventory = inventory;
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        }

        // POST /api/stock-batches
        [HttpPost]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<StockBatchDto>> CreateBatch(
            [FromBody] StockBatchCreateDto dto,
            CancellationToken ct)
        {
            // Validation
            if (dto.Quantity <= 0)
                return BadRequest("Quantity must be greater than 0.");

            if (dto.UnitCost.HasValue && dto.UnitCost <= 0)
                return BadRequest("Unit cost must be greater than 0.");

            var branchId = await GetCurrentBranchIdAsync(ct);
            var material = await _context.RawMaterials.FirstOrDefaultAsync(m => m.Id == dto.MaterialId, ct);
            if (material == null)
                return NotFound("Material not found.");

            var expiryUtc = NormalizeToUtc(dto.ExpiryDate);
            decimal unitCostValue = dto.UnitCost ?? material.CostPerUnit;

            var batch = new StockBatch
            {
                Id = Guid.NewGuid(),
                TenantId = material.TenantId,
                BranchId = branchId,
                MaterialId = dto.MaterialId,
                PurchaseOrderId = dto.PurchaseOrderId,
                BatchNumber = dto.BatchNumber,
                Quantity = dto.Quantity,
                RemainingQuantity = dto.Quantity,
                ExpiryDate = expiryUtc,
                UnitCost = unitCostValue,
                TotalCost = dto.Quantity * unitCostValue,
                CreatedAt = DateTime.UtcNow,
                IsApproved = true,
                NearExpiryNotified = false,
                ExpiredWasteLogged = false,
            };

            _context.StockBatches.Add(batch);
            await _context.SaveChangesAsync(ct);

            // FIFO costing: refresh CurrentStock + CostPerUnit from the live batch state.
            // CostPerUnit reflects the next-FIFO-batch UnitCost — never a weighted average.
            await _inventory.RefreshMaterialFifoCostAsync(material.Id, ct);
            await _context.SaveChangesAsync(ct);

            return Ok(new StockBatchDto
            {
                Id = batch.Id,
                MaterialId = batch.MaterialId,
                MaterialName = material.Name,
                Quantity = batch.Quantity,
                RemainingQuantity = batch.RemainingQuantity,
                UnitCost = batch.UnitCost,
                TotalCost = batch.TotalCost,
                ExpiryDate = batch.ExpiryDate,
                PurchaseOrderId = batch.PurchaseOrderId,
                BatchNumber = batch.BatchNumber,
                CreatedAt = batch.CreatedAt,
                IsApproved = batch.IsApproved,
                Status = batch.Status.ToString()
            });
        }

        // PUT /api/stock-batches/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> UpdateBatch(
            Guid id,
            [FromBody] StockBatchUpdateDto dto,
            CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var batch = await _context.StockBatches
                .Include(sb => sb.RawMaterial)
                .FirstOrDefaultAsync(sb => sb.Id == id && sb.BranchId == branchId, ct);

            if (batch == null)
                return NotFound();

            var quantityDiff = dto.Quantity - batch.Quantity;
            var previousExpiryDate = batch.ExpiryDate;
            var expiryUtc = NormalizeToUtc(dto.ExpiryDate);
            var hadFutureExpiry = previousExpiryDate >= DateTime.UtcNow.Date;
            var hasFutureExpiry = expiryUtc >= DateTime.UtcNow.Date;

            batch.Quantity = dto.Quantity;
            batch.BatchNumber = dto.BatchNumber;
            batch.ExpiryDate = expiryUtc;
            batch.RemainingQuantity = Math.Max(0, batch.RemainingQuantity + quantityDiff);
            batch.TotalCost = batch.Quantity * batch.UnitCost;

            if (quantityDiff > 0 || (!hadFutureExpiry && hasFutureExpiry) || expiryUtc > previousExpiryDate)
            {
                batch.NearExpiryNotified = false;
            }

            if (hasFutureExpiry)
            {
                batch.ExpiredWasteLogged = false;
            }

            // Recalculate status based on new values
            if (batch.RemainingQuantity == 0)
                batch.Status = BatchStatus.Finished;
            else if (batch.ExpiryDate < DateTime.UtcNow.Date)
                batch.Status = BatchStatus.Expired;
            else
                batch.Status = BatchStatus.Good;

            // FIFO costing: refresh CurrentStock + CostPerUnit (head of FIFO queue).
            await _context.SaveChangesAsync(ct);
            await _inventory.RefreshMaterialFifoCostAsync(batch.MaterialId, ct);
            await _context.SaveChangesAsync(ct);
            return Ok();
        }

        // DELETE /api/stock-batches/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> DeleteBatch(Guid id, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var batch = await _context.StockBatches
                .Include(sb => sb.RawMaterial)
                .FirstOrDefaultAsync(sb => sb.Id == id && sb.BranchId == branchId, ct);

            if (batch == null)
                return NotFound();

            var materialId = batch.MaterialId;
            _context.StockBatches.Remove(batch);
            await _context.SaveChangesAsync(ct);

            // FIFO costing: refresh CurrentStock + CostPerUnit after removing the batch.
            await _inventory.RefreshMaterialFifoCostAsync(materialId, ct);
            await _context.SaveChangesAsync(ct);

            return NoContent();
        }

        /// <summary>
        /// Treat the date as-is (no timezone shift). Expiry dates are date-only values.
        /// </summary>
        private static DateTime NormalizeToUtc(DateTime input)
        {
            // For date-only fields (ExpiryDate), preserve the date exactly as provided.
            // Never convert Local→UTC which would shift the date backward in UTC+ timezones.
            return DateTime.SpecifyKind(input.Date, DateTimeKind.Utc);
        }

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
            => (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
    }
}
