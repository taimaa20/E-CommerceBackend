using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs.Attendance;
using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/hr")]
    [Authorize]
    public class HrController : ControllerBase
    {
        private readonly IHrEmployeeService _hrService;
        private readonly IBonusService _bonusService;
        private readonly IStaffService _staffService;
        private readonly IAttendanceService _attendanceService;
        private readonly IMobileLeaveRequestService _leaveService;
        private readonly IMobileLoanRequestService _loanService;
        private readonly IMobilePermissionRequestService _permissionService;
        private readonly ICurrentUserResolver _currentUserResolver;

        public HrController(
            IHrEmployeeService hrService,
            IBonusService bonusService,
            IStaffService staffService,
            IAttendanceService attendanceService,
            IMobileLeaveRequestService leaveService,
            IMobileLoanRequestService loanService,
            IMobilePermissionRequestService permissionService,
            ICurrentUserResolver currentUserResolver)
        {
            _hrService = hrService ?? throw new ArgumentNullException(nameof(hrService));
            _bonusService = bonusService ?? throw new ArgumentNullException(nameof(bonusService));
            _staffService = staffService ?? throw new ArgumentNullException(nameof(staffService));
            _attendanceService = attendanceService ?? throw new ArgumentNullException(nameof(attendanceService));
            _leaveService = leaveService ?? throw new ArgumentNullException(nameof(leaveService));
            _loanService = loanService ?? throw new ArgumentNullException(nameof(loanService));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _currentUserResolver = currentUserResolver ?? throw new ArgumentNullException(nameof(currentUserResolver));
        }

        // ===== Lookups =====

        [HttpGet("lookups")]
        public async Task<IActionResult> GetLookups(CancellationToken ct)
            => Ok(await _hrService.GetLookupsAsync(ct));

        // ===== Self (employee view) =====

        [HttpGet("me")]
        public async Task<IActionResult> GetMe(CancellationToken ct)
        {
            var userId = await _currentUserResolver.ResolveUserIdAsync(User);
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid session" });

            var profile = await _hrService.GetEmployeeByUserIdAsync(userId.Value, ct);
            return profile == null ? NotFound() : Ok(profile);
        }

        // ===== Admin: Employees =====

        [HttpGet("employees")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetEmployees(
            [FromQuery] string? search,
            [FromQuery] int? role,
            [FromQuery] int? contractStatus,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var data = await _hrService.GetEmployeesAsync(search, role, contractStatus, page, pageSize, ct);
            return Ok(data);
        }

        [HttpGet("employees/{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetEmployee(Guid id, CancellationToken ct)
        {
            var dto = await _hrService.GetEmployeeAsync(id, ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        [HttpPost("employees")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> CreateEmployee([FromBody] HrEmployeeCreateDto? dto, CancellationToken ct)
        {
            if (dto == null) return BadRequest(new { message = "Body required" });
            var actingUserId = (await _currentUserResolver.ResolveUserIdAsync(User)) ?? Guid.Empty;
            try
            {
                var created = await _staffService.CreateAsync(dto, actingUserId, ct);
                return CreatedAtAction(nameof(GetEmployee), new { id = created.EmployeeId }, created);
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("employees/{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> UpdateEmployee(Guid id, [FromBody] HrEmployeeUpdateDto? dto, CancellationToken ct)
        {
            try
            {

         
            if (dto == null) return BadRequest(new { message = "Body required" });
            var actingUserId = (await _currentUserResolver.ResolveUserIdAsync(User)) ?? Guid.Empty;
            try
            {
                var updated = await _staffService.UpdateAsync(id, dto, actingUserId, ct);
                return Ok(updated);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        [HttpGet("employees/{id:guid}/requests")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetEmployeeRequests(Guid id, CancellationToken ct)
            => Ok(await _hrService.GetEmployeeRequestsAsync(id, ct));

        [HttpGet("employees/{id:guid}/bonuses")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetEmployeeBonuses(
            Guid id,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var data = await _bonusService.GetAsync(id, status, null, null, null, page, pageSize, ct);
            return Ok(data);
        }

        [HttpPost("employees/{id:guid}/bonuses")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> CreateEmployeeBonus(Guid id, [FromBody] HrBonusCreateDto? dto, CancellationToken ct)
        {
            if (dto == null) return BadRequest(new { message = "Body required" });
            // path id wins
            dto.EmployeeId = id;
            var actingUserId = (await _currentUserResolver.ResolveUserIdAsync(User)) ?? Guid.Empty;
            var actingName = User?.Identity?.Name ?? "system";
            try
            {
                var bonus = await _bonusService.CreateAsync(dto, actingUserId, actingName, ct);
                return Ok(bonus);
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ===== Admin: Bonuses (cross-employee) =====

        [HttpGet("bonuses")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetBonuses(
            [FromQuery] Guid? employeeId,
            [FromQuery] string? status,
            [FromQuery] string? bonusType,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            try
            {
                var data = await _bonusService.GetAsync(employeeId, status, bonusType, from, to, page, pageSize, ct);
                return Ok(data);
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("bonuses")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> CreateBonus([FromBody] HrBonusCreateDto? dto, CancellationToken ct)
        {
            if (dto == null) return BadRequest(new { message = "Body required" });
            var actingUserId = (await _currentUserResolver.ResolveUserIdAsync(User)) ?? Guid.Empty;
            var actingName = User?.Identity?.Name ?? "system";
            try
            {
                var bonus = await _bonusService.CreateAsync(dto, actingUserId, actingName, ct);
                return Ok(bonus);
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("bonuses/{id:guid}/status")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> UpdateBonusStatus(
            Guid id,
            [FromBody] HrBonusUpdateStatusDto? dto,
            CancellationToken ct)
        {
            if (dto == null) return BadRequest(new { message = "Body required" });
            var actingUserId = (await _currentUserResolver.ResolveUserIdAsync(User)) ?? Guid.Empty;
            try
            {
                var bonus = await _bonusService.UpdateStatusAsync(id, dto.Status, actingUserId, ct);
                return Ok(bonus);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ===== Attendance (delegates to IAttendanceService over TimeEntry) =====
        // Single source of truth: TimeEntry feeds Stats + Timesheet + Analytics.

        [HttpPost("check-in")]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequest? request, CancellationToken ct)
        {
            var userId = await _currentUserResolver.ResolveUserIdAsync(User);
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid session" });

            var existing = await _attendanceService.GetActiveCheckInAsync(userId.Value, ct);
            if (existing != null)
            {
                return Conflict(new { message = "Already checked in today.", currentRecord = existing });
            }

            var record = await _attendanceService.CheckInAsync(userId.Value, request ?? new CheckInRequest(), ct);
            if (record == null) return BadRequest(new { message = "Unable to check in" });
            return StatusCode(StatusCodes.Status201Created, record);
        }

        [HttpPost("check-out")]
        public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest? request, CancellationToken ct)
        {
            var userId = await _currentUserResolver.ResolveUserIdAsync(User);
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid session" });

            var record = await _attendanceService.CheckOutAsync(userId.Value, request ?? new CheckOutRequest(), ct);
            if (record == null) return BadRequest(new { message = "No active check-in to close." });
            return Ok(record);
        }

        [HttpGet("me/attendance/active")]
        public async Task<IActionResult> GetMyActiveAttendance(CancellationToken ct)
        {
            var userId = await _currentUserResolver.ResolveUserIdAsync(User);
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid session" });
            var record = await _attendanceService.GetActiveCheckInAsync(userId.Value, ct);
            return Ok(new { active = record });
        }

        [HttpGet("attendance")]
        public async Task<IActionResult> GetAttendance([FromQuery] AttendanceFilterRequest filter, CancellationToken ct)
        {
            var userId = await _currentUserResolver.ResolveUserIdAsync(User);
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid session" });

            // Self-only unless caller is in the admin group; admin/manager can view all.
            if (!User.IsInRole(AppRoleNames.Admin) && !User.IsInRole(AppRoleNames.Manager))
            {
                filter.UserId = userId.Value;
            }

            var data = await _attendanceService.GetAttendanceAsync(filter ?? new AttendanceFilterRequest(), ct);
            return Ok(data);
        }

        [HttpGet("employees/{id:guid}/timesheet")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetEmployeeTimesheet(
            Guid id,
            [FromQuery] string? month,
            CancellationToken ct)
        {
            int year, mon;
            if (!string.IsNullOrWhiteSpace(month) && month!.Length == 7 && month[4] == '-'
                && int.TryParse(month[..4], out year) && int.TryParse(month[5..], out mon))
            {
                // ok
            }
            else
            {
                var now = DateTime.UtcNow;
                year = now.Year; mon = now.Month;
            }

            try
            {
                var dto = await _hrService.GetEmployeeTimesheetAsync(id, year, mon, ct);
                return dto == null ? NotFound() : Ok(dto);
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ===== Admin: Mobile request follow-up (leave / loan / permission) =====
        // Admin/Manager can list and update status; the original requester can cancel their own pending request.

        [HttpGet("leave-requests")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetLeaveRequests([FromQuery] HrRequestListQuery query, CancellationToken ct)
            => Ok(await _leaveService.GetAdminListAsync(BuildFilter(query), ct));

        [HttpPut("leave-requests/{id:guid}/status")]
        public async Task<IActionResult> UpdateLeaveStatus(Guid id, [FromBody] HrRequestStatusUpdateDto? dto, CancellationToken ct)
            => await UpdateRequestStatusAsync(dto, async (status, actingUserId, isAdmin) =>
                (object)await _leaveService.UpdateStatusAsync(id, status, actingUserId, isAdmin, ct));

        [HttpGet("loan-requests")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetLoanRequests([FromQuery] HrRequestListQuery query, CancellationToken ct)
            => Ok(await _loanService.GetAdminListAsync(BuildFilter(query), ct));

        [HttpPut("loan-requests/{id:guid}/status")]
        public async Task<IActionResult> UpdateLoanStatus(Guid id, [FromBody] HrRequestStatusUpdateDto? dto, CancellationToken ct)
            => await UpdateRequestStatusAsync(dto, async (status, actingUserId, isAdmin) =>
                (object)await _loanService.UpdateStatusAsync(id, status, actingUserId, isAdmin, ct));

        [HttpGet("permission-requests")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetPermissionRequests([FromQuery] HrRequestListQuery query, CancellationToken ct)
            => Ok(await _permissionService.GetAdminListAsync(BuildFilter(query), ct));

        [HttpPut("permission-requests/{id:guid}/status")]
        public async Task<IActionResult> UpdatePermissionStatus(Guid id, [FromBody] HrRequestStatusUpdateDto? dto, CancellationToken ct)
            => await UpdateRequestStatusAsync(dto, async (status, actingUserId, isAdmin) =>
                (object)await _permissionService.UpdateStatusAsync(id, status, actingUserId, isAdmin, ct));

        [HttpGet("permission-requests/{id:guid}/attachment")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> DownloadPermissionAttachment(Guid id, CancellationToken ct)
        {
            var stream = await _permissionService.OpenAttachmentAsync(id, ct);
            if (stream == null) return NotFound();
            return File(stream.Content, stream.ContentType, stream.FileName);
        }

        private async Task<IActionResult> UpdateRequestStatusAsync(
            HrRequestStatusUpdateDto? dto,
            Func<string, Guid, bool, Task<object>> mutate)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest(new { message = "Status is required." });

            var actingUserId = (await _currentUserResolver.ResolveUserIdAsync(User)) ?? Guid.Empty;
            if (actingUserId == Guid.Empty) return Unauthorized(new { message = "Invalid session" });

            var isAdmin = User.IsInRole(AppRoleNames.Admin) || User.IsInRole(AppRoleNames.Manager);

            try
            {
                var result = await mutate(dto.Status, actingUserId, isAdmin);
                return Ok(result);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        private static HrRequestAdminFilter BuildFilter(HrRequestListQuery query)
        {
            DateOnly? from = ParseDate(query.From);
            DateOnly? to = ParseDate(query.To);
            return new HrRequestAdminFilter(
                query.EmployeeId,
                NormalizeStatus(query.Status),
                string.IsNullOrWhiteSpace(query.Type) ? null : query.Type.Trim(),
                from,
                to,
                query.Page ?? 1,
                query.PageSize ?? 25);
        }

        private static string? NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            var s = status.Trim().ToLowerInvariant();
            return MobileRequestStatuses.All.Contains(s) ? s : null;
        }

        private static DateOnly? ParseDate(string? value)
            => string.IsNullOrWhiteSpace(value) ? null
                : (DateOnly.TryParse(value, out var d) ? d : (DateOnly?)null);
    }

    public sealed class HrRequestListQuery
    {
        public Guid? EmployeeId { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }
}
