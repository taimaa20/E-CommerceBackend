using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class TableCategoryRepository : ITableCategoryRepository
    {
        private readonly PosDbContext _context;

        public TableCategoryRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<List<TableCategoryDto>> GetAllProjectedAsync(bool isArabic, Guid branchId, CancellationToken cancellationToken = default)
        {
            // Server-side projection — drop tracking, drop the Tables nav graph,
            // and let the DB count the bridge rows.
            return _context.TableCategories
                .AsNoTracking()
                .Where(c => c.BranchId == branchId)
                .OrderBy(c => c.Name)
                .Select(c => new TableCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    NameAr = c.NameAr,
                    DisplayName = isArabic && c.NameAr != null && c.NameAr != string.Empty ? c.NameAr : c.Name,
                    Color = c.Color,
                    TableCount = c.Tables.Count()
                })
                .ToListAsync(cancellationToken);
        }

        public Task<bool> NameExistsAsync(string name, Guid branchId, Guid? excludeId, CancellationToken cancellationToken = default)
        {
            var normalized = name.Trim().ToLower();
            var query = _context.TableCategories
                .AsNoTracking()
                .Where(c => c.BranchId == branchId && c.Name.ToLower() == normalized);

            if (excludeId.HasValue)
                query = query.Where(c => c.Id != excludeId.Value);

            return query.AnyAsync(cancellationToken);
        }

        public Task<TableCategory?> GetByIdAsync(Guid id, Guid branchId, CancellationToken cancellationToken = default)
        {
            return _context.TableCategories.FirstOrDefaultAsync(c => c.Id == id && c.BranchId == branchId, cancellationToken);
        }

        public Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return _context.Branches
                .Where(b => b.TenantId == tenantId && b.IsMainBranch)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task AddAsync(TableCategory category, CancellationToken cancellationToken = default)
        {
            _context.TableCategories.Add(category);
            return Task.CompletedTask;
        }

        public async Task UnlinkTablesAsync(Guid categoryId, CancellationToken cancellationToken = default)
        {
            // FK is SetNull at the DB level, but we clear explicitly so the EF
            // change tracker reflects reality before SaveChanges.
            var tables = await _context.Tables
                .Where(t => t.TableCategoryId == categoryId)
                .ToListAsync(cancellationToken);

            foreach (var t in tables)
                t.TableCategoryId = null;
        }

        public Task RemoveAsync(TableCategory category, CancellationToken cancellationToken = default)
        {
            _context.TableCategories.Remove(category);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TableCategory category, CancellationToken cancellationToken = default)
        {
            _context.TableCategories.Update(category);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
