using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Security
{
    /// <summary>
    /// Sync claim-based access to the current authenticated user.
    /// Wraps the same NameIdentifier / Role claim lookups previously duplicated across
    /// AdminCashierBalances, CashierBalance, CancelLog, WasteLog controllers.
    /// For DB-backed username-fallback lookups, keep using <see cref="Services.ICurrentUserResolver"/>.
    /// </summary>
    public interface ICurrentUserAccessor
    {
        Guid UserId { get; }
        Guid? UserIdOrNull { get; }
        UserRole? Role { get; }
        bool IsAdminOrManager { get; }
    }

    public sealed class CurrentUserAccessor : ICurrentUserAccessor
    {
        private readonly IHttpContextAccessor _http;

        public CurrentUserAccessor(IHttpContextAccessor http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public Guid UserId => UserIdOrNull ?? Guid.Empty;

        public Guid? UserIdOrNull
        {
            get
            {
                var raw = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
                return Guid.TryParse(raw, out var id) ? id : null;
            }
        }

        public UserRole? Role
        {
            get
            {
                var user = _http.HttpContext?.User;
                if (user == null) return null;

                var raw = user.FindFirstValue(ClaimTypes.Role)
                          ?? user.Claims.FirstOrDefault(c => c.Type == "role")?.Value;

                return Enum.TryParse<UserRole>(raw, out var role) ? role : null;
            }
        }

        public bool IsAdminOrManager
        {
            get
            {
                var user = _http.HttpContext?.User;
                return user != null
                    && (user.IsInRole(AppRoleNames.Admin) || user.IsInRole(AppRoleNames.Manager));
            }
        }
    }
}
