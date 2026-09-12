using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly IBranchContext _branchContext;

        public NotificationsController(PosDbContext context, IBranchContext branchContext)
        {
            _context = context;
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        }

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
            => (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

        // GET: api/notifications?role=Kitchen&unreadOnly=true&limit=50
        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] string? role = null,
            [FromQuery] bool unreadOnly = false,
            [FromQuery] int limit = 50,
            CancellationToken ct = default)
        {
            // Notifications are branch operational data — the feed follows the active branch.
            var branchId = await GetCurrentBranchIdAsync(ct);
            var query = _context.Notifications.AsNoTracking()
                .Where(n => n.BranchId == branchId);

            if (!string.IsNullOrEmpty(role))
                query = query.Where(n => n.TargetRole == null || n.TargetRole == role || n.TargetRole.Contains(role));

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(Math.Min(limit, 200))
                .Select(n => new
                {
                    n.Id,
                    Type = n.Type.ToString(),
                    n.Title,
                    n.Message,
                    n.ReferenceId,
                    n.TargetRole,
                    n.IsRead,
                    n.CreatedAt
                })
                .ToListAsync(ct);

            return Ok(notifications);
        }

        // PUT: api/notifications/{id}/read
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null) return NotFound();

            notification.IsRead = true;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PUT: api/notifications/read-all
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead([FromQuery] string? role = null, CancellationToken ct = default)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var query = _context.Notifications.Where(n => !n.IsRead && n.BranchId == branchId);
            if (!string.IsNullOrEmpty(role))
                query = query.Where(n => n.TargetRole == null || n.TargetRole == role || n.TargetRole.Contains(role));

            await query.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
            return NoContent();
        }

        // GET: api/notifications/unread-count
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount([FromQuery] string? role = null, CancellationToken ct = default)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var query = _context.Notifications.Where(n => !n.IsRead && n.BranchId == branchId);
            if (!string.IsNullOrEmpty(role))
                query = query.Where(n => n.TargetRole == null || n.TargetRole == role || n.TargetRole.Contains(role));

            var count = await query.CountAsync(ct);
            return Ok(new { count });
        }
    }
}
