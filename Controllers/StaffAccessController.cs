using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StaffAccessController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly ITimeTrackingService _timeTrackingService;
        private readonly ITenantResolver _tenantResolver;

        public StaffAccessController(
            PosDbContext context,
            ITimeTrackingService timeTrackingService,
            ITenantResolver tenantResolver)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _timeTrackingService = timeTrackingService ?? throw new ArgumentNullException(nameof(timeTrackingService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        // Kiosk PIN Authentication
        [HttpPost("identify")]
        public async Task<IActionResult> Identify([FromBody] PinRequest request)
        {
            if (string.IsNullOrEmpty(request.Pin)) return BadRequest("PIN is required");

            var staffProfile = await _context.StaffProfiles
                .Include(sp => sp.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(sp => sp.PinCode == request.Pin);

            if (staffProfile == null)
            {
                // Demo: If PIN matches NO ONE, but it's the demo PIN '123456', Force Create/Get Demo User
                if (request.Pin == "123456") 
                {
                    var demoUser = await _context.Users.Include(u => u.StaffProfile).FirstOrDefaultAsync(u => u.Username == "garson");
                    
                    if (demoUser == null)
                    {
                        // 1. Create User if missing
                        demoUser = new User
                        {
                            Id = Guid.NewGuid(),
                            TenantId = _tenantResolver.GetTenantId(),
                            Username = "garson",
                            PasswordHash = PasswordHelper.Hash("1234"),
                            Role = UserRole.Waiter,
                            FullName = "Demo Garson"
                        };
                        _context.Users.Add(demoUser);
                        await _context.SaveChangesAsync();
                    }

                    if (demoUser.StaffProfile == null)
                    {
                        // 2. Create Profile if missing
                        demoUser.StaffProfile = new StaffProfile 
                        { 
                            Id = Guid.NewGuid(),
                            TenantId = demoUser.TenantId,
                            UserId = demoUser.Id,
                            StaffNo = "DEMO01",
                            PinCode = "123456",
                            HourlyWage = 150 
                        };
                        _context.StaffProfiles.Add(demoUser.StaffProfile);
                        await _context.SaveChangesAsync();
                    }
                    
                    staffProfile = demoUser.StaffProfile;
                }
                
                if (staffProfile == null) return Unauthorized("Geçersiz PIN");
            }

            // Check Status
            var today = DateTime.UtcNow.Date;
            var activeSession = await _context.TimeEntries
                .Where(te => te.StaffId == staffProfile.Id && te.Date == today && te.ClockOut == null)
                .OrderByDescending(te => te.ClockIn)
                .FirstOrDefaultAsync();

            return Ok(new StaffStatusDto
            {
                UserId = staffProfile.UserId,
                StaffId = staffProfile.Id,
                FullName = staffProfile.User.FullName ?? staffProfile.User.Username,
                FullNameAr = staffProfile.User.FullNameAr,
                IsClockedIn = activeSession != null,
                ClockInTime = activeSession?.ClockIn
            });
        }

        [HttpPost("clock-action")]
        public async Task<IActionResult> ClockAction([FromBody] ClockActionRequest request)
        {
            bool success = false;
            
            if (request.Action == "IN")
            {
                success = await _timeTrackingService.ClockInAsync(request.UserId);
            }
            else
            {
                success = await _timeTrackingService.ClockOutAsync(request.UserId);
            }

            if (!success) return BadRequest("İşlem başarısız.");
            
            return Ok(new { success = true });
        }
    }

    public class PinRequest
    {
        public string Pin { get; set; }
    }

    public class ClockActionRequest
    {
        public Guid UserId { get; set; }
        public string Action { get; set; } // IN or OUT
    }

    public class StaffStatusDto
    {
        public Guid UserId { get; set; }
        public Guid StaffId { get; set; }
        public string FullName { get; set; }
        public string? FullNameAr { get; set; }
        public bool IsClockedIn { get; set; }
        public DateTime? ClockInTime { get; set; }
    }
}
