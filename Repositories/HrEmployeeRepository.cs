using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class HrEmployeeRepository : IHrEmployeeRepository
    {
        private readonly PosDbContext _context;

        public HrEmployeeRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<HrEmployeeListResult> GetEmployeesAsync(HrEmployeeListFilter filter, CancellationToken ct)
        {
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize < 1 ? 25 : Math.Min(filter.PageSize, 200);

            // Outer-join from User -> StaffProfile so users without a profile still surface,
            // matching the behavior of the existing Mobile HR repository which auto-creates
            // missing profiles. We don't auto-create here (read-only listing).
            var query =
                from u in _context.Users.AsNoTracking()
                join sp in _context.StaffProfiles.AsNoTracking() on u.Id equals sp.UserId into spJoin
                from sp in spJoin.DefaultIfEmpty()
                select new { u, sp };

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim().ToLower();
                query = query.Where(r =>
                    r.u.Username.ToLower().Contains(s)
                    || (r.u.FullName != null && r.u.FullName.ToLower().Contains(s))
                    || (r.u.FullNameAr != null && r.u.FullNameAr.ToLower().Contains(s))
                    || (r.sp != null && r.sp.StaffNo != null && r.sp.StaffNo.ToLower().Contains(s)));
            }

            if (filter.Role.HasValue)
            {
                var role = (UserRole)filter.Role.Value;
                query = query.Where(r => r.u.Role == role);
            }

            if (filter.ContractStatus.HasValue && filter.ContractStatus.Value >= 0)
            {
                var cs = (ContractStatus)filter.ContractStatus.Value;
                query = query.Where(r => r.sp != null && r.sp.ContractStatus == cs);
            }

            var total = await query.CountAsync(ct);

            var rows = await query
                .OrderBy(r => r.u.FullName ?? r.u.Username)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new HrEmployeeRow(
                    r.sp != null ? r.sp.Id : Guid.Empty,
                    r.u.Id,
                    r.u.Username,
                    r.u.FullName,
                    r.u.FullNameAr,
                    r.u.Role.ToString(),
                    r.sp != null ? r.sp.StaffNo : null,
                    r.sp != null ? r.sp.Phone : null,
                    r.sp != null ? r.sp.PhotoUrl : null,
                    r.sp != null ? (int)r.sp.ContractStatus : 0,
                    r.sp != null ? r.sp.StartDate : null,
                    r.u.MonthlySalary,
                    r.sp != null ? r.sp.NetSalary : 0m))
                .ToListAsync(ct);

            return new HrEmployeeListResult(rows, total);
        }

        public Task<StaffProfile?> GetByIdAsync(Guid staffProfileId, CancellationToken ct)
            => _context.StaffProfiles
                .Include(sp => sp.User)
                .FirstOrDefaultAsync(sp => sp.Id == staffProfileId, ct);

        public Task<StaffProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct)
            => _context.StaffProfiles
                .Include(sp => sp.User)
                .FirstOrDefaultAsync(sp => sp.UserId == userId, ct);

        public Task<int> CountOpenLeaveRequestsAsync(Guid staffProfileId, CancellationToken ct)
            => _context.MobileLeaveRequests
                .AsNoTracking()
                .CountAsync(r => r.StaffProfileId == staffProfileId
                    && r.Status == MobileRequestStatuses.Pending, ct);

        public Task<int> CountOpenLoanRequestsAsync(Guid staffProfileId, CancellationToken ct)
            => _context.MobileLoanRequests
                .AsNoTracking()
                .CountAsync(r => r.StaffProfileId == staffProfileId
                    && r.Status == MobileRequestStatuses.Pending, ct);

        public Task<int> CountOpenPermissionRequestsAsync(Guid staffProfileId, CancellationToken ct)
            => _context.MobilePermissionRequests
                .AsNoTracking()
                .CountAsync(r => r.StaffProfileId == staffProfileId
                    && r.Status == MobileRequestStatuses.Pending, ct);

        public async Task<IReadOnlyDictionary<Guid, int>> CountOpenRequestsByStaffAsync(
            IReadOnlyList<Guid> staffProfileIds, CancellationToken ct)
        {
            if (staffProfileIds.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            // Three queries — one per request kind — then union-merge in memory.
            var leaves = await _context.MobileLeaveRequests
                .AsNoTracking()
                .Where(r => staffProfileIds.Contains(r.StaffProfileId) && r.Status == MobileRequestStatuses.Pending)
                .GroupBy(r => r.StaffProfileId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var loans = await _context.MobileLoanRequests
                .AsNoTracking()
                .Where(r => staffProfileIds.Contains(r.StaffProfileId) && r.Status == MobileRequestStatuses.Pending)
                .GroupBy(r => r.StaffProfileId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var perms = await _context.MobilePermissionRequests
                .AsNoTracking()
                .Where(r => staffProfileIds.Contains(r.StaffProfileId) && r.Status == MobileRequestStatuses.Pending)
                .GroupBy(r => r.StaffProfileId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var dict = new Dictionary<Guid, int>();
            foreach (var r in leaves) dict[r.Id] = (dict.TryGetValue(r.Id, out var c) ? c : 0) + r.Count;
            foreach (var r in loans) dict[r.Id] = (dict.TryGetValue(r.Id, out var c) ? c : 0) + r.Count;
            foreach (var r in perms) dict[r.Id] = (dict.TryGetValue(r.Id, out var c) ? c : 0) + r.Count;
            return dict;
        }

        public async Task<double> GetTotalWorkingHoursAsync(Guid staffProfileId, CancellationToken ct)
        {
            var minutes = await _context.TimeEntries
                .AsNoTracking()
                .Where(t => t.StaffId == staffProfileId && t.DurationMinutes.HasValue)
                .SumAsync(t => (int?)t.DurationMinutes, ct) ?? 0;
            return Math.Round(minutes / 60.0, 2);
        }

        public Task<IReadOnlyList<TimeEntry>> GetTimeEntriesInRangeAsync(
            Guid staffProfileId,
            DateTime fromInclusive,
            DateTime toExclusive,
            CancellationToken ct)
        {
            return _context.TimeEntries
                .AsNoTracking()
                .Where(t => t.StaffId == staffProfileId
                    && ((t.ClockIn.HasValue && t.ClockIn.Value >= fromInclusive && t.ClockIn.Value < toExclusive)
                        || (!t.ClockIn.HasValue && t.Date >= fromInclusive && t.Date < toExclusive)))
                .OrderBy(t => t.ClockIn ?? t.Date)
                .ToListAsync(ct)
                .ContinueWith(t => (IReadOnlyList<TimeEntry>)t.Result, ct);
        }
    }
}
