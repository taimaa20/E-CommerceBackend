using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TablesController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchContext _branchContext;

        public TablesController(
            PosDbContext context,
            ITenantResolver tenantResolver,
            IBranchContext branchContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        }

        // GET: api/Tables
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetTables(CancellationToken ct)
        {
            var branchId = await ResolveBranchIdForReadAsync(ct);

            var isArabic = IsArabicRequested(Request);
            var tables = await _context.Tables
                .AsNoTracking()
                .Where(t => t.BranchId == branchId)
                .Include(t => t.Category)
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.NameAr,
                    DisplayName = isArabic && t.NameAr != null && t.NameAr != string.Empty ? t.NameAr : t.Name,
                    t.Capacity,
                    t.Status,
                    // Surfaced so the table grid can filter historical orders to the
                    // active session when enriching Occupied/Reserved status. Without
                    // this, the frontend counts orders from already-cleared sessions.
                    t.CurrentTicketId,
                    t.TableCategoryId,
                    CategoryName = t.Category != null ? (isArabic && t.Category.NameAr != null ? t.Category.NameAr : t.Category.Name) : null,
                    CategoryColor = t.Category != null ? t.Category.Color : null
                })
                .ToListAsync(ct);

            return Ok(tables);
        }

        // GET: api/Tables/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetTable(Guid id, CancellationToken ct)
        {
            var branchId = await ResolveBranchIdForReadAsync(ct);

            var isArabic = IsArabicRequested(Request);
            var table = await _context.Tables
                .FirstOrDefaultAsync(t => t.Id == id && t.BranchId == branchId, ct);

            if (table == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                table.Id,
                table.Name,
                table.NameAr,
                DisplayName = ResolveDisplayName(table.Name, table.NameAr, isArabic),
                table.Capacity,
                table.Status
            });
        }

        // GET: api/Tables/{id}/public
        [HttpGet("{id}/public")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetTablePublic(Guid id, CancellationToken ct)
        {
            var isArabic = IsArabicRequested(Request);
            var table = await _context.Tables.FindAsync(new object[] { id }, ct);

            if (table == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                table.Id,
                table.Name,
                table.NameAr,
                DisplayName = ResolveDisplayName(table.Name, table.NameAr, isArabic),
                table.Capacity,
                table.Status,
                table.CurrentTicketId,
                table.NextTicketId
            });
        }

        // POST: api/Tables
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Roles = AppRoleNames.Admin)]
        public async Task<ActionResult<Table>> CreateTable(Table table, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);

            // Validation: Name required
            if (string.IsNullOrWhiteSpace(table.Name))
                return BadRequest("Table name is required");

            // Duplicate EN name check (case-insensitive, scoped to the current branch)
            var duplicateEn = await _context.Tables
                .AnyAsync(t => t.BranchId == branchId && t.Name.ToLower() == table.Name.Trim().ToLower(), ct);
            if (duplicateEn)
                return Conflict(new { message = "A table with this English name already exists" });

            // Duplicate AR name check (if provided)
            if (!string.IsNullOrWhiteSpace(table.NameAr))
            {
                var duplicateAr = await _context.Tables
                    .AnyAsync(t => t.BranchId == branchId && t.NameAr != null && t.NameAr.ToLower() == table.NameAr.Trim().ToLower(), ct);
                if (duplicateAr)
                    return Conflict(new { message = "A table with this Arabic name already exists" });
            }

            table.Id = Guid.NewGuid();
            table.TenantId = _tenantResolver.GetTenantId();
            table.BranchId = branchId;

            // Validate category exists in the current branch if provided
            if (table.TableCategoryId.HasValue)
            {
                var catExists = await _context.TableCategories
                    .AnyAsync(c => c.Id == table.TableCategoryId.Value && c.BranchId == branchId, ct);
                if (!catExists)
                    table.TableCategoryId = null;
            }

            _context.Tables.Add(table);
            await _context.SaveChangesAsync(ct);

            await _context.Entry(table).Reference(t => t.Category).LoadAsync(ct);
            var isArabic = IsArabicRequested(Request);
            return CreatedAtAction("GetTable", new { id = table.Id }, new
            {
                table.Id,
                table.Name,
                table.NameAr,
                DisplayName = ResolveDisplayName(table.Name, table.NameAr, isArabic),
                table.Capacity,
                table.Status,
                table.TableCategoryId,
                CategoryName = table.Category != null ? (isArabic && table.Category.NameAr != null ? table.Category.NameAr : table.Category.Name) : null,
                CategoryColor = table.Category?.Color
            });
        }

        // PUT: api/Tables/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = AppRoleNames.Admin)]
        public async Task<IActionResult> UpdateTable(Guid id, [FromBody] UpdateTableRequest request, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);

            // Validation
            if (request == null)
                return BadRequest("Request body is required.");

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Table name is required.");

            if (request.Capacity < 1)
                return BadRequest("Capacity must be at least 1.");

            var table = await _context.Tables
                .FirstOrDefaultAsync(t => t.Id == id && t.BranchId == branchId, ct);
            if (table == null)
                return NotFound();

            // Check for duplicate names (excluding current table, scoped to the current branch)
            var duplicate = await _context.Tables
                .AnyAsync(t => t.BranchId == branchId && t.Id != id && t.Name.ToLower() == request.Name.Trim().ToLower(), ct);
            if (duplicate)
                return Conflict(new { message = "A table with this name already exists." });

            // Check for duplicate Arabic names if provided
            if (!string.IsNullOrWhiteSpace(request.NameAr))
            {
                var duplicateAr = await _context.Tables
                    .AnyAsync(t => t.BranchId == branchId && t.Id != id && t.NameAr != null && t.NameAr.ToLower() == request.NameAr.Trim().ToLower(), ct);
                if (duplicateAr)
                    return Conflict(new { message = "A table with this Arabic name already exists." });
            }

            // Update table properties
            table.Name = request.Name.Trim();
            table.NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim();
            table.Capacity = request.Capacity;

            // Update category
            if (request.TableCategoryId.HasValue && request.TableCategoryId != Guid.Empty)
            {
                var catExists = await _context.TableCategories
                    .AnyAsync(c => c.Id == request.TableCategoryId.Value && c.BranchId == branchId, ct);
                table.TableCategoryId = catExists ? request.TableCategoryId : null;
            }
            else
            {
                table.TableCategoryId = null;
            }

            // Save changes
            _context.Tables.Update(table);
            await _context.SaveChangesAsync(ct);

            // Load related data for response
            await _context.Entry(table).Reference(t => t.Category).LoadAsync(ct);
            var isArabic = IsArabicRequested(Request);
            
            return Ok(new
            {
                table.Id,
                table.Name,
                table.NameAr,
                DisplayName = ResolveDisplayName(table.Name, table.NameAr, isArabic),
                table.Capacity,
                table.Status,
                table.TableCategoryId,
                CategoryName = table.Category != null ? (isArabic && table.Category.NameAr != null ? table.Category.NameAr : table.Category.Name) : null,
                CategoryColor = table.Category?.Color
            });
        }

        // DELETE: api/Tables/5
        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoleNames.Admin)]
        public async Task<IActionResult> DeleteTable(Guid id, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);

            var table = await _context.Tables
                .FirstOrDefaultAsync(t => t.Id == id && t.BranchId == branchId, ct);
            if (table == null)
            {
                return NotFound();
            }

            _context.Tables.Remove(table);
            await _context.SaveChangesAsync(ct);

            return NoContent();
        }
        
        // PUT: api/Tables/{id}/status
        [HttpPut("{id}/status")]
        [Authorize(Roles = AppRoleGroups.DineInTableOperators)]
        public async Task<IActionResult> UpdateTableStatus(Guid id, [FromBody] TableStatus status, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);

            var table = await _context.Tables
                .FirstOrDefaultAsync(t => t.Id == id && t.BranchId == branchId, ct);
            if (table == null)
            {
                return NotFound();
            }

            table.Status = status;
            await _context.SaveChangesAsync(ct);
        
            return Ok();
        }
        // PUT: api/Tables/{id}/reset
        // Reset table status to Free and end the session
        // NextTicketId persists (global counter never resets)
        // CurrentTicketId is cleared (ends session) so next use creates new ticket
        [HttpPut("{id}/reset")]
        [Authorize(Roles = AppRoleGroups.DineInTableOperators)]
        public async Task<IActionResult> ResetTable(Guid id, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);

            var table = await _context.Tables
                .FirstOrDefaultAsync(t => t.Id == id && t.BranchId == branchId, ct);
            if (table == null)
            {
                return NotFound();
            }

            // Reset table status to Free and end current session
            table.Status = TableStatus.Free;
            table.CurrentTicketId = null; // End session - next click creates new ticket
            
            await _context.SaveChangesAsync(ct);

            return Ok();
        }

        private static bool IsArabicRequested(HttpRequest request)
        {
            var language = request.Headers["Accept-Language"].ToString();
            return language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDisplayName(string name, string? nameAr, bool isArabic)
        {
            return isArabic && !string.IsNullOrWhiteSpace(nameAr) ? nameAr : name;
        }

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
        {
            var context = await _branchContext.GetCurrentAsync(ct);
            return context.CurrentBranch.Id;
        }

        // GetTables/GetTable have no [Authorize] (pre-existing, tracked separately in
        // SECURITY_AUDIT.md H4) so an unauthenticated caller can reach them. Resolving
        // IBranchContext for such a caller throws, so fall back to the tenant's Main
        // Branch — the same branch every table implicitly belonged to before this
        // module was branch-scoped, so anonymous reads keep their pre-existing shape.
        private async Task<Guid> ResolveBranchIdForReadAsync(CancellationToken ct)
        {
            if (User.Identity?.IsAuthenticated == true)
                return await GetCurrentBranchIdAsync(ct);

            var tenantId = _tenantResolver.GetTenantId();
            var mainBranchId = await _context.Branches
                .Where(b => b.TenantId == tenantId && b.IsMainBranch)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(ct);

            return mainBranchId ?? Guid.Empty;
        }
    }
}
