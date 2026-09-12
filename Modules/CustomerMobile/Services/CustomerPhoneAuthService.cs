using System.Security.Cryptography;
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
    public interface ICustomerPhoneAuthService
    {
        Task<CustomerStartAuthResponse> StartAuthAsync(CustomerStartAuthRequest request, CancellationToken ct);
        Task<CustomerVerifyAuthResponse> VerifyAuthAsync(CustomerVerifyAuthRequest request, CancellationToken ct);
        Task<CustomerMeDto> GetCurrentAsync(CancellationToken ct);
        Task<CustomerCompleteProfileResponse> CompleteProfileAsync(CustomerCompleteProfileRequest request, CancellationToken ct);
    }

    public class CustomerPhoneAuthService : ICustomerPhoneAuthService
    {
        // Phase 1: SMS provider integration is not in place yet. Until it's wired up,
        // every issued OTP is the fixed value below (per product spec). Toggle the
        // configuration flag `CustomerMobile:UseFixedOtp` to false once the SMS
        // provider is integrated — the rest of the flow stays unchanged.
        private const string FixedOtpForDevPhase = "0000";
        private const int OtpExpiryMinutes = 5;
        private const int MaxOtpRequestsPerHour = 5;
        private const int CustomerNumberMaxAttempts = 5;

        private readonly PosDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICustomerMobileContext _mobileContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CustomerPhoneAuthService> _logger;

        public CustomerPhoneAuthService(
            PosDbContext context,
            ITokenService tokenService,
            ITenantResolver tenantResolver,
            ICustomerMobileContext mobileContext,
            IConfiguration configuration,
            ILogger<CustomerPhoneAuthService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CustomerStartAuthResponse> StartAuthAsync(CustomerStartAuthRequest request, CancellationToken ct)
        {
            var phone = NormalizePhone(request.PhoneNumber);
            await EnforceRateLimitAsync(phone, ct);

            var customer = await _context.CustomerAccounts
                .FirstOrDefaultAsync(c => c.PhoneNumber == phone || c.MobileNumber == phone, ct);

            // Caller-facing flag is set BEFORE we touch the row: a re-activated soft-deleted
            // account is still "existing" from the user's point of view.
            var isExistingCustomer = customer != null;

            if (customer == null)
            {
                customer = await CreatePhoneOnlyCustomerAsync(phone, ct);
            }
            else
            {
                if (customer.IsDeleted)
                {
                    customer.IsDeleted = false;
                    customer.IsActive = true;
                    customer.DeletedAt = null;
                }

                if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
                    customer.PhoneNumber = phone;
            }

            var otp = new CustomerOtp
            {
                Id = Guid.NewGuid(),
                TenantId = customer.TenantId,
                CustomerId = customer.Id,
                PhoneNumber = phone,
                OtpCode = GenerateOtpCode(),
                Purpose = CustomerOtpPurposes.PhoneLogin,
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes)
            };

            _context.CustomerOtps.Add(otp);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Customer auth started for {Phone} (existing={Existing})", phone, isExistingCustomer);
            return new CustomerStartAuthResponse
            {
                PhoneNumber = phone,
                IsExistingCustomer = isExistingCustomer,
                IsPhoneVerified = customer.IsVerified,
                IsProfileCompleted = customer.IsProfileCompleted
            };
        }

        public async Task<CustomerVerifyAuthResponse> VerifyAuthAsync(CustomerVerifyAuthRequest request, CancellationToken ct)
        {
            var phone = NormalizePhone(request.PhoneNumber);
            var customer = await _context.CustomerAccounts
                .FirstOrDefaultAsync(c => c.PhoneNumber == phone || c.MobileNumber == phone, ct)
                ?? throw new UnauthorizedException("Invalid verification code.");

            if (!customer.IsActive || customer.IsDeleted)
                throw new UnauthorizedException("Customer account is inactive.");

            var otp = await _context.CustomerOtps
                .Where(o => o.CustomerId == customer.Id
                            && o.Purpose == CustomerOtpPurposes.PhoneLogin
                            && o.UsedAt == null)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(ct)
                ?? throw new UnauthorizedException("Invalid verification code.");

            if (otp.ExpiresAt <= DateTime.UtcNow)
                throw new UnauthorizedException("Verification code has expired.");

            if (!string.Equals(otp.OtpCode, request.Otp.Trim(), StringComparison.Ordinal))
                throw new UnauthorizedException("Invalid verification code.");

            otp.UsedAt = DateTime.UtcNow;
            customer.IsVerified = true;
            customer.LastLoginDate = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
                customer.PhoneNumber = phone;

            await RevokeDeviceTokensAsync(customer.Id, request.DeviceId, ct);
            await UpsertDeviceAsync(customer, request, ct);

            var accessExpiryMinutes = GetAccessTokenExpiryMinutes();
            var accessToken = _tokenService.GenerateAccessToken(new UserTokenPayload
            {
                UserId = customer.Id,
                TenantId = customer.TenantId,
                Username = customer.PhoneNumber ?? phone,
                Role = AppRoleNames.Customer,
                DeviceId = NormalizeNullable(request.DeviceId),
                FullName = $"{customer.FirstName} {customer.LastName}".Trim()
            });

            var rawRefreshToken = _tokenService.GenerateRefreshToken();
            var refreshTokenHash = _tokenService.HashRefreshToken(rawRefreshToken);
            var refreshExpiresAt = DateTime.UtcNow.AddHours(GetRefreshTokenExpiryHours());

            _context.CustomerRefreshTokens.Add(new CustomerRefreshToken
            {
                Id = Guid.NewGuid(),
                TenantId = customer.TenantId,
                CustomerId = customer.Id,
                TokenHash = refreshTokenHash,
                DeviceId = NormalizeNullable(request.DeviceId),
                DeviceName = NormalizeNullable(request.DeviceName),
                ExpiresAt = refreshExpiresAt
            });

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Customer {CustomerId} verified OTP for phone {Phone}", customer.Id, phone);

            return new CustomerVerifyAuthResponse
            {
                Token = accessToken,
                ExpiresIn = accessExpiryMinutes * 60,
                RefreshToken = rawRefreshToken,
                RefreshTokenExpiresAt = refreshExpiresAt,
                Customer = new CustomerSessionDto
                {
                    Id = customer.Id,
                    PhoneNumber = customer.PhoneNumber ?? phone,
                    IsPhoneVerified = customer.IsVerified,
                    IsProfileCompleted = customer.IsProfileCompleted
                },
                NextAction = customer.IsProfileCompleted
                    ? CustomerNextActions.Home
                    : CustomerNextActions.CompleteProfile
            };
        }

        public async Task<CustomerMeDto> GetCurrentAsync(CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            var customer = await _context.CustomerAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId, ct)
                ?? throw new NotFoundException("Customer profile was not found.");

            return new CustomerMeDto
            {
                Id = customer.Id,
                PhoneNumber = customer.PhoneNumber ?? customer.MobileNumber,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                BirthDate = customer.BirthDate,
                IsPhoneVerified = customer.IsVerified,
                IsProfileCompleted = customer.IsProfileCompleted
            };
        }

        public async Task<CustomerCompleteProfileResponse> CompleteProfileAsync(CustomerCompleteProfileRequest request, CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            var customer = await _context.CustomerAccounts
                .FirstOrDefaultAsync(c => c.Id == customerId, ct)
                ?? throw new NotFoundException("Customer profile was not found.");

            customer.FirstName = request.FirstName.Trim();
            customer.LastName = request.LastName.Trim();

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var email = request.Email.Trim().ToLowerInvariant();
                if (await _context.CustomerAccounts.AnyAsync(c => c.Id != customer.Id && c.Email.ToLower() == email, ct))
                    throw new ConflictException("Email is already registered.");
                customer.Email = email;
            }

            if (request.BirthDate.HasValue)
                customer.BirthDate = DateTime.SpecifyKind(request.BirthDate.Value.Date, DateTimeKind.Utc);

            customer.IsProfileCompleted = true;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Customer {CustomerId} completed profile", customer.Id);
            return new CustomerCompleteProfileResponse
            {
                IsPhoneVerified = customer.IsVerified,
                IsProfileCompleted = true,
                NextAction = CustomerNextActions.Home
            };
        }

        private async Task EnforceRateLimitAsync(string phone, CancellationToken ct)
        {
            var since = DateTime.UtcNow.AddHours(-1);
            var recentCount = await _context.CustomerOtps
                .Where(o => o.PhoneNumber == phone && o.CreatedAt >= since)
                .CountAsync(ct);

            if (recentCount >= MaxOtpRequestsPerHour)
            {
                _logger.LogWarning("OTP rate limit hit for phone {Phone}", phone);
                throw new ValidationException("Too many OTP requests. Please try again later.");
            }
        }

        private async Task<CustomerAccount> CreatePhoneOnlyCustomerAsync(string phone, CancellationToken ct)
        {
            var customer = new CustomerAccount
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                CustomerNumber = await GenerateCustomerNumberAsync(ct),
                FirstName = string.Empty,
                LastName = string.Empty,
                Email = string.Empty,
                MobileNumber = phone,
                PhoneNumber = phone,
                PasswordHash = PasswordHelper.Hash(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")),
                PasswordSalt = "bcrypt",
                IsVerified = false,
                IsActive = true,
                IsProfileCompleted = false,
                PreferredLanguage = CustomerPreferredLanguage.En
            };

            _context.CustomerAccounts.Add(customer);
            return customer;
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

        private string GenerateOtpCode()
        {
            if (_configuration.GetValue<bool?>("CustomerMobile:UseFixedOtp") ?? true)
                return FixedOtpForDevPhase;

            // Cryptographically-random 4-digit OTP. Switch to 6 digits by widening
            // the modulus when the SMS provider integration lands.
            var buffer = new byte[4];
            RandomNumberGenerator.Fill(buffer);
            var value = BitConverter.ToUInt32(buffer, 0) % 10000;
            return value.ToString("D4");
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

        private async Task UpsertDeviceAsync(CustomerAccount customer, CustomerVerifyAuthRequest request, CancellationToken ct)
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

        private int GetAccessTokenExpiryMinutes()
            => (_tokenService as TokenService)?.GetAccessTokenExpiryMinutes() ?? 15;

        private int GetRefreshTokenExpiryHours()
            => (_tokenService as TokenService)?.GetRefreshTokenExpiryHours() ?? 8;

        private static CustomerDeviceType ParseDeviceType(string? value)
            => Enum.TryParse<CustomerDeviceType>(value, true, out var parsed)
                ? parsed
                : CustomerDeviceType.Unknown;

        private static string NormalizePhone(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new ValidationException("Phone number is required.");

            var trimmed = raw.Trim();
            var hasPlus = trimmed.StartsWith('+');
            var digits = new string(trimmed.Where(char.IsDigit).ToArray());
            if (digits.Length < 6 || digits.Length > 18)
                throw new ValidationException("Phone number is invalid.");

            return hasPlus ? "+" + digits : digits;
        }

        private static string? NormalizeNullable(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
