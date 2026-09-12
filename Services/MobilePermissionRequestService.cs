using Microsoft.AspNetCore.Http;
using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.DTOs.MobileRequests;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    public class MobilePermissionRequestService : IMobilePermissionRequestService
    {
        private const string AttachmentRoot = "App_Data";
        private const string AttachmentFolder = "mobile-permission-attachments";

        private static readonly IReadOnlySet<string> AllowedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf",
                ".jpg",
                ".jpeg",
                ".png",
                ".gif",
                ".webp"
            };

        private static readonly IReadOnlySet<string> AllowedContentTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/pdf",
                "image/jpeg",
                "image/png",
                "image/gif",
                "image/webp"
            };

        private readonly IMobileHrRequestRepository _repository;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<MobilePermissionRequestService> _logger;

        public MobilePermissionRequestService(
            IMobileHrRequestRepository repository,
            IWebHostEnvironment environment,
            ILogger<MobilePermissionRequestService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<MobilePermissionRequestListItemDto>> GetAsync(
            Guid userId,
            MobileRequestListQuery query,
            CancellationToken ct)
        {
            var staff = await GetStaffAsync(userId, ct);
            var filter = MobileRequestValidation.BuildFilter(query);
            return await _repository.GetPermissionRequestsAsync(staff.StaffProfileId, filter, ct);
        }

        public async Task<MobilePermissionRequestDto> CreateAsync(
            Guid userId,
            MobilePermissionRequestCreateDto request,
            IFormFile? attachment,
            CancellationToken ct)
        {
            var staff = await GetStaffAsync(userId, ct);
            var values = await BuildRequestValuesAsync(staff.StaffProfileId, request, attachment, ct);
            var storedAttachment = await StoreAttachmentAsync(staff.TenantId, attachment, ct);
            var entity = BuildEntity(staff, request, values, storedAttachment);

            await _repository.AddPermissionRequestAsync(entity, ct);
            _logger.LogInformation("Mobile permission request {RequestId} created by user {UserId}.", entity.Id, userId);
            return Map(entity);
        }

        public async Task<MobilePermissionAllowanceDto> GetAllowanceAsync(Guid userId, CancellationToken ct)
        {
            var staff = await GetStaffAsync(userId, ct);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var periodStart = new DateOnly(today.Year, today.Month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);
            var used = await _repository.GetUsedPermissionMinutesAsync(staff.StaffProfileId, periodStart, periodEnd, ct);

            return new MobilePermissionAllowanceDto
            {
                RemainingHours = MobileRequestValidation.FormatDuration(Math.Max(0, MobilePermissionPolicies.MonthlyAllowanceMinutes - used)),
                PeriodStart = MobileRequestValidation.FormatDate(periodStart),
                PeriodEnd = MobileRequestValidation.FormatDate(periodEnd)
            };
        }

        private async Task<PermissionRequestValues> BuildRequestValuesAsync(
            Guid staffProfileId,
            MobilePermissionRequestCreateDto request,
            IFormFile? attachment,
            CancellationToken ct)
        {
            var type = MobileRequestValidation.RequireCode(request.PermissionType, "permission_type", MobilePermissionRequestTypes.All);
            var date = MobileRequestValidation.RequireDate(request.Date, "date");
            var timeFrom = MobileRequestValidation.RequireTime(request.TimeFrom, "time_from");
            var timeTo = MobileRequestValidation.RequireTime(request.TimeTo, "time_to");
            var duration = ValidateTimeRange(timeFrom, timeTo);

            ValidateAttachmentRule(type, attachment);
            await ValidateAllowanceAsync(staffProfileId, date, duration, ct);
            return new PermissionRequestValues(type, date, timeFrom, timeTo, duration);
        }

        private static int ValidateTimeRange(TimeOnly timeFrom, TimeOnly timeTo)
        {
            if (timeTo <= timeFrom)
            {
                throw new MobileRequestValidationException("time_to must be after time_from.");
            }

            return (int)(timeTo - timeFrom).TotalMinutes;
        }

        private async Task ValidateAllowanceAsync(
            Guid staffProfileId,
            DateOnly date,
            int duration,
            CancellationToken ct)
        {
            var periodStart = new DateOnly(date.Year, date.Month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);
            var used = await _repository.GetUsedPermissionMinutesAsync(staffProfileId, periodStart, periodEnd, ct);
            if (used + duration > MobilePermissionPolicies.MonthlyAllowanceMinutes)
            {
                throw new MobileRequestValidationException("Permission allowance exceeded for this period.");
            }
        }

        private async Task<StoredPermissionAttachment?> StoreAttachmentAsync(
            Guid tenantId,
            IFormFile? attachment,
            CancellationToken ct)
        {
            if (attachment == null)
            {
                return null;
            }

            await ValidateAttachmentAsync(attachment, ct);
            var extension = Path.GetExtension(attachment.FileName).ToLowerInvariant();
            var storageKey = BuildStorageKey(tenantId, extension);
            var fullPath = Path.Combine(GetAttachmentRootPath(), storageKey.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using var input = attachment.OpenReadStream();
            await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, ct);

            return new StoredPermissionAttachment(
                storageKey,
                Path.GetFileName(attachment.FileName),
                attachment.ContentType,
                attachment.Length);
        }

        private static void ValidateAttachmentRule(string permissionType, IFormFile? attachment)
        {
            if (attachment != null && permissionType != MobilePermissionRequestTypes.Medical)
            {
                throw new MobileRequestValidationException("Attachments are only supported for medical permissions.");
            }
        }

        private static async Task ValidateAttachmentAsync(IFormFile attachment, CancellationToken ct)
        {
            var extension = Path.GetExtension(attachment.FileName);
            if (attachment.Length <= 0 || attachment.Length > MobilePermissionPolicies.MaxAttachmentBytes)
                throw new MobileRequestValidationException("Attachment size is invalid.");
            if (!AllowedExtensions.Contains(extension))
                throw new MobileRequestValidationException("Attachment type is invalid.");
            if (!AllowedContentTypes.Contains(attachment.ContentType))
                throw new MobileRequestValidationException("Attachment content type is invalid.");
            if (!await HasValidSignatureAsync(attachment, extension, ct))
                throw new MobileRequestValidationException("Attachment signature is invalid.");
        }

        private static async Task<bool> HasValidSignatureAsync(
            IFormFile attachment,
            string extension,
            CancellationToken ct)
        {
            var header = new byte[12];
            await using var stream = attachment.OpenReadStream();
            var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);

            return extension.ToLowerInvariant() switch
            {
                ".pdf" => read >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
                ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => read >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
                ".gif" => read >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38,
                ".webp" => read >= 12 && IsWebp(header),
                _ => false
            };
        }

        private static bool IsWebp(byte[] header)
            => header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

        private MobilePermissionRequest BuildEntity(
            MobileStaffSnapshot staff,
            MobilePermissionRequestCreateDto request,
            PermissionRequestValues values,
            StoredPermissionAttachment? attachment)
            => new()
            {
                Id = Guid.NewGuid(),
                TenantId = staff.TenantId,
                StaffProfileId = staff.StaffProfileId,
                Date = values.Date,
                PermissionType = values.PermissionType,
                TimeFrom = values.TimeFrom,
                TimeTo = values.TimeTo,
                DurationMinutes = values.DurationMinutes,
                Reason = MobileRequestValidation.TrimAndCap(request.Reason, 500),
                Status = MobileRequestStatuses.Pending,
                AttachmentStorageKey = attachment?.StorageKey,
                AttachmentOriginalFileName = MobileRequestValidation.TrimAndCap(attachment?.OriginalFileName, 255),
                AttachmentContentType = MobileRequestValidation.TrimAndCap(attachment?.ContentType, 100),
                AttachmentSizeBytes = attachment?.SizeBytes,
                CreatedByUserId = staff.UserId,
                CreatedByUserName = staff.FullName ?? staff.Username,
                CreatedAt = DateTime.UtcNow
            };

        private async Task<MobileStaffSnapshot> GetStaffAsync(Guid userId, CancellationToken ct)
            => await _repository.GetOrCreateStaffSnapshotAsync(userId, ct)
                ?? throw new MobileRequestValidationException("Staff profile was not found.");

        private string GetAttachmentRootPath()
            => Path.Combine(_environment.ContentRootPath, AttachmentRoot, AttachmentFolder);

        private static string BuildStorageKey(Guid tenantId, string extension)
            => $"{tenantId:N}/{DateTime.UtcNow:yyyyMM}/{Guid.NewGuid():N}{extension}";

        private static MobilePermissionRequestDto Map(MobilePermissionRequest request)
            => new()
            {
                Id = request.Id.ToString(),
                Date = MobileRequestValidation.FormatDate(request.Date),
                PermissionType = request.PermissionType,
                TimeFrom = MobileRequestValidation.FormatTime(request.TimeFrom),
                TimeTo = MobileRequestValidation.FormatTime(request.TimeTo),
                TotalDuration = MobileRequestValidation.FormatDuration(request.DurationMinutes),
                Status = request.Status,
                Reason = request.Reason
            };

        private sealed record PermissionRequestValues(
            string PermissionType,
            DateOnly Date,
            TimeOnly TimeFrom,
            TimeOnly TimeTo,
            int DurationMinutes);

        private sealed record StoredPermissionAttachment(
            string StorageKey,
            string OriginalFileName,
            string ContentType,
            long SizeBytes);

        // ===== Admin =====

        public async Task<HrRequestPagedResponse<HrPermissionRequestDto>> GetAdminListAsync(
            HrRequestAdminFilter filter,
            CancellationToken ct)
        {
            var page = await _repository.GetAdminPermissionRequestsAsync(filter, ct);
            return new HrRequestPagedResponse<HrPermissionRequestDto>
            {
                Items = page.Items.Select(row => MapAdmin(row.Entity, row.Employee)).ToList(),
                Total = page.Total,
                Page = filter.NormalizedPage,
                PageSize = filter.NormalizedPageSize,
                StatusCounts = page.StatusCounts
            };
        }

        public async Task<HrPermissionRequestDto> UpdateStatusAsync(
            Guid requestId,
            string newStatus,
            Guid actingUserId,
            bool isAdmin,
            CancellationToken ct)
        {
            var entity = await _repository.GetPermissionByIdAsync(requestId, ct)
                ?? throw new KeyNotFoundException("Permission request not found.");
            HrRequestTransitionGuard.Apply(entity.Status, ref newStatus, entity.CreatedByUserId, actingUserId, isAdmin);

            entity.Status = newStatus;
            entity.ReviewedAt = DateTime.UtcNow;
            entity.ReviewedByUserId = actingUserId;
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation("Permission request {RequestId} -> {Status} by user {UserId}.", entity.Id, newStatus, actingUserId);

            var employee = await _repository.GetEmployeeForStaffAsync(entity.StaffProfileId, ct)
                ?? new HrRequestEmployeeDto { EmployeeId = entity.StaffProfileId };
            return MapAdmin(entity, employee);
        }

        public async Task<PermissionAttachmentStream?> OpenAttachmentAsync(Guid requestId, CancellationToken ct)
        {
            var entity = await _repository.GetPermissionByIdAsync(requestId, ct);
            if (entity == null || string.IsNullOrWhiteSpace(entity.AttachmentStorageKey))
                return null;

            var fullPath = Path.Combine(GetAttachmentRootPath(),
                entity.AttachmentStorageKey.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath)) return null;

            // Caller is responsible for disposing the stream once the response finishes.
            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return new PermissionAttachmentStream(
                stream,
                entity.AttachmentContentType ?? "application/octet-stream",
                entity.AttachmentOriginalFileName ?? Path.GetFileName(fullPath),
                stream.Length);
        }

        private static HrPermissionRequestDto MapAdmin(MobilePermissionRequest e, HrRequestEmployeeDto emp) => new()
        {
            Id = e.Id,
            Employee = emp,
            Status = e.Status,
            StatusLabel = MobileRequestLabels.Status(e.Status),
            CreatedAt = e.CreatedAt,
            ReviewedAt = e.ReviewedAt,
            ReviewedByUserId = e.ReviewedByUserId,
            PermissionType = e.PermissionType,
            PermissionTypeLabel = MobileRequestLabels.PermissionType(e.PermissionType),
            Date = MobileRequestValidation.FormatDate(e.Date),
            TimeFrom = MobileRequestValidation.FormatTime(e.TimeFrom),
            TimeTo = MobileRequestValidation.FormatTime(e.TimeTo),
            DurationMinutes = e.DurationMinutes,
            Reason = e.Reason,
            HasAttachment = !string.IsNullOrWhiteSpace(e.AttachmentStorageKey),
            AttachmentFileName = e.AttachmentOriginalFileName,
            AttachmentContentType = e.AttachmentContentType,
            AttachmentSizeBytes = e.AttachmentSizeBytes
        };
    }
}
