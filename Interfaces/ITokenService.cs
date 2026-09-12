using System.Security.Claims;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(UserTokenPayload payload);
        /// <summary>Issue an access token with a caller-supplied lifetime. Used by the legacy
        /// web flow that needs a 24-h flat token (no refresh).</summary>
        string GenerateAccessToken(UserTokenPayload payload, TimeSpan lifetime);
        string GenerateRefreshToken();
        string HashRefreshToken(string rawToken);
        ClaimsPrincipal? ValidateAccessToken(string token);
    }
}
