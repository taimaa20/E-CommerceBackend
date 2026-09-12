using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.DTOs.MobileRequests;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Repositories
{
    public class MobileHrRequestRepository : IMobileHrRequestRepository
    {
        private readonly PosDbContext _context;

        public MobileHrRequestRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<MobileStaffSnapshot?> GetOrCreateStaffSnapshotAsync(Guid userId, CancellationToken ct)
        {
            var snapshot = await QueryStaffSnapshot(userId).FirstOrDefaultAsync(ct);
            if (snapshot != null)
            {
                return snapshot;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
            {
                return null;
            }

            _context.StaffProfiles.Add(new StaffProfile
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                UserId = user.Id,
                StaffNo = StaffNumberHelper.BuildGeneratedStaffNumber(user.Username, user.Id)
            });

            await _context.SaveChangesAsync(ct);
            return await QueryStaffSnapshot(userId).FirstOrDefaultAsync(ct);
        }

        public async Task<IReadOnlyList<MobileLeaveRequestDto>> GetLeaveRequestsAsync(
            Guid staffProfileId,
            MobileRequestFilter filter,
            CancellationToken ct)
        {
            var query = _context.MobileLeaveRequests
                .AsNoTracking()
                .Where(r => r.StaffProfileId == staffProfileId);

            query = ApplyLeaveFilters(query, filter);
            var rows = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(r => new
                {
                    r.Id,
                    r.StartDate,
                    r.EndDate,
                    r.DurationInDays,
                    r.Type,
                    r.Notes,
                    r.Status
                })
                .ToListAsync(ct);

            return rows.Select(r => new MobileLeaveRequestDto
            {
                Id = r.Id.ToString(),
                StartDate = FormatDate(r.StartDate),
                EndDate = FormatDate(r.EndDate),
                DurationInDays = r.DurationInDays,
                Type = r.Type,
                Notes = r.Notes,
                Status = r.Status
            }).ToList();
        }

        public Task<bool> HasOverlappingLeaveRequestAsync(
            Guid staffProfileId,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken ct)
            => _context.MobileLeaveRequests
                .AsNoTracking()
                .AnyAsync(r => r.StaffProfileId == staffProfileId
                    && r.Status != MobileRequestStatuses.Rejected
                    && r.StartDate <= endDate
                    && r.EndDate >= startDate,
                    ct);

        public async Task<MobileLeaveRequest> AddLeaveRequestAsync(MobileLeaveRequest request, CancellationToken ct)
        {
            _context.MobileLeaveRequests.Add(request);
            await _context.SaveChangesAsync(ct);
            return request;
        }

        public async Task<IReadOnlyList<MobileLoanRequestDto>> GetLoanRequestsAsync(
            Guid staffProfileId,
            MobileRequestFilter filter,
            CancellationToken ct)
        {
            var query = _context.MobileLoanRequests
                .AsNoTracking()
                .Where(r => r.StaffProfileId == staffProfileId);

            query = ApplyLoanFilters(query, filter);
            var rows = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(r => new
                {
                    r.Id,
                    r.LoanType,
                    r.RequestDate,
                    r.RequestedAmount,
                    r.Currency,
                    r.Status,
                    r.PaymentPeriodMonths,
                    r.InstallmentStartDate
                })
                .ToListAsync(ct);

            return rows.Select(r => MapLoan(r.Id, r.LoanType, r.RequestDate,
                r.RequestedAmount, r.Currency, r.Status, r.PaymentPeriodMonths,
                r.InstallmentStartDate)).ToList();
        }

        public async Task<decimal> GetPreviousLoanBalanceAsync(Guid staffProfileId, CancellationToken ct)
        {
            return await _context.MobileLoanRequests
                .AsNoTracking()
                .Where(r => r.StaffProfileId == staffProfileId
                    && r.Status != MobileRequestStatuses.Rejected)
                .SumAsync(r => (decimal?)r.NetAmount, ct) ?? 0m;
        }

        public async Task<MobileLoanRequest> AddLoanRequestAsync(MobileLoanRequest request, CancellationToken ct)
        {
            _context.MobileLoanRequests.Add(request);
            await _context.SaveChangesAsync(ct);
            return request;
        }

        public async Task<IReadOnlyList<MobilePermissionRequestListItemDto>> GetPermissionRequestsAsync(
            Guid staffProfileId,
            MobileRequestFilter filter,
            CancellationToken ct)
        {
            var query = _context.MobilePermissionRequests
                .AsNoTracking()
                .Where(r => r.StaffProfileId == staffProfileId);

            query = ApplyPermissionFilters(query, filter);
            var rows = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(r => new
                {
                    r.Id,
                    r.Date,
                    r.PermissionType,
                    r.TimeFrom,
                    r.TimeTo,
                    r.Status,
                    r.Reason
                })
                .ToListAsync(ct);

            return rows.Select(r => new MobilePermissionRequestListItemDto
            {
                Id = r.Id.ToString(),
                Date = FormatDate(r.Date),
                TypeLabel = MobileRequestLabels.PermissionType(r.PermissionType),
                PermissionType = r.PermissionType,
                TimeFrom = FormatTime(r.TimeFrom),
                TimeTo = FormatTime(r.TimeTo),
                TimeRange = $"{FormatTime(r.TimeFrom)} – {FormatTime(r.TimeTo)}",
                StatusLabel = MobileRequestLabels.Status(r.Status),
                Reason = r.Reason
            }).ToList();
        }

        public async Task<int> GetUsedPermissionMinutesAsync(
            Guid staffProfileId,
            DateOnly periodStart,
            DateOnly periodEnd,
            CancellationToken ct)
        {
            return await _context.MobilePermissionRequests
                .AsNoTracking()
                .Where(r => r.StaffProfileId == staffProfileId
                    && r.Status != MobileRequestStatuses.Rejected
                    && r.Date >= periodStart
                    && r.Date <= periodEnd)
                .SumAsync(r => (int?)r.DurationMinutes, ct) ?? 0;
        }

        public async Task<MobilePermissionRequest> AddPermissionRequestAsync(
            MobilePermissionRequest request,
            CancellationToken ct)
        {
            _context.MobilePermissionRequests.Add(request);
            await _context.SaveChangesAsync(ct);
            return request;
        }

        // ===== Admin =====

        public async Task<HrAdminRequestPage<MobileLeaveRequest>> GetAdminLeaveRequestsAsync(
            HrRequestAdminFilter filter,
            CancellationToken ct)
        {
            var query = _context.MobileLeaveRequests.AsNoTracking().AsQueryable();
            if (filter.EmployeeId.HasValue) query = query.Where(r => r.StaffProfileId == filter.EmployeeId.Value);
            if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(r => r.Status == filter.Status);
            if (!string.IsNullOrWhiteSpace(filter.TypeCode)) query = query.Where(r => r.Type == filter.TypeCode);
            if (filter.From.HasValue) query = query.Where(r => r.EndDate >= filter.From.Value);
            if (filter.To.HasValue) query = query.Where(r => r.StartDate <= filter.To.Value);

            var total = await query.CountAsync(ct);
            var statusCounts = await query
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var rows = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.NormalizedPageSize)
                .Join(_context.StaffProfiles.AsNoTracking().Include(sp => sp.User),
                      r => r.StaffProfileId,
                      sp => sp.Id,
                      (r, sp) => new { Request = r, Staff = sp })
                .ToListAsync(ct);

            var items = rows.Select(x => new HrAdminRequestRow<MobileLeaveRequest>(
                x.Request,
                MapEmployee(x.Staff))).ToList();

            return new HrAdminRequestPage<MobileLeaveRequest>(
                items,
                total,
                statusCounts.ToDictionary(x => x.Status, x => x.Count, StringComparer.Ordinal));
        }

        public async Task<HrAdminRequestPage<MobileLoanRequest>> GetAdminLoanRequestsAsync(
            HrRequestAdminFilter filter,
            CancellationToken ct)
        {
            var query = _context.MobileLoanRequests.AsNoTracking().AsQueryable();
            if (filter.EmployeeId.HasValue) query = query.Where(r => r.StaffProfileId == filter.EmployeeId.Value);
            if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(r => r.Status == filter.Status);
            if (!string.IsNullOrWhiteSpace(filter.TypeCode)) query = query.Where(r => r.LoanType == filter.TypeCode);
            if (filter.From.HasValue) query = query.Where(r => r.RequestDate >= filter.From.Value);
            if (filter.To.HasValue) query = query.Where(r => r.RequestDate <= filter.To.Value);

            var total = await query.CountAsync(ct);
            var statusCounts = await query
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var rows = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.NormalizedPageSize)
                .Join(_context.StaffProfiles.AsNoTracking().Include(sp => sp.User),
                      r => r.StaffProfileId,
                      sp => sp.Id,
                      (r, sp) => new { Request = r, Staff = sp })
                .ToListAsync(ct);

            var items = rows.Select(x => new HrAdminRequestRow<MobileLoanRequest>(
                x.Request,
                MapEmployee(x.Staff))).ToList();

            return new HrAdminRequestPage<MobileLoanRequest>(
                items,
                total,
                statusCounts.ToDictionary(x => x.Status, x => x.Count, StringComparer.Ordinal));
        }

        public async Task<HrAdminRequestPage<MobilePermissionRequest>> GetAdminPermissionRequestsAsync(
            HrRequestAdminFilter filter,
            CancellationToken ct)
        {
            var query = _context.MobilePermissionRequests.AsNoTracking().AsQueryable();
            if (filter.EmployeeId.HasValue) query = query.Where(r => r.StaffProfileId == filter.EmployeeId.Value);
            if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(r => r.Status == filter.Status);
            if (!string.IsNullOrWhiteSpace(filter.TypeCode)) query = query.Where(r => r.PermissionType == filter.TypeCode);
            if (filter.From.HasValue) query = query.Where(r => r.Date >= filter.From.Value);
            if (filter.To.HasValue) query = query.Where(r => r.Date <= filter.To.Value);

            var total = await query.CountAsync(ct);
            var statusCounts = await query
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var rows = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.NormalizedPageSize)
                .Join(_context.StaffProfiles.AsNoTracking().Include(sp => sp.User),
                      r => r.StaffProfileId,
                      sp => sp.Id,
                      (r, sp) => new { Request = r, Staff = sp })
                .ToListAsync(ct);

            var items = rows.Select(x => new HrAdminRequestRow<MobilePermissionRequest>(
                x.Request,
                MapEmployee(x.Staff))).ToList();

            return new HrAdminRequestPage<MobilePermissionRequest>(
                items,
                total,
                statusCounts.ToDictionary(x => x.Status, x => x.Count, StringComparer.Ordinal));
        }

        public Task<MobileLeaveRequest?> GetLeaveByIdAsync(Guid id, CancellationToken ct)
            => _context.MobileLeaveRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

        public Task<MobileLoanRequest?> GetLoanByIdAsync(Guid id, CancellationToken ct)
            => _context.MobileLoanRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

        public Task<MobilePermissionRequest?> GetPermissionByIdAsync(Guid id, CancellationToken ct)
            => _context.MobilePermissionRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

        public async Task<HrRequestEmployeeDto?> GetEmployeeForStaffAsync(Guid staffProfileId, CancellationToken ct)
        {
            var sp = await _context.StaffProfiles
                .AsNoTracking()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == staffProfileId, ct);
            return sp == null ? null : MapEmployee(sp);
        }

        public Task SaveChangesAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);

        private static HrRequestEmployeeDto MapEmployee(StaffProfile sp) => new()
        {
            EmployeeId = sp.Id,
            UserId = sp.UserId,
            Username = sp.User?.Username ?? string.Empty,
            FullName = sp.User?.FullName,
            FullNameAr = sp.User?.FullNameAr,
            StaffNo = sp.StaffNo,
            PhotoUrl = sp.PhotoUrl
        };

        private IQueryable<MobileStaffSnapshot> QueryStaffSnapshot(Guid userId)
            => _context.StaffProfiles
                .AsNoTracking()
                .Where(sp => sp.UserId == userId)
                .Select(sp => new MobileStaffSnapshot(
                    sp.Id,
                    sp.TenantId,
                    sp.UserId,
                    sp.User.Username,
                    sp.User.FullName,
                    sp.StaffNo,
                    sp.Phone,
                    sp.PhotoUrl,
                    sp.StartDate,
                    sp.User.MonthlySalary,
                    sp.NetSalary));

        private static IQueryable<MobileLeaveRequest> ApplyLeaveFilters(
            IQueryable<MobileLeaveRequest> query,
            MobileRequestFilter filter)
        {
            if (filter.Status != null) query = query.Where(r => r.Status == filter.Status);
            if (filter.From.HasValue) query = query.Where(r => r.EndDate >= filter.From.Value);
            if (filter.To.HasValue) query = query.Where(r => r.StartDate <= filter.To.Value);
            return query;
        }

        private static IQueryable<MobileLoanRequest> ApplyLoanFilters(
            IQueryable<MobileLoanRequest> query,
            MobileRequestFilter filter)
        {
            if (filter.Status != null) query = query.Where(r => r.Status == filter.Status);
            if (filter.From.HasValue) query = query.Where(r => r.RequestDate >= filter.From.Value);
            if (filter.To.HasValue) query = query.Where(r => r.RequestDate <= filter.To.Value);
            return query;
        }

        private static IQueryable<MobilePermissionRequest> ApplyPermissionFilters(
            IQueryable<MobilePermissionRequest> query,
            MobileRequestFilter filter)
        {
            if (filter.Status != null) query = query.Where(r => r.Status == filter.Status);
            if (filter.From.HasValue) query = query.Where(r => r.Date >= filter.From.Value);
            if (filter.To.HasValue) query = query.Where(r => r.Date <= filter.To.Value);
            return query;
        }

        private static MobileLoanRequestDto MapLoan(
            Guid id,
            string loanType,
            DateOnly requestDate,
            decimal amount,
            string currency,
            string status,
            int months,
            DateOnly installmentStartDate)
            => new()
            {
                Id = id.ToString(),
                LoanTypeLabel = MobileRequestLabels.LoanType(loanType),
                LoanType = loanType,
                RequestDate = FormatDate(requestDate),
                AmountLabel = $"{amount.ToString("N0", CultureInfo.InvariantCulture)} {currency}",
                StatusLabel = MobileRequestLabels.Status(status),
                PaymentPeriodMonths = months,
                Subtitle = $"{months} months · EMI starts {installmentStartDate.ToString("MMM yyyy", CultureInfo.InvariantCulture)}"
            };

        private static string FormatDate(DateOnly date)
            => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static string FormatTime(TimeOnly time)
            => time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
