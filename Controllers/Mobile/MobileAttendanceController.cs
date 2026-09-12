using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs.Attendance;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Controllers.Mobile
{
    [ApiController]
    [Route("api/mobile/attendance")]
    [Authorize]
    [EnableRateLimiting("api")]
    public class MobileAttendanceController : ControllerBase
    {
        private readonly IAttendanceService _attendanceService;
        private readonly ICurrentUserResolver _currentUserResolver;

        public MobileAttendanceController(IAttendanceService attendanceService, ICurrentUserResolver currentUserResolver)
        {
            _attendanceService = attendanceService;
            _currentUserResolver = currentUserResolver;
        }

        [HttpPost("checkin")]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequest? request, CancellationToken ct)
        {
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "جلسة غير صالحة / Invalid session" });
            }

            var existingRecord = await _attendanceService.GetActiveCheckInAsync(userId.Value, ct);
            if (existingRecord != null)
            {
                return Conflict(new
                {
                    message = "أنت بالفعل في الدوام / Already checked in",
                    currentRecord = existingRecord
                });
            }

            var record = await _attendanceService.CheckInAsync(userId.Value, request ?? new CheckInRequest(), ct);
            if (record == null)
            {
                return BadRequest(new { message = "تعذر تسجيل الحضور / Unable to check in" });
            }

            return StatusCode(StatusCodes.Status201Created, record);
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest? request, CancellationToken ct)
        {
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "جلسة غير صالحة / Invalid session" });
            }

            var record = await _attendanceService.CheckOutAsync(userId.Value, request ?? new CheckOutRequest(), ct);
            if (record == null)
            {
                return NotFound(new { message = "لا يوجد تسجيل دخول مفتوح / No active check-in found" });
            }

            return Ok(record);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActive(CancellationToken ct)
        {
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "جلسة غير صالحة / Invalid session" });
            }

            var record = await _attendanceService.GetActiveCheckInAsync(userId.Value, ct);
            if (record == null)
            {
                return NotFound(new { message = "لا يوجد تسجيل دخول مفتوح / No active check-in found" });
            }

            return Ok(record);
        }

        [HttpGet]
        public async Task<IActionResult> GetAttendance([FromQuery] AttendanceFilterRequest filter, CancellationToken ct)
        {
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "جلسة غير صالحة / Invalid session" });
            }

            if (!User.IsInRole(AppRoleNames.Admin))
            {
                filter.UserId = userId.Value;
            }

            var response = await _attendanceService.GetAttendanceAsync(filter, ct);
            return Ok(response);
        }

        private Task<Guid?> GetCurrentUserIdAsync()
            => _currentUserResolver.ResolveUserIdAsync(User);
    }
}
