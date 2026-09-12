using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = AppRoleNames.Admin)]
    public class StaffController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;

        public StaffController(PosDbContext context, ITenantResolver tenantResolver)
        {
            _context = context;
            _tenantResolver = tenantResolver;
        }

        [HttpGet("analytics")]
        public async Task<IActionResult> GetStaffAnalytics()
        {
            // Pre-load each staff user's StaffProfile so we can surface NetSalary even when
            // the legacy User.MonthlySalary column was never populated by older form versions.
            var staff = await _context.Users
                .Where(u => u.Role == UserRole.Waiter || u.Role == UserRole.Kitchen || u.Role == UserRole.Cashier || u.Role == UserRole.TrackerPickup)
                .ToListAsync();

            var userIds = staff.Select(u => u.Id).ToList();
            var profiles = await _context.StaffProfiles
                .Where(sp => userIds.Contains(sp.UserId))
                .ToDictionaryAsync(sp => sp.UserId);

            var staffAnalytics = new List<StaffAnalyticsDto>();

            foreach (var user in staff)
            {
                profiles.TryGetValue(user.Id, out var profile);

                // Working hours: HR check-in/out writes to TimeEntries (StaffId = StaffProfile.Id).
                // Older data may still live in Shifts (UserId-keyed) — use it as a fallback only when
                // TimeEntries has nothing for this employee.
                double totalHours = 0;
                if (profile != null)
                {
                    var minutes = await _context.TimeEntries
                        .Where(t => t.StaffId == profile.Id && t.DurationMinutes.HasValue)
                        .SumAsync(t => (int?)t.DurationMinutes) ?? 0;
                    totalHours = Math.Round(minutes / 60.0, 2);
                }
                if (totalHours == 0)
                {
                    var shifts = await _context.Shifts
                        .Where(s => s.UserId == user.Id && s.ClockOut.HasValue)
                        .ToListAsync();
                    totalHours = shifts.Sum(s => s.TotalHours);
                }

                var paidWaiterOrders = _context.Orders
                    .Where(o => o.WaiterId == user.Id)
                    .WherePaid();

                var totalOrders = await paidWaiterOrders.CountAsync();

                var totalSales = await paidWaiterOrders
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

                var commissionEarned = totalSales * (user.CommissionRate / 100);

                var net = profile?.NetSalary ?? 0m;
                var sgk = profile?.SgkPremium ?? 0m;
                // If User.MonthlySalary was never set (older records), fall back to net + sgk gross.
                var monthly = user.MonthlySalary > 0m ? user.MonthlySalary : net + sgk;

                staffAnalytics.Add(new StaffAnalyticsDto
                {
                    UserId = user.Id,
                    Username = user.Username,
                    FullName = user.FullName ?? user.Username,
                    FullNameAr = user.FullNameAr,
                    Role = user.Role.ToString(),
                    MonthlySalary = monthly,
                    NetSalary = net,
                    SgkPremium = sgk,
                    CommissionRate = user.CommissionRate,
                    TotalWorkingHours = Math.Round(totalHours, 2),
                    TotalOrders = totalOrders,
                    TotalSales = totalSales,
                    CommissionEarned = commissionEarned
                });
            }

            return Ok(staffAnalytics);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetStaffDetails(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            return Ok(new StaffDetailDto
            {
                UserId = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                FullNameAr = user.FullNameAr,
                Role = user.Role.ToString(),
                MonthlySalary = user.MonthlySalary,
                CommissionRate = user.CommissionRate
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateStaffDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            user.FullName = dto.FullName;
            user.FullNameAr = dto.FullNameAr;
            user.MonthlySalary = dto.MonthlySalary;
            user.CommissionRate = dto.CommissionRate;

            await _context.SaveChangesAsync();
            return Ok(user);
        }

        [HttpGet("{id}/shifts")]
        public async Task<IActionResult> GetStaffShifts(Guid id, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-7);
            var end = endDate ?? DateTime.UtcNow;

            var shifts = await _context.Shifts
                .Where(s => s.UserId == id && s.ClockIn >= start && s.ClockIn <= end)
                .OrderByDescending(s => s.ClockIn)
                .Select(s => new ShiftDto
                {
                    Id = s.Id,
                    ClockIn = s.ClockIn,
                    ClockOut = s.ClockOut,
                    TotalHours = s.TotalHours
                })
                .ToListAsync();

            return Ok(shifts);
        }

        [HttpPost("{id}/clock-in")]
        public async Task<IActionResult> ClockIn(Guid id)
        {
            var shift = new Shift
            {
                Id = Guid.NewGuid(),
                UserId = id,
                ClockIn = DateTime.UtcNow,
                TenantId = _tenantResolver.GetTenantId()
            };

            _context.Shifts.Add(shift);
            await _context.SaveChangesAsync();
            return Ok(shift);
        }

        [HttpPost("{id}/clock-out")]
        public async Task<IActionResult> ClockOut(Guid id)
        {
            var activeShift = await _context.Shifts
                .Where(s => s.UserId == id && s.ClockOut == null)
                .OrderByDescending(s => s.ClockIn)
                .FirstOrDefaultAsync();

            if (activeShift == null)
                return BadRequest("No active shift found");

            activeShift.ClockOut = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(activeShift);
        }
    }

    // DTOs
    public class StaffAnalyticsDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? FullNameAr { get; set; }
        public string Role { get; set; } = string.Empty;
        public decimal MonthlySalary { get; set; }
        public decimal NetSalary { get; set; }
        public decimal SgkPremium { get; set; }
        public decimal CommissionRate { get; set; }
        public double TotalWorkingHours { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSales { get; set; }
        public decimal CommissionEarned { get; set; }
    }

    public class StaffDetailDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? FullNameAr { get; set; }
        public string Role { get; set; } = string.Empty;
        public decimal MonthlySalary { get; set; }
        public decimal CommissionRate { get; set; }
    }

    public class UpdateStaffDto
    {
        public string? FullName { get; set; }
        public string? FullNameAr { get; set; }
        public decimal MonthlySalary { get; set; }
        public decimal CommissionRate { get; set; }
    }

    public class ShiftDto
    {
        public Guid Id { get; set; }
        public DateTime ClockIn { get; set; }
        public DateTime? ClockOut { get; set; }
        public double TotalHours { get; set; }
    }
}
