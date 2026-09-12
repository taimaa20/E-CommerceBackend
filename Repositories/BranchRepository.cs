using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class BranchRepository : IBranchRepository
    {
        private readonly PosDbContext _context;

        public BranchRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<(List<Branch> Items, int TotalCount)> GetPagedAsync(
            string? search,
            bool? isActive,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.Branches.AsNoTracking();

            if (isActive.HasValue)
                query = query.Where(b => b.IsActive == isActive.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b =>
                    EF.Functions.ILike(b.Name, $"%{term}%") ||
                    (b.NameAr != null && EF.Functions.ILike(b.NameAr, $"%{term}%")) ||
                    EF.Functions.ILike(b.Code, $"%{term}%"));
            }

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(b => b.IsMainBranch)
                .ThenByDescending(b => b.IsActive)
                .ThenBy(b => b.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }

        public Task<Branch?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);

        public Task<Branch?> GetTrackedAsync(Guid id, CancellationToken ct = default)
            => _context.Branches.FirstOrDefaultAsync(b => b.Id == id, ct);

        public Task<Branch?> GetMainAsync(CancellationToken ct = default)
            => _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.IsMainBranch, ct);

        public Task<bool> AnyAsync(CancellationToken ct = default)
            => _context.Branches.AsNoTracking().AnyAsync(ct);

        public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct = default)
            => _context.Branches
                .AsNoTracking()
                .AnyAsync(b => b.Code == code
                    && (!excludeId.HasValue || b.Id != excludeId.Value), ct);

        public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken ct = default)
            => _context.Branches
                .AsNoTracking()
                .AnyAsync(b => b.Name == name
                    && (!excludeId.HasValue || b.Id != excludeId.Value), ct);

        public async Task<Branch> AddAsync(Branch branch, bool clearExistingMain, Guid? updatedById, CancellationToken ct = default)
        {
            if (!clearExistingMain)
            {
                _context.Branches.Add(branch);
                await _context.SaveChangesAsync(ct);
                return branch;
            }

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await ClearMainAsync(branch.Id, updatedById, ct);
            _context.Branches.Add(branch);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return branch;
        }

        public async Task<Branch> UpdateAsync(Branch branch, bool clearExistingMain, Guid? updatedById, CancellationToken ct = default)
        {
            if (!clearExistingMain)
            {
                _context.Branches.Update(branch);
                await _context.SaveChangesAsync(ct);
                return branch;
            }

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await ClearMainAsync(branch.Id, updatedById, ct);
            _context.Branches.Update(branch);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return branch;
        }

        public async Task DeleteAsync(Branch branch, CancellationToken ct = default)
        {
            branch.DeletedAt = DateTime.UtcNow;
            branch.IsActive = false;
            branch.IsMainBranch = false;
            _context.Branches.Update(branch);
            await _context.SaveChangesAsync(ct);
        }

        private Task ClearMainAsync(Guid excludeId, Guid? updatedById, CancellationToken ct)
            => _context.Branches
                .Where(b => b.IsMainBranch && b.Id != excludeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.IsMainBranch, false)
                    .SetProperty(b => b.UpdatedAt, DateTime.UtcNow)
                    .SetProperty(b => b.UpdatedById, updatedById), ct);
    }
}
