using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs.Auth;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public class AuthService : IAuthService
    {
        private static readonly string DummyPasswordHash = PasswordHelper.Hash("InvalidPassword123!");

        // Legacy web token TTL. Kept at 24 h to preserve the original AuthController contract;
        // the mobile flow uses ITokenService.GenerateAccessToken default (15 min) + refresh.
        private static readonly TimeSpan LegacyAccessTokenLifetime = TimeSpan.FromHours(24);

        private readonly PosDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(
            PosDbContext context,
            ITokenService tokenService,
            ILogger<AuthService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _tokenService = tokenService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            try
            {
                var username = TrimAndCap(request.Username, 100);
                var password = TrimAndCap(request.Password, 128);
                var deviceId = TrimAndCap(request.DeviceId, 200);
                var deviceName = TrimAndCap(request.DeviceName, 200);

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return null;
                }

                var normalizedUsername = username.ToLowerInvariant();
                var user = await _context.Users
                    .Include(u => u.StaffProfile)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername);

                var passwordVerified = PasswordHelper.Verify(password, user?.PasswordHash ?? DummyPasswordHash);
                if (user == null || !passwordVerified)
                {
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(deviceId))
                {
                    var existingDeviceTokens = await _context.RefreshTokens
                        .Where(rt => rt.UserId == user.Id
                            && rt.DeviceId == deviceId
                            && rt.RevokedAt == null
                            && rt.ExpiresAt > DateTime.UtcNow)
                        .ToListAsync();

                    foreach (var existingToken in existingDeviceTokens)
                    {
                        existingToken.RevokedAt = DateTime.UtcNow;
                    }
                }

                var accessToken = _tokenService.GenerateAccessToken(BuildPayload(user, deviceId));
                var rawRefreshToken = _tokenService.GenerateRefreshToken();
                var hashedRefreshToken = _tokenService.HashRefreshToken(rawRefreshToken);
                var refreshExpiresAt = DateTime.UtcNow.AddHours(GetRefreshTokenExpiryHours());

                _context.RefreshTokens.Add(new RefreshToken
                {
                    UserId = user.Id,
                    TokenHash = hashedRefreshToken,
                    DeviceId = deviceId,
                    DeviceName = deviceName,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = refreshExpiresAt
                });

                await _context.SaveChangesAsync();

                return BuildLoginResponse(user, accessToken, rawRefreshToken, refreshExpiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mobile login failed.");
                return null;
            }
        }

        public async Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request)
        {
            try
            {
                var rawRefreshToken = TrimAndCap(request.RefreshToken, 512);
                var deviceId = TrimAndCap(request.DeviceId, 200);
                if (string.IsNullOrWhiteSpace(rawRefreshToken))
                {
                    return null;
                }

                var hashedRefreshToken = _tokenService.HashRefreshToken(rawRefreshToken);
                var storedToken = await _context.RefreshTokens
                    .Include(rt => rt.User)
                        .ThenInclude(u => u.StaffProfile)
                    .FirstOrDefaultAsync(rt => rt.TokenHash == hashedRefreshToken);

                if (storedToken == null)
                {
                    return null;
                }

                if (storedToken.RevokedAt.HasValue)
                {
                    _logger.LogWarning("Refresh token reuse detected for user {UserId}. Revoking all user tokens.", storedToken.UserId);
                    await RevokeAllUserTokensAsync(storedToken.UserId);
                    return null;
                }

                if (storedToken.ExpiresAt <= DateTime.UtcNow)
                {
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(deviceId) && !string.Equals(storedToken.DeviceId, deviceId, StringComparison.Ordinal))
                {
                    return null;
                }

                if (storedToken.User == null)
                {
                    return null;
                }

                var newRawRefreshToken = _tokenService.GenerateRefreshToken();
                var newHashedRefreshToken = _tokenService.HashRefreshToken(newRawRefreshToken);
                var now = DateTime.UtcNow;
                var refreshExpiresAt = now.AddHours(GetRefreshTokenExpiryHours());

                storedToken.RevokedAt = now;
                storedToken.ReplacedByTokenHash = newHashedRefreshToken;

                _context.RefreshTokens.Add(new RefreshToken
                {
                    UserId = storedToken.UserId,
                    TokenHash = newHashedRefreshToken,
                    DeviceId = storedToken.DeviceId,
                    DeviceName = storedToken.DeviceName,
                    CreatedAt = now,
                    ExpiresAt = refreshExpiresAt
                });

                await _context.SaveChangesAsync();

                var accessToken = _tokenService.GenerateAccessToken(BuildPayload(storedToken.User, storedToken.DeviceId));
                return BuildLoginResponse(storedToken.User, accessToken, newRawRefreshToken, refreshExpiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mobile refresh failed.");
                return null;
            }
        }

        public async Task<bool> LogoutAsync(LogoutRequest request)
        {
            try
            {
                var rawRefreshToken = TrimAndCap(request.RefreshToken, 512);
                if (string.IsNullOrWhiteSpace(rawRefreshToken))
                {
                    return false;
                }

                var hashedRefreshToken = _tokenService.HashRefreshToken(rawRefreshToken);
                var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hashedRefreshToken);
                if (storedToken == null)
                {
                    return false;
                }

                storedToken.RevokedAt ??= DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mobile logout failed.");
                return false;
            }
        }

        public async Task RevokeAllUserTokensAsync(Guid userId)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
                .ToListAsync();

            if (activeTokens.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var token in activeTokens)
            {
                token.RevokedAt = now;
            }

            await _context.SaveChangesAsync();
        }

        private LoginResponse BuildLoginResponse(User user, string accessToken, string rawRefreshToken, DateTime refreshExpiresAt)
        {
            var accessExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenExpiryMinutes());
            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = rawRefreshToken,
                AccessTokenExpiresAt = accessExpiresAt,
                RefreshTokenExpiresAt = refreshExpiresAt,
                User = new AuthUserDto
                {
                    Id = user.Id,
                    TenantId = user.TenantId,
                    Username = user.Username,
                    FullName = user.FullName,
                    FullNameAr = user.FullNameAr,
                    Phone = user.StaffProfile?.Phone,
                    Role = user.Role.ToString(),
                    EmployeeNumber = user.StaffProfile?.StaffNo,
                    Image = BuildAbsoluteUrl(user.StaffProfile?.PhotoUrl),
                    ImageUrl = BuildAbsoluteUrl(user.StaffProfile?.PhotoUrl)
                }
            };
        }

        private UserTokenPayload BuildPayload(User user, string? deviceId)
            => new()
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                Username = user.Username,
                Role = user.Role.ToString(),
                DeviceId = deviceId,
                FullName = user.FullName,
                FullNameAr = user.FullNameAr
            };

        private int GetAccessTokenExpiryMinutes()
            => ( _tokenService as TokenService)?.GetAccessTokenExpiryMinutes() ?? 15;

        private int GetRefreshTokenExpiryHours()
            => (_tokenService as TokenService)?.GetRefreshTokenExpiryHours() ?? 8;

        private string? BuildAbsoluteUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out _))
            {
                return value;
            }

            var request = _httpContextAccessor.HttpContext?.Request;
            if (request == null)
            {
                return value;
            }

            var path = value.StartsWith('/') ? value : $"/{value}";
            return $"{request.Scheme}://{request.Host}{path}";
        }

        private static string? TrimAndCap(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        public async Task<LegacyLoginResponse?> LegacyLoginAsync(
            LegacyLoginRequest request,
            CancellationToken cancellationToken = default)
        {
            var username = TrimAndCap(request.Username, 100);
            var password = TrimAndCap(request.Password, 128);

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            var normalizedUsername = username.ToLowerInvariant();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername, cancellationToken);

            // Always run BCrypt — even on missing user — to keep response time constant
            // and avoid username-enumeration via timing.
            var passwordVerified = PasswordHelper.Verify(password, user?.PasswordHash ?? DummyPasswordHash);
            if (user == null || !passwordVerified)
            {
                return null;
            }

            var accessToken = _tokenService.GenerateAccessToken(
                new UserTokenPayload
                {
                    UserId = user.Id,
                    TenantId = user.TenantId,
                    Username = user.Username,
                    Role = user.Role.ToString(),
                    FullName = user.FullName,
                    FullNameAr = user.FullNameAr
                },
                LegacyAccessTokenLifetime);

            return new LegacyLoginResponse
            {
                Token = accessToken,
                TenantId = user.TenantId.ToString(),
                Username = user.Username,
                FullName = user.FullName,
                FullNameAr = user.FullNameAr,
                Role = user.Role,
                RoleName = user.Role.ToString(),
                UserId = user.Id.ToString()
            };
        }
    }
}
