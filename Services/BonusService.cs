using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    public class BonusService : IBonusService
    {
        private readonly IBonusRepository _repo;
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly ILogger<BonusService> _logger;

        public BonusService(
            IBonusRepository repo,
            PosDbContext context,
            ITenantResolver tenantResolver,
            ILogger<BonusService> logger)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HrBonusListResponse> GetAsync(
            Guid? employeeId,
            string? status,
            string? bonusType,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            ValidateOptionalCode(status, "status", HrBonusStatuses.All);
            ValidateOptionalCode(bonusType, "bonusType", HrBonusTypes.All);

            var filter = new BonusListFilter(
                employeeId,
                string.IsNullOrWhiteSpace(status) ? null : status,
                string.IsNullOrWhiteSpace(bonusType) ? null : bonusType,
                from.HasValue ? DateOnly.FromDateTime(from.Value) : null,
                to.HasValue ? DateOnly.FromDateTime(to.Value) : null,
                page < 1 ? 1 : page,
                pageSize < 1 ? 25 : Math.Min(pageSize, 200));

            try
            {
                var result = await _repo.GetAsync(filter, ct);

                return new HrBonusListResponse
                {
                    Items = result.Items.Select(Map).ToList(),
                    Total = result.Total,
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalAmount = result.TotalAmount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "HR bonuses query failed (employeeId={EmployeeId}, status={Status}, type={BonusType}, from={From}, to={To}).",
                    employeeId, status, bonusType, from, to);
                throw;
            }
        }

        public async Task<HrBonusDto> CreateAsync(
            HrBonusCreateDto dto,
            Guid actingUserId,
            string actingUserName,
            CancellationToken ct)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.EmployeeId == Guid.Empty) throw new ArgumentException("employeeId is required.", nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.BonusType) || !HrBonusTypes.All.Contains(dto.BonusType))
                throw new ArgumentException($"Invalid bonusType. Allowed: {string.Join(", ", HrBonusTypes.AllOrdered)}", nameof(dto));
            if (dto.Amount <= 0) throw new ArgumentException("Amount must be greater than zero.", nameof(dto));

            // Confirm employee exists in current tenant
            var staff = await _context.StaffProfiles
                .AsNoTracking()
                .Include(sp => sp.User)
                .FirstOrDefaultAsync(sp => sp.Id == dto.EmployeeId, ct)
                ?? throw new ArgumentException("Employee not found.", nameof(dto));

            var entity = new Bonus
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                StaffProfileId = staff.Id,
                BonusType = dto.BonusType,
                Amount = decimal.Round(dto.Amount, 2),
                Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "QAR" : dto.Currency!.Trim().ToUpperInvariant(),
                AwardDate = DateOnly.FromDateTime(dto.AwardDate),
                Reason = Trim(dto.Reason, 500),
                Notes = Trim(dto.Notes, 1000),
                Status = HrBonusStatuses.Pending,
                CreatedByUserId = actingUserId,
                CreatedByUserName = string.IsNullOrWhiteSpace(actingUserName) ? "system" : actingUserName,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(entity, ct);
            _logger.LogInformation("HR bonus {BonusId} created for employee {EmployeeId} by user {UserId}.",
                entity.Id, entity.StaffProfileId, actingUserId);

            entity.StaffProfile = staff;     // for mapping
            return Map(entity);
        }

        public async Task<HrBonusDto> UpdateStatusAsync(
            Guid bonusId,
            string newStatus,
            Guid actingUserId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(newStatus) || !HrBonusStatuses.All.Contains(newStatus))
                throw new ArgumentException("Invalid status.", nameof(newStatus));

            var entity = await _context.Bonuses
                .Include(b => b.StaffProfile)
                    .ThenInclude(sp => sp.User)
                .FirstOrDefaultAsync(b => b.Id == bonusId, ct)
                ?? throw new KeyNotFoundException("Bonus not found.");

            if (!HrBonusStatuses.AllowedTransitions.TryGetValue(entity.Status, out var allowed)
                || !allowed.Contains(newStatus))
            {
                throw new InvalidOperationException(
                    $"Cannot transition bonus from '{entity.Status}' to '{newStatus}'.");
            }

            entity.Status = newStatus;
            switch (newStatus)
            {
                case HrBonusStatuses.Approved:
                    entity.ApprovedByUserId = actingUserId;
                    entity.ApprovedAt = DateTime.UtcNow;
                    break;
                case HrBonusStatuses.Paid:
                    entity.PaidAt = DateTime.UtcNow;
                    break;
            }

            await _repo.UpdateAsync(entity, ct);
            _logger.LogInformation("HR bonus {BonusId} status -> {Status} by user {UserId}.",
                entity.Id, entity.Status, actingUserId);
            return Map(entity);
        }

        public async Task<HrBonusDto?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var entity = await _repo.GetByIdAsync(id, ct);
            return entity == null ? null : Map(entity);
        }

        private static void ValidateOptionalCode(string? value, string name, IReadOnlySet<string> allowed)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!allowed.Contains(value))
                throw new ArgumentException($"Invalid {name}. Allowed: {string.Join(", ", allowed)}");
        }

        private static string? Trim(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var t = value.Trim();
            return t.Length > max ? t[..max] : t;
        }

        private static HrBonusDto Map(Bonus b)
        {
            var fullName = b.StaffProfile?.User?.FullName ?? b.StaffProfile?.User?.Username;
            return new HrBonusDto
            {
                Id = b.Id,
                EmployeeId = b.StaffProfileId,
                EmployeeName = fullName,
                StaffNo = b.StaffProfile?.StaffNo,
                BonusType = b.BonusType,
                BonusTypeLabel = MobileRequestLabels.BonusType(b.BonusType),
                Amount = b.Amount,
                Currency = b.Currency,
                AwardDate = b.AwardDate.ToString("yyyy-MM-dd"),
                Reason = b.Reason,
                Notes = b.Notes,
                Status = b.Status,
                StatusLabel = MobileRequestLabels.BonusStatus(b.Status),
                CreatedByUserName = b.CreatedByUserName,
                CreatedAt = b.CreatedAt,
                ApprovedAt = b.ApprovedAt,
                PaidAt = b.PaidAt
            };
        }
    }
}
