using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.DTOs.MobileRequests;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    public class MobileLeaveRequestService : IMobileLeaveRequestService
    {
        private const int AnnualBalanceDays = 12;
        private const string LeaveRulesHtml = "<p>Submit leave requests before your planned absence where possible.</p>";

        private readonly IMobileHrRequestRepository _repository;
        private readonly ILogger<MobileLeaveRequestService> _logger;

        public MobileLeaveRequestService(
            IMobileHrRequestRepository repository,
            ILogger<MobileLeaveRequestService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<MobileLeaveRequestDto>> GetAsync(
            Guid userId,
            MobileRequestListQuery query,
            CancellationToken ct)
        {
            var staff = await GetStaffAsync(userId, ct);
            var filter = MobileRequestValidation.BuildFilter(query);
            return await _repository.GetLeaveRequestsAsync(staff.StaffProfileId, filter, ct);
        }

        public async Task<MobileLeaveRequestDto> CreateAsync(
            Guid userId,
            MobileLeaveRequestCreateDto request,
            CancellationToken ct)
        {
            var staff = await GetStaffAsync(userId, ct);
            var type = MobileRequestValidation.RequireCode(request.Type, "type", MobileLeaveRequestTypes.All);
            var startDate = MobileRequestValidation.RequireDate(request.StartDate, "start_date");
            var endDate = MobileRequestValidation.RequireDate(request.EndDate, "end_date");
            ValidateDateRange(startDate, endDate);

            if (await _repository.HasOverlappingLeaveRequestAsync(staff.StaffProfileId, startDate, endDate, ct))
            {
                throw new MobileRequestValidationException("Leave request overlaps an existing pending or approved request.");
            }

            var entity = BuildEntity(staff, type, startDate, endDate, request.Notes);
            await _repository.AddLeaveRequestAsync(entity, ct);
            _logger.LogInformation("Mobile leave request {RequestId} created by user {UserId}.", entity.Id, userId);
            return Map(entity);
        }

        public async Task<MobileLeaveRequestMetaDto> GetMetaAsync(Guid userId, CancellationToken ct)
        {
            _ = await GetStaffAsync(userId, ct);
            return new MobileLeaveRequestMetaDto
            {
                AnnualBalanceDays = AnnualBalanceDays,
                SickRequiresAttachment = false,
                RulesHtml = LeaveRulesHtml,
                MaxConsecutiveDays = null
            };
        }

        private async Task<MobileStaffSnapshot> GetStaffAsync(Guid userId, CancellationToken ct)
            => await _repository.GetOrCreateStaffSnapshotAsync(userId, ct)
                ?? throw new MobileRequestValidationException("Staff profile was not found.");

        private static void ValidateDateRange(DateOnly startDate, DateOnly endDate)
        {
            if (endDate < startDate)
            {
                throw new MobileRequestValidationException("end_date must be after or equal to start_date.");
            }
        }

        private static MobileLeaveRequest BuildEntity(
            MobileStaffSnapshot staff,
            string type,
            DateOnly startDate,
            DateOnly endDate,
            string? notes)
            => new()
            {
                Id = Guid.NewGuid(),
                TenantId = staff.TenantId,
                StaffProfileId = staff.StaffProfileId,
                StartDate = startDate,
                EndDate = endDate,
                DurationInDays = endDate.DayNumber - startDate.DayNumber + 1,
                Type = type,
                Notes = MobileRequestValidation.TrimAndCap(notes, 500),
                Status = MobileRequestStatuses.Pending,
                CreatedByUserId = staff.UserId,
                CreatedByUserName = staff.FullName ?? staff.Username,
                CreatedAt = DateTime.UtcNow
            };

        private static MobileLeaveRequestDto Map(MobileLeaveRequest request)
            => new()
            {
                Id = request.Id.ToString(),
                StartDate = MobileRequestValidation.FormatDate(request.StartDate),
                EndDate = MobileRequestValidation.FormatDate(request.EndDate),
                DurationInDays = request.DurationInDays,
                Type = request.Type,
                Notes = request.Notes,
                Status = request.Status
            };

        // ===== Admin =====

        public async Task<HrRequestPagedResponse<HrLeaveRequestDto>> GetAdminListAsync(
            HrRequestAdminFilter filter,
            CancellationToken ct)
        {
            var page = await _repository.GetAdminLeaveRequestsAsync(filter, ct);
            return new HrRequestPagedResponse<HrLeaveRequestDto>
            {
                Items = page.Items.Select(row => MapAdmin(row.Entity, row.Employee)).ToList(),
                Total = page.Total,
                Page = filter.NormalizedPage,
                PageSize = filter.NormalizedPageSize,
                StatusCounts = page.StatusCounts
            };
        }

        public async Task<HrLeaveRequestDto> UpdateStatusAsync(
            Guid requestId,
            string newStatus,
            Guid actingUserId,
            bool isAdmin,
            CancellationToken ct)
        {
            var entity = await _repository.GetLeaveByIdAsync(requestId, ct)
                ?? throw new KeyNotFoundException("Leave request not found.");
            HrRequestTransitionGuard.Apply(entity.Status, ref newStatus, entity.CreatedByUserId, actingUserId, isAdmin);

            entity.Status = newStatus;
            entity.ReviewedAt = DateTime.UtcNow;
            entity.ReviewedByUserId = actingUserId;
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation("Leave request {RequestId} -> {Status} by user {UserId}.", entity.Id, newStatus, actingUserId);

            var employee = await _repository.GetEmployeeForStaffAsync(entity.StaffProfileId, ct)
                ?? new HrRequestEmployeeDto { EmployeeId = entity.StaffProfileId };
            return MapAdmin(entity, employee);
        }

        private static HrLeaveRequestDto MapAdmin(MobileLeaveRequest e, HrRequestEmployeeDto emp) => new()
        {
            Id = e.Id,
            Employee = emp,
            Status = e.Status,
            StatusLabel = MobileRequestLabels.Status(e.Status),
            CreatedAt = e.CreatedAt,
            ReviewedAt = e.ReviewedAt,
            ReviewedByUserId = e.ReviewedByUserId,
            Type = e.Type,
            TypeLabel = MobileRequestLabels.LeaveType(e.Type),
            StartDate = MobileRequestValidation.FormatDate(e.StartDate),
            EndDate = MobileRequestValidation.FormatDate(e.EndDate),
            DurationInDays = e.DurationInDays,
            Notes = e.Notes
        };
    }
}
