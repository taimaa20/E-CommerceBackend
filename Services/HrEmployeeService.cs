using System.Globalization;
using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.DTOs.MobileRequests;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    public class HrEmployeeService : IHrEmployeeService
    {
        private readonly IHrEmployeeRepository _employeeRepo;
        private readonly IBonusRepository _bonusRepo;
        private readonly IMobileHrRequestRepository _hrRequestRepo;
        private readonly ILogger<HrEmployeeService> _logger;

        public HrEmployeeService(
            IHrEmployeeRepository employeeRepo,
            IBonusRepository bonusRepo,
            IMobileHrRequestRepository hrRequestRepo,
            ILogger<HrEmployeeService> logger)
        {
            _employeeRepo = employeeRepo ?? throw new ArgumentNullException(nameof(employeeRepo));
            _bonusRepo = bonusRepo ?? throw new ArgumentNullException(nameof(bonusRepo));
            _hrRequestRepo = hrRequestRepo ?? throw new ArgumentNullException(nameof(hrRequestRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HrEmployeeListResponse> GetEmployeesAsync(
            string? search,
            int? role,
            int? contractStatus,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            var filter = new HrEmployeeListFilter(search, role, contractStatus, page, pageSize);
            var result = await _employeeRepo.GetEmployeesAsync(filter, ct);

            var staffIds = result.Items
                .Where(r => r.StaffProfileId != Guid.Empty)
                .Select(r => r.StaffProfileId)
                .ToList();

            var openRequests = await _employeeRepo.CountOpenRequestsByStaffAsync(staffIds, ct);
            var pendingBonuses = await _bonusRepo.CountPendingByStaffAsync(staffIds, ct);

            var items = result.Items.Select(r => new HrEmployeeListItemDto
            {
                EmployeeId = r.StaffProfileId,
                UserId = r.UserId,
                Username = r.Username,
                FullName = r.FullName,
                FullNameAr = r.FullNameAr,
                Role = r.Role,
                StaffNo = r.StaffNo,
                Phone = r.Phone,
                PhotoUrl = r.PhotoUrl,
                ContractStatus = r.ContractStatus,
                StartDate = r.StartDate,
                MonthlySalary = r.MonthlySalary,
                NetSalary = r.NetSalary,
                OpenRequestsCount = openRequests.TryGetValue(r.StaffProfileId, out var orc) ? orc : 0,
                PendingBonusesCount = pendingBonuses.TryGetValue(r.StaffProfileId, out var pb) ? pb : 0
            }).ToList();

            return new HrEmployeeListResponse
            {
                Items = items,
                Total = result.Total,
                Page = filter.Page < 1 ? 1 : filter.Page,
                PageSize = filter.PageSize < 1 ? 25 : Math.Min(filter.PageSize, 200)
            };
        }

        public async Task<HrEmployeeDetailDto?> GetEmployeeAsync(Guid employeeId, CancellationToken ct)
        {
            var staff = await _employeeRepo.GetByIdAsync(employeeId, ct);
            return staff == null ? null : await BuildDetailAsync(staff, ct);
        }

        public async Task<HrEmployeeDetailDto?> GetEmployeeByUserIdAsync(Guid userId, CancellationToken ct)
        {
            var staff = await _employeeRepo.GetByUserIdAsync(userId, ct);
            return staff == null ? null : await BuildDetailAsync(staff, ct);
        }

        public async Task<HrEmployeeRequestsDto> GetEmployeeRequestsAsync(Guid employeeId, CancellationToken ct)
        {
            var filter = new MobileRequestFilter(null, null, null, 1, 50);
            var leaves = await _hrRequestRepo.GetLeaveRequestsAsync(employeeId, filter, ct);
            var loans = await _hrRequestRepo.GetLoanRequestsAsync(employeeId, filter, ct);
            var perms = await _hrRequestRepo.GetPermissionRequestsAsync(employeeId, filter, ct);

            return new HrEmployeeRequestsDto
            {
                Leaves = leaves.Select(MapLeave).ToList(),
                Loans = loans.Select(MapLoan).ToList(),
                Permissions = perms.Select(MapPermission).ToList()
            };
        }

        public async Task<HrTimesheetDto?> GetEmployeeTimesheetAsync(Guid employeeId, int year, int month, CancellationToken ct)
        {
            if (year < 2000 || year > 2999) throw new ArgumentException("Invalid year.", nameof(year));
            if (month < 1 || month > 12) throw new ArgumentException("Invalid month.", nameof(month));

            var staff = await _employeeRepo.GetByIdAsync(employeeId, ct);
            if (staff == null) return null;

            var fromInclusive = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var toExclusive = fromInclusive.AddMonths(1);

            var entries = await _employeeRepo.GetTimeEntriesInRangeAsync(staff.Id, fromInclusive, toExclusive, ct);

            // Bucket by day; one summary per day. If multiple sessions exist, sum them.
            var byDay = entries
                .GroupBy(e => (e.ClockIn ?? e.Date).Date)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var firstIn = g.Where(x => x.ClockIn.HasValue)
                        .OrderBy(x => x.ClockIn).Select(x => x.ClockIn).FirstOrDefault();
                    var lastOut = g.Where(x => x.ClockOut.HasValue)
                        .OrderByDescending(x => x.ClockOut).Select(x => x.ClockOut).FirstOrDefault();
                    var minutes = g.Sum(x => x.DurationMinutes ?? 0);
                    var hasOpen = g.Any(x => x.ClockIn.HasValue && !x.ClockOut.HasValue);
                    return new HrTimesheetDayDto
                    {
                        Date = g.Key.ToString("yyyy-MM-dd"),
                        CheckInAt = firstIn,
                        CheckOutAt = lastOut,
                        DurationMinutes = minutes,
                        Status = hasOpen ? "CheckedIn" : (minutes > 0 ? "CheckedOut" : "Absent")
                    };
                })
                .ToList();

            var totalMinutes = byDay.Sum(d => d.DurationMinutes);
            var openSessions = entries.Count(e => e.ClockIn.HasValue && !e.ClockOut.HasValue);

            return new HrTimesheetDto
            {
                EmployeeId = staff.Id,
                Month = $"{year:D4}-{month:D2}",
                TotalMinutes = totalMinutes,
                TotalHours = Math.Round(totalMinutes / 60.0, 2),
                DaysWorked = byDay.Count(d => d.DurationMinutes > 0 || d.Status == "CheckedIn"),
                OpenSessions = openSessions,
                Days = byDay
            };
        }

        public Task<HrLookupsDto> GetLookupsAsync(CancellationToken ct)
        {
            // Constants today; trivial swap to DB lookup tables when they exist.
            // The label values mirror MobileRequestLabels output so frontend renders consistently.
            var dto = new HrLookupsDto
            {
                LeaveTypes = new[]
                {
                    new HrCodeLabelDto { Code = MobileLeaveRequestTypes.Annual,    Label = "Annual",    LabelAr = "سنوي" },
                    new HrCodeLabelDto { Code = MobileLeaveRequestTypes.Sick,      Label = "Sick",      LabelAr = "مرضي" },
                    new HrCodeLabelDto { Code = MobileLeaveRequestTypes.Personal,  Label = "Personal",  LabelAr = "شخصي" },
                    new HrCodeLabelDto { Code = MobileLeaveRequestTypes.Emergency, Label = "Emergency", LabelAr = "طارئ" }
                },
                LoanTypes = new[]
                {
                    new HrCodeLabelDto { Code = MobileLoanRequestTypes.MarriageLoan,     Label = "Marriage loan",      LabelAr = "قرض زواج" },
                    new HrCodeLabelDto { Code = MobileLoanRequestTypes.LifeExpensesLoan, Label = "Life-expenses loan", LabelAr = "قرض نفقات معيشية" }
                },
                PermissionTypes = new[]
                {
                    new HrCodeLabelDto { Code = MobilePermissionRequestTypes.Personal, Label = "Personal", LabelAr = "شخصي" },
                    new HrCodeLabelDto { Code = MobilePermissionRequestTypes.Official, Label = "Official", LabelAr = "رسمي" },
                    new HrCodeLabelDto { Code = MobilePermissionRequestTypes.Medical,  Label = "Medical",  LabelAr = "طبي" }
                },
                BonusTypes = new[]
                {
                    new HrCodeLabelDto { Code = HrBonusTypes.Performance, Label = "Performance", LabelAr = "أداء" },
                    new HrCodeLabelDto { Code = HrBonusTypes.Holiday,     Label = "Holiday",     LabelAr = "عيد" },
                    new HrCodeLabelDto { Code = HrBonusTypes.Annual,      Label = "Annual",      LabelAr = "سنوي" },
                    new HrCodeLabelDto { Code = HrBonusTypes.Referral,    Label = "Referral",    LabelAr = "إحالة" },
                    new HrCodeLabelDto { Code = HrBonusTypes.Spot,        Label = "Spot",        LabelAr = "فوري" },
                    new HrCodeLabelDto { Code = HrBonusTypes.Other,       Label = "Other",       LabelAr = "أخرى" }
                },
                RequestStatuses = new[]
                {
                    new HrCodeLabelDto { Code = MobileRequestStatuses.Pending,   Label = "Pending",   LabelAr = "قيد المراجعة" },
                    new HrCodeLabelDto { Code = MobileRequestStatuses.Approved,  Label = "Approved",  LabelAr = "تمت الموافقة" },
                    new HrCodeLabelDto { Code = MobileRequestStatuses.Rejected,  Label = "Rejected",  LabelAr = "مرفوض" },
                    new HrCodeLabelDto { Code = MobileRequestStatuses.Cancelled, Label = "Cancelled", LabelAr = "ملغى" }
                },
                BonusStatuses = new[]
                {
                    new HrCodeLabelDto { Code = HrBonusStatuses.Pending,   Label = "Pending",   LabelAr = "قيد المراجعة" },
                    new HrCodeLabelDto { Code = HrBonusStatuses.Approved,  Label = "Approved",  LabelAr = "تمت الموافقة" },
                    new HrCodeLabelDto { Code = HrBonusStatuses.Paid,      Label = "Paid",      LabelAr = "مدفوع" },
                    new HrCodeLabelDto { Code = HrBonusStatuses.Cancelled, Label = "Cancelled", LabelAr = "ملغى" }
                },
                ContractStatuses = new[]
                {
                    new HrCodeLabelDto { Code = "0", Label = "Pending",    LabelAr = "بانتظار" },
                    new HrCodeLabelDto { Code = "1", Label = "Signed",     LabelAr = "موقع" },
                    new HrCodeLabelDto { Code = "2", Label = "Terminated", LabelAr = "منتهي" }
                },
                Roles = Enum.GetValues<UserRole>()
                    .Select(r => new HrCodeLabelDto { Code = ((int)r).ToString(CultureInfo.InvariantCulture), Label = r.ToString() })
                    .ToList()
            };
            return Task.FromResult(dto);
        }

        private async Task<HrEmployeeDetailDto> BuildDetailAsync(StaffProfile staff, CancellationToken ct)
        {
            var totals = await _bonusRepo.GetTotalsByStaffAsync(new[] { staff.Id }, ct);
            var openLeaves = await _employeeRepo.CountOpenLeaveRequestsAsync(staff.Id, ct);
            var openLoans = await _employeeRepo.CountOpenLoanRequestsAsync(staff.Id, ct);
            var openPerms = await _employeeRepo.CountOpenPermissionRequestsAsync(staff.Id, ct);
            var hours = await _employeeRepo.GetTotalWorkingHoursAsync(staff.Id, ct);

            var totalsForStaff = totals.TryGetValue(staff.Id, out var t) ? t : (count: 0, total: 0m);

            return new HrEmployeeDetailDto
            {
                EmployeeId = staff.Id,
                UserId = staff.UserId,
                Username = staff.User?.Username ?? string.Empty,
                FullName = staff.User?.FullName,
                FullNameAr = staff.User?.FullNameAr,
                Role = staff.User?.Role.ToString() ?? string.Empty,
                StaffNo = staff.StaffNo,
                PinCode = staff.PinCode,
                BloodType = (int)staff.BloodType,
                Phone = staff.Phone,
                Address = staff.Address,
                PhotoUrl = staff.PhotoUrl,
                StartDate = staff.StartDate,
                ContractStatus = (int)staff.ContractStatus,
                MonthlySalary = staff.User?.MonthlySalary ?? 0m,
                NetSalary = staff.NetSalary,
                SgkPremium = staff.SgkPremium,
                HourlyWage = staff.HourlyWage,
                CommissionRate = staff.User?.CommissionRate ?? 0m,
                WeeklyShiftPattern = staff.WeeklyShiftPattern,
                Stats = new HrEmployeeStatsDto
                {
                    TotalBonuses = totalsForStaff.count,
                    TotalBonusAmount = totalsForStaff.total,
                    OpenLeaveRequests = openLeaves,
                    OpenLoanRequests = openLoans,
                    OpenPermissionRequests = openPerms,
                    TotalWorkingHours = hours
                }
            };
        }

        private static HrEmployeeRequestItemDto MapLeave(MobileLeaveRequestDto r) => new()
        {
            Id = Guid.TryParse(r.Id, out var g) ? g : Guid.Empty,
            Kind = "leave",
            TypeCode = r.Type,
            TypeLabel = MobileRequestLabels.LeaveType(r.Type),
            Title = $"{r.DurationInDays} day(s)",
            Subtitle = $"{r.StartDate} → {r.EndDate}",
            Status = r.Status,
            StatusLabel = MobileRequestLabels.Status(r.Status),
            CreatedAt = DateTime.UtcNow,
            Date = r.StartDate
        };

        private static HrEmployeeRequestItemDto MapLoan(MobileLoanRequestDto r)
        {
            // Recover canonical status code from the label-only DTO so the UI can drive transitions.
            var code = ResolveStatusCodeFromLabel(r.StatusLabel);
            return new HrEmployeeRequestItemDto
            {
                Id = Guid.TryParse(r.Id, out var g) ? g : Guid.Empty,
                Kind = "loan",
                TypeCode = r.LoanType,
                TypeLabel = r.LoanTypeLabel,
                Title = r.AmountLabel,
                Subtitle = r.Subtitle,
                Status = code,
                StatusLabel = r.StatusLabel,
                CreatedAt = DateTime.UtcNow,
                Date = r.RequestDate
            };
        }

        private static HrEmployeeRequestItemDto MapPermission(MobilePermissionRequestListItemDto r)
        {
            var code = ResolveStatusCodeFromLabel(r.StatusLabel);
            return new HrEmployeeRequestItemDto
            {
                Id = Guid.TryParse(r.Id, out var g) ? g : Guid.Empty,
                Kind = "permission",
                TypeCode = r.PermissionType,
                TypeLabel = r.TypeLabel,
                Title = r.TimeRange,
                Subtitle = r.Reason,
                Status = code,
                StatusLabel = r.StatusLabel,
                CreatedAt = DateTime.UtcNow,
                Date = r.Date
            };
        }

        private static string ResolveStatusCodeFromLabel(string label)
            => label switch
            {
                "Pending" => MobileRequestStatuses.Pending,
                "Approved" => MobileRequestStatuses.Approved,
                "Rejected" => MobileRequestStatuses.Rejected,
                "Cancelled" => MobileRequestStatuses.Cancelled,
                _ => label.ToLowerInvariant()
            };
    }
}
