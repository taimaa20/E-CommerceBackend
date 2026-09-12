using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Interfaces;

namespace RestaurantPos.Api.Services
{
    public class CurrentUserResolver : ICurrentUserResolver
    {
        private readonly PosDbContext _context;

        public CurrentUserResolver(PosDbContext context)
        {
            _context = context;
        }

        public async Task<Guid?> ResolveUserIdAsync(ClaimsPrincipal principal)
        {
            var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("UserId")
                ?? principal.FindFirstValue("userId");

            if (Guid.TryParse(userIdValue, out var userId))
            {
                return userId;
            }

            var username = principal.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            var normalizedUsername = username.Trim().ToLowerInvariant();
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.Username.ToLower() == normalizedUsername)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync();
        }
    }
}
