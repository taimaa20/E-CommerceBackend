using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs.Auth
{
    /// <summary>Input for POST /api/auth/login (legacy flat-token flow used by the web client).
    /// The mobile flow uses <see cref="LoginRequest"/> (with device binding + refresh token).</summary>
    public class LegacyLoginRequest
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string Username { get; set; } = string.Empty;

        [Required, StringLength(128, MinimumLength = 1)]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>Response shape kept identical to the original AuthController contract
    /// (Token, TenantId, Username, FullName, FullNameAr, Role, RoleName, UserId) so web frontends
    /// are not affected by the refactor.</summary>
    public class LegacyLoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? FullNameAr { get; set; }
        public UserRole Role { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }
}
