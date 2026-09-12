using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class BonusRepository : IBonusRepository
    {
        private readonly PosDbContext _context;

        public BonusRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<BonusListResult> GetAsync(BonusListFilter filter, CancellationToken ct)
        {
            var query = _context.Bonuses.AsNoTracking();

            if (filter.StaffProfileId.HasValue) query = query.Where(b => b.StaffProfileId == filter.StaffProfileId.Value);
            if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(b => b.Status == filter.Status);
            if (!string.IsNullOrWhiteSpace(filter.BonusType)) query = query.Where(b => b.BonusType == filter.BonusType);
            if (filter.From.HasValue) query = query.Where(b => b.AwardDate >= filter.From.Value);
            if (filter.To.HasValue) query = query.Where(b => b.AwardDate <= filter.To.Value);

            var total = await query.CountAsync(ct);
            var totalAmount = await query.SumAsync(b => (decimal?)b.Amount, ct) ?? 0m;

            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize < 1 ? 25 : Math.Min(filter.PageSize, 200);

            var items = await query
                .Include(b => b.StaffProfile)
                    .ThenInclude(sp => sp.User)
                .OrderByDescending(b => b.AwardDate)
                .ThenByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new BonusListResult(items, total, totalAmount);
        }

        public Task<Bonus?> GetByIdAsync(Guid id, CancellationToken ct)
            => _context.Bonuses
                .Include(b => b.StaffProfile)
                    .ThenInclude(sp => sp.User)
                .FirstOrDefaultAsync(b => b.Id == id, ct);

        public async Task<Bonus> AddAsync(Bonus bonus, CancellationToken ct)
        {
            _context.Bonuses.Add(bonus);
            await _context.SaveChangesAsync(ct);
            return bonus;
        }

        public Task UpdateAsync(Bonus bonus, CancellationToken ct)
            => _context.SaveChangesAsync(ct);

        public Task<int> CountPendingForStaffAsync(Guid staffProfileId, CancellationToken ct)
            => _context.Bonuses
                .AsNoTracking()
                .Where(b => b.StaffProfileId == staffProfileId && b.Status == HrBonusStatuses.Pending)
                .CountAsync(ct);

        public async Task<IReadOnlyDictionary<Guid, int>> CountPendingByStaffAsync(
            IReadOnlyList<Guid> staffProfileIds, CancellationToken ct)
        {
            if (staffProfileIds.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            var rows = await _context.Bonuses
                .AsNoTracking()
                .Where(b => staffProfileIds.Contains(b.StaffProfileId) && b.Status == HrBonusStatuses.Pending)
                .GroupBy(b => b.StaffProfileId)
                .Select(g => new { StaffProfileId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            return rows.ToDictionary(r => r.StaffProfileId, r => r.Count);
        }

        public async Task<IReadOnlyDictionary<Guid, (int count, decimal total)>> GetTotalsByStaffAsync(
            IReadOnlyList<Guid> staffProfileIds, CancellationToken ct)
        {
            if (staffProfileIds.Count == 0)
            {
                return new Dictionary<Guid, (int, decimal)>();
            }

            var rows = await _context.Bonuses
                .AsNoTracking()
                .Where(b => staffProfileIds.Contains(b.StaffProfileId)
                            && b.Status != HrBonusStatuses.Cancelled)
                .GroupBy(b => b.StaffProfileId)
                .Select(g => new
                {
                    StaffProfileId = g.Key,
                    Count = g.Count(),
                    Total = g.Sum(x => (decimal?)x.Amount) ?? 0m
                })
                .ToListAsync(ct);

            return rows.ToDictionary(r => r.StaffProfileId, r => (r.Count, r.Total));
        }
    }
}
