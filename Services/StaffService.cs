using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public class StaffService : IStaffService
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly IHrEmployeeService _hrEmployeeService;
        private readonly ILogger<StaffService> _logger;

        public StaffService(
            PosDbContext context,
            ITenantResolver tenantResolver,
            IHrEmployeeService hrEmployeeService,
            ILogger<StaffService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _hrEmployeeService = hrEmployeeService ?? throw new ArgumentNullException(nameof(hrEmployeeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HrEmployeeDetailDto> CreateAsync(HrEmployeeCreateDto dto, Guid actingUserId, CancellationToken ct)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.Username)) throw new ArgumentException("Username is required.", nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
                throw new ArgumentException("Password is required (min 6 chars).", nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.FullName)) throw new ArgumentException("Full name is required.", nameof(dto));

            var tenantId = _tenantResolver.GetTenantId();
            var username = dto.Username.Trim();

            var exists = await _context.Users.AnyAsync(u => u.Username == username, ct);
            if (exists) throw new InvalidOperationException("Username already in use.");

            var mainBranchId = await _context.Branches
                .AsNoTracking()
                .Where(b => b.IsMainBranch && b.IsActive)
                .Select(b => b.Id)
                .FirstOrDefaultAsync(ct);
            if (mainBranchId == Guid.Empty)
                throw new InvalidOperationException("Main Branch is required before creating users.");

            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Username = username,
                PasswordHash = PasswordHelper.Hash(dto.Password),
                Role = (UserRole)dto.Role,
                FullName = dto.FullName.Trim(),
                FullNameAr = TrimOrNull(dto.FullNameAr),
                MonthlySalary = decimal.Round(dto.MonthlySalary, 2),
                CommissionRate = decimal.Round(dto.CommissionRate, 2)
            };

            var profile = new StaffProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                StaffNo = string.IsNullOrWhiteSpace(dto.StaffNo)
                    ? StaffNumberHelper.BuildGeneratedStaffNumber(username, user.Id)
                    : dto.StaffNo!.Trim(),
                PinCode = string.IsNullOrWhiteSpace(dto.PinCode) ? null : dto.PinCode!.Trim(),
                BloodType = (BloodType)dto.BloodType,
                Phone = TrimOrNull(dto.Phone),
                Address = TrimOrNull(dto.Address),
                PhotoUrl = TrimOrNull(dto.PhotoUrl),
                StartDate = ToUtc(dto.StartDate),
                ContractStatus = (ContractStatus)dto.ContractStatus,
                NetSalary = decimal.Round(dto.NetSalary, 2),
                SgkPremium = decimal.Round(dto.SgkPremium, 2),
                HourlyWage = decimal.Round(dto.HourlyWage, 2),
                WeeklyShiftPattern = TrimOrNull(dto.WeeklyShiftPattern)
            };

            _context.Users.Add(user);
            _context.StaffProfiles.Add(profile);
            _context.UserBranches.Add(new UserBranch
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                BranchId = mainBranchId,
                IsDefault = true,
                CreatedById = ActorOrNull(actingUserId),
                UpdatedById = ActorOrNull(actingUserId)
            });
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Staff hired: user {UserId} / profile {StaffProfileId} by {ActingUserId}.",
                user.Id, profile.Id, actingUserId);

            return await _hrEmployeeService.GetEmployeeAsync(profile.Id, ct)
                ?? throw new InvalidOperationException("Created staff could not be reloaded.");
        }

        public async Task<HrEmployeeDetailDto> UpdateAsync(
            Guid employeeId,
            HrEmployeeUpdateDto dto,
            Guid actingUserId,
            CancellationToken ct)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var profile = await _context.StaffProfiles
                .Include(sp => sp.User)
                .FirstOrDefaultAsync(sp => sp.Id == employeeId, ct)
                ?? throw new KeyNotFoundException("Employee not found.");

            // User-level fields
            if (!string.IsNullOrWhiteSpace(dto.FullName)) profile.User.FullName = dto.FullName.Trim();
            if (dto.FullNameAr != null) profile.User.FullNameAr = TrimOrNull(dto.FullNameAr);
            if (dto.MonthlySalary.HasValue) profile.User.MonthlySalary = decimal.Round(dto.MonthlySalary.Value, 2);
            if (dto.CommissionRate.HasValue) profile.User.CommissionRate = decimal.Round(dto.CommissionRate.Value, 2);
            if (dto.Role.HasValue) profile.User.Role = (UserRole)dto.Role.Value;
            if (!string.IsNullOrWhiteSpace(dto.Password) && dto.Password.Length >= 6)
            {
                profile.User.PasswordHash = PasswordHelper.Hash(dto.Password);
            }

            // Profile-level fields
            if (dto.StaffNo != null) profile.StaffNo = TrimOrNull(dto.StaffNo);
            if (dto.PinCode != null) profile.PinCode = TrimOrNull(dto.PinCode);
            if (dto.BloodType.HasValue) profile.BloodType = (BloodType)dto.BloodType.Value;
            if (dto.Phone != null) profile.Phone = TrimOrNull(dto.Phone);
            if (dto.Address != null) profile.Address = TrimOrNull(dto.Address);
            if (dto.PhotoUrl != null) profile.PhotoUrl = TrimOrNull(dto.PhotoUrl);
            if (dto.StartDate.HasValue) profile.StartDate = ToUtc(dto.StartDate);
            if (dto.ContractStatus.HasValue) profile.ContractStatus = (ContractStatus)dto.ContractStatus.Value;
            if (dto.NetSalary.HasValue) profile.NetSalary = decimal.Round(dto.NetSalary.Value, 2);
            if (dto.SgkPremium.HasValue) profile.SgkPremium = decimal.Round(dto.SgkPremium.Value, 2);
            if (dto.HourlyWage.HasValue) profile.HourlyWage = decimal.Round(dto.HourlyWage.Value, 2);
            if (dto.WeeklyShiftPattern != null) profile.WeeklyShiftPattern = TrimOrNull(dto.WeeklyShiftPattern);

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Staff updated: profile {StaffProfileId} by {ActingUserId}.", profile.Id, actingUserId);

            return await _hrEmployeeService.GetEmployeeAsync(profile.Id, ct)
                ?? throw new InvalidOperationException("Updated staff could not be reloaded.");
        }

        private static string? TrimOrNull(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static Guid? ActorOrNull(Guid value)
            => value == Guid.Empty ? null : value;

        // Date-only inputs (e.g. "2024-05-01") deserialize with Kind=Unspecified.
        // Npgsql rejects those for `timestamptz` columns — coerce to UTC midnight.
        private static DateTime? ToUtc(DateTime? value)
        {
            if (!value.HasValue) return null;
            var v = value.Value;
            return v.Kind switch
            {
                DateTimeKind.Utc => v,
                DateTimeKind.Local => v.ToUniversalTime(),
                _ => DateTime.SpecifyKind(v, DateTimeKind.Utc),
            };
        }
    }
}
