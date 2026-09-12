using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateAccessToken(UserTokenPayload payload)
            => GenerateAccessToken(payload, TimeSpan.FromMinutes(GetAccessTokenExpiryMinutes()));

        public string GenerateAccessToken(UserTokenPayload payload, TimeSpan lifetime)
        {
            if (lifetime <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(lifetime), "Token lifetime must be positive.");

            var now = DateTime.UtcNow;
            var expiresAt = now.Add(lifetime);
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtSecret()));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, payload.UserId.ToString()),
                new("tenant_id", payload.TenantId.ToString()),
                new(ClaimTypes.Name, payload.Username),
            };

            // SuperAdmin is a strict superset of Admin. Emit Admin as the primary
            // role claim so every existing Admin authorization surface — policies,
            // [Authorize(Roles="Admin")], IsInRole, and role-enum comparisons that
            // read the first role claim — treats SuperAdmin exactly as Admin. The
            // SuperAdmin claim is added alongside so future sensitive permissions
            // can gate on IsInRole("SuperAdmin") without loosening anything today.
            if (string.Equals(payload.Role, Security.AppRoleNames.SuperAdmin, StringComparison.Ordinal))
            {
                claims.Add(new Claim(ClaimTypes.Role, Security.AppRoleNames.Admin));
                claims.Add(new Claim(ClaimTypes.Role, Security.AppRoleNames.SuperAdmin));
            }
            else
            {
                claims.Add(new Claim(ClaimTypes.Role, payload.Role));
            }

            if (!string.IsNullOrWhiteSpace(payload.DeviceId))
            {
                claims.Add(new Claim("device_id", payload.DeviceId));
            }

            if (!string.IsNullOrWhiteSpace(payload.FullName))
            {
                claims.Add(new Claim("full_name", payload.FullName));
            }

            if (!string.IsNullOrWhiteSpace(payload.FullNameAr))
            {
                claims.Add(new Claim("full_name_ar", payload.FullNameAr));
            }

            var token = new JwtSecurityToken(
                issuer: GetJwtIssuer(),
                audience: GetJwtAudience(),
                claims: claims,
                notBefore: now,
                expires: expiresAt,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return WebEncoders.Base64UrlEncode(bytes);
        }

        public string HashRefreshToken(string rawToken)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

        public ClaimsPrincipal? ValidateAccessToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = GetJwtIssuer(),
                ValidAudience = GetJwtAudience(),
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtSecret())),
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
            }
            catch
            {
                return null;
            }
        }

        public int GetAccessTokenExpiryMinutes()
            => _configuration.GetValue<int?>("Jwt:AccessTokenExpiryMinutes") ?? 15;

        public int GetRefreshTokenExpiryHours()
            => _configuration.GetValue<int?>("Jwt:RefreshTokenExpiryHours") ?? 8;

        private string GetJwtSecret()
            => _configuration["Jwt:Secret"]
                ?? _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT secret is not configured.");

        private string GetJwtIssuer()
            => _configuration["Jwt:Issuer"] ?? "RestaurantPos";

        private string GetJwtAudience()
            => _configuration["Jwt:Audience"] ?? "RestaurantPosClient";
    }
}
