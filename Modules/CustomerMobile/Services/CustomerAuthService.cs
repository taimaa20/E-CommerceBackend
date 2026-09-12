using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerAuthService : ICustomerAuthService
    {
        private const int CustomerNumberMaxAttempts = 5;
        private const string VerifyPurpose = "verify-account";
        private const string ResetPurpose = "reset-password";
        private static readonly string DummyPasswordHash = PasswordHelper.Hash("InvalidPassword123!");

        private readonly PosDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly ICustomerOtpProvider _otpProvider;
        private readonly ITenantResolver _tenantResolver;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CustomerAuthService> _logger;

        public CustomerAuthService(
            PosDbContext context,
            ITokenService tokenService,
            ICustomerOtpProvider otpProvider,
            ITenantResolver tenantResolver,
            IConfiguration configuration,
            ILogger<CustomerAuthService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _otpProvider = otpProvider ?? throw new ArgumentNullException(nameof(otpProvider));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CustomerProfileDto> RegisterAsync(CustomerRegisterRequest request, CancellationToken ct)
        {
            ValidatePasswordPolicy(request.Password);
            var email = NormalizeEmail(request.Email);
            var mobile = NormalizeMobile(request.MobileNumber);

            if (await _context.CustomerAccounts.AnyAsync(c => c.Email.ToLower() == email, ct))
                throw new ConflictException("Email is already registered.");

            if (await _context.CustomerAccounts.AnyAsync(c => c.MobileNumber == mobile, ct))
                throw new ConflictException("Mobile number is already registered.");

            var customer = new CustomerAccount
            {
                Id = Guid.NewGuid(),
                TenantId = ResolveTenantId(),
                CustomerNumber = await GenerateCustomerNumberAsync(ct),
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                MobileNumber = mobile,
                PasswordHash = PasswordHelper.Hash(request.Password),
                PasswordSalt = "bcrypt",
                PreferredLanguage = ParseLanguage(request.PreferredLanguage),
                IsVerified = !RequireOtpVerification()
            };

            _context.CustomerAccounts.Add(customer);
            await _context.SaveChangesAsync(ct);

            if (!customer.IsVerified)
                await _otpProvider.SendAsync(customer, VerifyPurpose, ct);

            return MapProfile(customer);
        }

        public async Task<CustomerLoginResponse> LoginAsync(CustomerLoginRequest request, CancellationToken ct)
        {
            var identifier = NormalizeIdentifier(request.Identifier);
            var customer = await FindByIdentifierAsync(identifier, tracking: true, ct);
            var passwordVerified = PasswordHelper.Verify(request.Password, customer?.PasswordHash ?? DummyPasswordHash);

            if (customer == null || !passwordVerified)
            {
                _logger.LogWarning("Customer login failed for identifier {Identifier}", identifier);
                throw new UnauthorizedException("Invalid credentials.");
            }

            ValidateCanLogin(customer);
            await RevokeDeviceTokensAsync(customer.Id, request.DeviceId, ct);
            await UpsertDeviceAsync(customer, request, ct);

            customer.LastLoginDate = DateTime.UtcNow;
            var response = IssueTokens(customer, request.DeviceId, request.DeviceName);
            await _context.SaveChangesAsync(ct);
            return response;
        }

        public async Task<CustomerLoginResponse> RefreshAsync(CustomerRefreshTokenRequest request, CancellationToken ct)
        {
            var rawToken = NormalizeToken(request.RefreshToken);
            var tokenHash = _tokenService.HashRefreshToken(rawToken);
            var storedToken = await _context.CustomerRefreshTokens
                .Include(t => t.Customer)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

            if (storedToken == null)
                throw new UnauthorizedException("Session expired.");

            if (storedToken.RevokedAt.HasValue)
            {
                await RevokeAllTokensAsync(storedToken.CustomerId, ct);
                throw new UnauthorizedException("Session expired.");
            }

            if (storedToken.ExpiresAt <= DateTime.UtcNow || storedToken.Customer == null)
                throw new UnauthorizedException("Session expired.");

            if (!string.IsNullOrWhiteSpace(request.DeviceId) &&
                !string.Equals(storedToken.DeviceId, request.DeviceId.Trim(), StringComparison.Ordinal))
            {
                throw new UnauthorizedException("Session expired.");
            }

            ValidateCanLogin(storedToken.Customer);
            storedToken.RevokedAt = DateTime.UtcNow;
            var response = IssueTokens(storedToken.Customer, storedToken.DeviceId, storedToken.DeviceName, storedToken);
            await _context.SaveChangesAsync(ct);
            return response;
        }

        public async Task LogoutAsync(CustomerLogoutRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return;

            var tokenHash = _tokenService.HashRefreshToken(NormalizeToken(request.RefreshToken));
            var token = await _context.CustomerRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
            if (token == null)
                return;

            token.RevokedAt ??= DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        public async Task ForgotPasswordAsync(CustomerForgotPasswordRequest request, CancellationToken ct)
        {
            var customer = await FindByIdentifierAsync(NormalizeIdentifier(request.Identifier), tracking: false, ct);
            if (customer != null && customer.IsActive && !customer.IsDeleted)
                await _otpProvider.SendAsync(customer, ResetPurpose, ct);
        }

        public async Task ResetPasswordAsync(CustomerResetPasswordRequest request, CancellationToken ct)
        {
            ValidatePasswordPolicy(request.NewPassword);
            var customer = await FindByIdentifierAsync(NormalizeIdentifier(request.Identifier), tracking: true, ct)
                ?? throw new UnauthorizedException("Invalid verification code.");

            if (!await _otpProvider.VerifyAsync(customer, ResetPurpose, request.OtpCode.Trim(), ct))
                throw new UnauthorizedException("Invalid verification code.");

            customer.PasswordHash = PasswordHelper.Hash(request.NewPassword);
            await RevokeAllTokensAsync(customer.Id, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task VerifyOtpAsync(CustomerVerifyOtpRequest request, CancellationToken ct)
        {
            var customer = await FindByIdentifierAsync(NormalizeIdentifier(request.Identifier), tracking: true, ct)
                ?? throw new UnauthorizedException("Invalid verification code.");

            if (!await _otpProvider.VerifyAsync(customer, VerifyPurpose, request.OtpCode.Trim(), ct))
                throw new UnauthorizedException("Invalid verification code.");

            customer.IsVerified = true;
            await _context.SaveChangesAsync(ct);
        }

        private CustomerLoginResponse IssueTokens(
            CustomerAccount customer,
            string? deviceId,
            string? deviceName,
            CustomerRefreshToken? replacedToken = null)
        {
            var accessToken = _tokenService.GenerateAccessToken(new UserTokenPayload
            {
                UserId = customer.Id,
                TenantId = customer.TenantId,
                Username = customer.Email,
                Role = AppRoleNames.Customer,
                DeviceId = NormalizeNullable(deviceId),
                FullName = $"{customer.FirstName} {customer.LastName}".Trim()
            });

            var rawRefreshToken = _tokenService.GenerateRefreshToken();
            var refreshTokenHash = _tokenService.HashRefreshToken(rawRefreshToken);
            var refreshExpiresAt = DateTime.UtcNow.AddHours(GetRefreshTokenExpiryHours());

            if (replacedToken != null)
                replacedToken.ReplacedByTokenHash = refreshTokenHash;

            _context.CustomerRefreshTokens.Add(new CustomerRefreshToken
            {
                Id = Guid.NewGuid(),
                TenantId = customer.TenantId,
                CustomerId = customer.Id,
                TokenHash = refreshTokenHash,
                DeviceId = NormalizeNullable(deviceId),
                DeviceName = NormalizeNullable(deviceName),
                ExpiresAt = refreshExpiresAt
            });

            return new CustomerLoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = rawRefreshToken,
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenExpiryMinutes()),
                RefreshTokenExpiresAt = refreshExpiresAt,
                Customer = MapProfile(customer)
            };
        }

        private async Task<CustomerAccount?> FindByIdentifierAsync(string identifier, bool tracking, CancellationToken ct)
        {
            var query = _context.CustomerAccounts.AsQueryable();
            if (!tracking)
                query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync(
                c => c.Email.ToLower() == identifier || c.MobileNumber == identifier,
                ct);
        }

        private async Task RevokeDeviceTokensAsync(Guid customerId, string? deviceId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return;

            var normalizedDeviceId = deviceId.Trim();
            var tokens = await _context.CustomerRefreshTokens
                .Where(t => t.CustomerId == customerId &&
                    t.DeviceId == normalizedDeviceId &&
                    t.RevokedAt == null &&
                    t.ExpiresAt > DateTime.UtcNow)
                .ToListAsync(ct);

            foreach (var token in tokens)
                token.RevokedAt = DateTime.UtcNow;
        }

        private async Task RevokeAllTokensAsync(Guid customerId, CancellationToken ct)
        {
            var tokens = await _context.CustomerRefreshTokens
                .Where(t => t.CustomerId == customerId && t.RevokedAt == null)
                .ToListAsync(ct);

            foreach (var token in tokens)
                token.RevokedAt = DateTime.UtcNow;
        }

        private async Task UpsertDeviceAsync(CustomerAccount customer, CustomerLoginRequest request, CancellationToken ct)
        {
            var deviceId = NormalizeNullable(request.DeviceId);
            if (deviceId == null)
                return;

            var device = await _context.CustomerDevices
                .FirstOrDefaultAsync(d => d.CustomerId == customer.Id && d.DeviceId == deviceId, ct);

            if (device == null)
            {
                _context.CustomerDevices.Add(new CustomerDevice
                {
                    Id = Guid.NewGuid(),
                    TenantId = customer.TenantId,
                    CustomerId = customer.Id,
                    DeviceId = deviceId,
                    DeviceType = ParseDeviceType(request.DeviceType),
                    FcmToken = NormalizeNullable(request.FcmToken),
                    LastSeenAt = DateTime.UtcNow
                });
                return;
            }

            device.DeviceType = ParseDeviceType(request.DeviceType);
            device.FcmToken = NormalizeNullable(request.FcmToken);
            device.LastSeenAt = DateTime.UtcNow;
        }

        private async Task<string> GenerateCustomerNumberAsync(CancellationToken ct)
        {
            for (var attempt = 0; attempt < CustomerNumberMaxAttempts; attempt++)
            {
                var number = $"C{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(1000, 9999)}";
                if (!await _context.CustomerAccounts.AnyAsync(c => c.CustomerNumber == number, ct))
                    return number;
            }

            return $"C{Guid.NewGuid():N}"[..18];
        }

        private Guid ResolveTenantId()
            => _tenantResolver.GetTenantId();

        private bool RequireOtpVerification()
            => _configuration.GetValue<bool?>("CustomerMobile:RequireOtpVerification") ?? false;

        private int GetAccessTokenExpiryMinutes()
            => (_tokenService as RestaurantPos.Api.Services.TokenService)?.GetAccessTokenExpiryMinutes() ?? 15;

        private int GetRefreshTokenExpiryHours()
            => (_tokenService as RestaurantPos.Api.Services.TokenService)?.GetRefreshTokenExpiryHours() ?? 8;

        private static void ValidateCanLogin(CustomerAccount customer)
        {
            if (!customer.IsActive || customer.IsDeleted)
                throw new UnauthorizedException("Customer account is inactive.");

            if (!customer.IsVerified)
                throw new UnauthorizedException("Customer account is not verified.");
        }

        private static void ValidatePasswordPolicy(string password)
        {
            var valid = password.Length >= 8 &&
                password.Any(char.IsUpper) &&
                password.Any(char.IsLower) &&
                password.Any(char.IsDigit) &&
                password.Any(ch => !char.IsLetterOrDigit(ch));

            if (!valid)
                throw new ValidationException("Password must include uppercase, lowercase, number, and special character.");
        }

        public static CustomerProfileDto MapProfile(CustomerAccount customer)
            => new()
            {
                Id = customer.Id,
                CustomerNumber = customer.CustomerNumber,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                MobileNumber = customer.MobileNumber,
                PreferredLanguage = customer.PreferredLanguage == CustomerPreferredLanguage.Ar ? "ar" : "en",
                IsVerified = customer.IsVerified,
                CreatedAt = customer.CreatedAt,
                LastLoginDate = customer.LastLoginDate
            };

        private static CustomerPreferredLanguage ParseLanguage(string? value)
            => string.Equals(value?.Trim(), "ar", StringComparison.OrdinalIgnoreCase)
                ? CustomerPreferredLanguage.Ar
                : CustomerPreferredLanguage.En;

        private static CustomerDeviceType ParseDeviceType(string? value)
            => Enum.TryParse<CustomerDeviceType>(value, true, out var parsed)
                ? parsed
                : CustomerDeviceType.Unknown;

        private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();
        private static string NormalizeMobile(string value) => value.Trim();
        private static string NormalizeIdentifier(string value) => value.Trim().ToLowerInvariant();
        private static string NormalizeToken(string value) => value.Trim();
        private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class NoOpCustomerOtpProvider : ICustomerOtpProvider
    {
        public Task SendAsync(CustomerAccount customer, string purpose, CancellationToken ct)
            => Task.CompletedTask;

        public Task<bool> VerifyAsync(CustomerAccount customer, string purpose, string otpCode, CancellationToken ct)
            => Task.FromResult(false);
    }
}
