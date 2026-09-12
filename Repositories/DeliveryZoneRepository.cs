using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Repositories
{
    public class DeliveryZoneRepository : IDeliveryZoneRepository
    {
        private readonly PosDbContext _context;

        public DeliveryZoneRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<DeliveryZone>> GetActiveAsync(Guid branchId, CancellationToken ct = default)
        {
            return await _context.DeliveryZones
                .AsNoTracking()
                .Where(z => z.BranchId == branchId && z.IsActive)
                .OrderBy(z => z.DisplayOrder)
                .ThenBy(z => z.Name)
                .ToListAsync(ct);
        }

        public async Task<(List<DeliveryZone> Items, int TotalCount)> GetPagedAsync(
            Guid branchId,
            string? search,
            bool? isActive,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.DeliveryZones.AsNoTracking()
                .Where(z => z.BranchId == branchId);

            if (isActive.HasValue)
            {
                query = query.Where(z => z.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                // Case-insensitive contains across name (en/ar) + code; translated to SQL ILIKE by Npgsql.
                query = query.Where(z =>
                    EF.Functions.ILike(z.Name, $"%{term}%") ||
                    (z.NameAr != null && EF.Functions.ILike(z.NameAr, $"%{term}%")) ||
                    EF.Functions.ILike(z.Code, $"%{term}%"));
            }

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .OrderBy(z => z.DisplayOrder)
                .ThenBy(z => z.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }

        public async Task<DeliveryZone?> GetByIdAsync(Guid id, Guid branchId, CancellationToken ct = default)
        {
            return await _context.DeliveryZones
                .AsNoTracking()
                .FirstOrDefaultAsync(z => z.Id == id && z.BranchId == branchId, ct);
        }

        public Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct = default)
        {
            return _context.Branches
                .Where(b => b.TenantId == tenantId && b.IsMainBranch)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<bool> CodeExistsAsync(string code, Guid branchId, Guid? excludeId, CancellationToken ct = default)
        {
            var normalized = code.Trim();
            return await _context.DeliveryZones
                .AsNoTracking()
                .AnyAsync(z =>
                    z.BranchId == branchId &&
                    z.Code.ToLower() == normalized.ToLower() &&
                    (!excludeId.HasValue || z.Id != excludeId.Value), ct);
        }

        public async Task<DeliveryZone> AddAsync(DeliveryZone zone, CancellationToken ct = default)
        {
            _context.DeliveryZones.Add(zone);
            await _context.SaveChangesAsync(ct);
            return zone;
        }

        public async Task<DeliveryZone> UpdateAsync(DeliveryZone zone, CancellationToken ct = default)
        {
            _context.DeliveryZones.Update(zone);
            await _context.SaveChangesAsync(ct);
            return zone;
        }

        public async Task DeleteAsync(DeliveryZone zone, CancellationToken ct = default)
        {
            // Soft delete: keep the row so order snapshots' FK target survives; query filter hides it.
            zone.DeletedAt = DateTime.UtcNow;
            zone.IsActive = false;
            _context.DeliveryZones.Update(zone);
            await _context.SaveChangesAsync(ct);
        }
    }
}
