using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs.Attendance;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Interfaces;

namespace RestaurantPos.Api.Services
{
    public class AttendanceService : IAttendanceService
    {
        private const int AttendanceNoteMaxLength = 500;
        private const double EarthRadiusMeters = 6371000d;
        private const double DegreesInHalfCircle = 180d;
        private const string CheckInAction = "check-in";
        private const string CheckOutAction = "check-out";
        private const string LocationRequiredMessage = "Attendance location is required for this branch.";
        private const string LocationInvalidMessage = "Attendance coordinates are invalid.";
        private const string LocationOutsideRadiusMessage = "You are outside the allowed branch attendance radius.";
        private const string BranchLocationIncompleteMessage = "Branch attendance location settings are incomplete.";
        private const string LocationRequiredStatus = "AttendanceLocationRequired";
        private const string LocationInvalidStatus = "AttendanceLocationInvalid";
        private const string LocationOutsideRadiusStatus = "OutsideAllowedRadius";
        private const string BranchLocationIncompleteStatus = "BranchLocationIncomplete";

        private readonly PosDbContext _context;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<AttendanceService> _logger;

        public AttendanceService(
            PosDbContext context,
            ISettingsService settingsService,
            ILogger<AttendanceService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AttendanceRecordDto?> CheckInAsync(
            Guid userId,
            CheckInRequest request,
            CancellationToken ct = default)
        {
            try
            {
                var staffProfile = await GetOrCreateStaffProfileAsync(userId, ct);

                if (staffProfile == null)
                {
                    return null;
                }

                var existingEntry = await _context.TimeEntries
                    .Include(te => te.Staff)
                        .ThenInclude(sp => sp.User)
                    .Where(te => te.StaffId == staffProfile.Id && te.ClockOut == null)
                    .OrderByDescending(te => te.ClockIn)
                    .FirstOrDefaultAsync(ct);

                if (existingEntry != null)
                {
                    return MapAttendanceRecord(existingEntry);
                }

                var location = await ValidateAttendanceLocationAsync(
                    userId,
                    staffProfile,
                    request.Latitude,
                    request.Longitude,
                    CheckInAction,
                    ct);

                var now = DateTime.UtcNow;
                var entry = new Models.TimeEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = staffProfile.TenantId,
                    StaffId = staffProfile.Id,
                    Date = now.Date,
                    ClockIn = now,
                    CheckInNote = TrimAndCap(request.Note, AttendanceNoteMaxLength),
                    CheckInLatitude = location?.Latitude,
                    CheckInLongitude = location?.Longitude,
                    CheckInDistanceMeters = location?.DistanceMeters
                };

                _context.TimeEntries.Add(entry);
                await _context.SaveChangesAsync(ct);

                entry.Staff = staffProfile;
                return MapAttendanceRecord(entry);
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Attendance check-in failed for user {UserId}.", userId);
                return null;
            }
        }

        public async Task<AttendanceRecordDto?> CheckOutAsync(
            Guid userId,
            CheckOutRequest request,
            CancellationToken ct = default)
        {
            try
            {
                var staffProfile = await _context.StaffProfiles
                    .Include(sp => sp.User)
                    .FirstOrDefaultAsync(sp => sp.UserId == userId, ct);

                if (staffProfile == null)
                {
                    return null;
                }

                var entry = await _context.TimeEntries
                    .Include(te => te.Staff)
                        .ThenInclude(sp => sp.User)
                    .Where(te => te.StaffId == staffProfile.Id && te.ClockOut == null)
                    .OrderByDescending(te => te.ClockIn)
                    .FirstOrDefaultAsync(ct);

                if (entry == null)
                {
                    return null;
                }

                var location = await ValidateAttendanceLocationAsync(
                    userId,
                    staffProfile,
                    request.Latitude,
                    request.Longitude,
                    CheckOutAction,
                    ct);

                var now = DateTime.UtcNow;
                entry.ClockOut = now;
                entry.CheckOutNote = TrimAndCap(request.Note, AttendanceNoteMaxLength);
                entry.CheckOutLatitude = location?.Latitude;
                entry.CheckOutLongitude = location?.Longitude;
                entry.CheckOutDistanceMeters = location?.DistanceMeters;
                if (entry.ClockIn.HasValue)
                {
                    entry.DurationMinutes = (int)Math.Round((now - entry.ClockIn.Value).TotalMinutes, MidpointRounding.AwayFromZero);
                }

                await _context.SaveChangesAsync(ct);
                return MapAttendanceRecord(entry);
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Attendance check-out failed for user {UserId}.", userId);
                return null;
            }
        }

        public async Task<AttendancePagedResponse> GetAttendanceAsync(
            AttendanceFilterRequest filter,
            CancellationToken ct = default)
        {
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize < 1 ? 20 : Math.Min(filter.PageSize, 100);
            var normalizedStatus = filter.Status?.Trim().ToLowerInvariant();
            var normalizedSortOrder = filter.SortOrder?.Trim().ToLowerInvariant() == "asc" ? "asc" : "desc";

            IQueryable<Models.TimeEntry> query = _context.TimeEntries.AsNoTracking();

            if (filter.UserId.HasValue)
            {
                query = query.Where(te => te.Staff.UserId == filter.UserId.Value);
            }

            if (filter.FromDate.HasValue)
            {
                var fromDate = filter.FromDate.Value.Date;
                query = query.Where(te => te.ClockIn.HasValue && te.ClockIn.Value >= fromDate);
            }

            if (filter.ToDate.HasValue)
            {
                var toDateExclusive = filter.ToDate.Value.Date.AddDays(1);
                query = query.Where(te => te.ClockIn.HasValue && te.ClockIn.Value < toDateExclusive);
            }

            if (normalizedStatus == "checkedin")
            {
                query = query.Where(te => te.ClockOut == null);
            }
            else if (normalizedStatus == "checkedout")
            {
                query = query.Where(te => te.ClockOut != null);
            }

            var totalCount = await query.CountAsync(ct);

            query = normalizedSortOrder == "asc"
                ? query.OrderBy(te => te.ClockIn ?? te.Date)
                : query.OrderByDescending(te => te.ClockIn ?? te.Date);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(te => new AttendanceRecordDto
                {
                    Id = te.Id,
                    UserId = te.Staff.UserId,
                    UserFullName = te.Staff.User.FullName ?? te.Staff.User.Username,
                    CheckInAt = te.ClockIn ?? te.Date,
                    CheckOutAt = te.ClockOut,
                    DurationMinutes = te.DurationMinutes,
                    CheckInNote = te.CheckInNote,
                    CheckOutNote = te.CheckOutNote,
                    CheckInLatitude = te.CheckInLatitude,
                    CheckInLongitude = te.CheckInLongitude,
                    CheckInDistanceMeters = te.CheckInDistanceMeters,
                    CheckOutLatitude = te.CheckOutLatitude,
                    CheckOutLongitude = te.CheckOutLongitude,
                    CheckOutDistanceMeters = te.CheckOutDistanceMeters,
                    Status = te.ClockOut == null ? "CheckedIn" : "CheckedOut"
                })
                .ToListAsync(ct);

            return new AttendancePagedResponse
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<AttendanceRecordDto?> GetActiveCheckInAsync(
            Guid userId,
            CancellationToken ct = default)
        {
            return await _context.TimeEntries
                .AsNoTracking()
                .Where(te => te.Staff.UserId == userId && te.ClockOut == null)
                .OrderByDescending(te => te.ClockIn)
                .Select(te => new AttendanceRecordDto
                {
                    Id = te.Id,
                    UserId = te.Staff.UserId,
                    UserFullName = te.Staff.User.FullName ?? te.Staff.User.Username,
                    CheckInAt = te.ClockIn ?? te.Date,
                    CheckOutAt = te.ClockOut,
                    DurationMinutes = te.DurationMinutes,
                    CheckInNote = te.CheckInNote,
                    CheckOutNote = te.CheckOutNote,
                    CheckInLatitude = te.CheckInLatitude,
                    CheckInLongitude = te.CheckInLongitude,
                    CheckInDistanceMeters = te.CheckInDistanceMeters,
                    CheckOutLatitude = te.CheckOutLatitude,
                    CheckOutLongitude = te.CheckOutLongitude,
                    CheckOutDistanceMeters = te.CheckOutDistanceMeters,
                    Status = "CheckedIn"
                })
                .FirstOrDefaultAsync(ct);
        }

        private static AttendanceRecordDto MapAttendanceRecord(Models.TimeEntry timeEntry)
        {
            return new AttendanceRecordDto
            {
                Id = timeEntry.Id,
                UserId = timeEntry.Staff.UserId,
                UserFullName = timeEntry.Staff.User.FullName ?? timeEntry.Staff.User.Username,
                CheckInAt = timeEntry.ClockIn ?? timeEntry.Date,
                CheckOutAt = timeEntry.ClockOut,
                DurationMinutes = timeEntry.DurationMinutes,
                CheckInNote = timeEntry.CheckInNote,
                CheckOutNote = timeEntry.CheckOutNote,
                CheckInLatitude = timeEntry.CheckInLatitude,
                CheckInLongitude = timeEntry.CheckInLongitude,
                CheckInDistanceMeters = timeEntry.CheckInDistanceMeters,
                CheckOutLatitude = timeEntry.CheckOutLatitude,
                CheckOutLongitude = timeEntry.CheckOutLongitude,
                CheckOutDistanceMeters = timeEntry.CheckOutDistanceMeters,
                Status = timeEntry.ClockOut == null ? "CheckedIn" : "CheckedOut"
            };
        }

        private async Task<Models.StaffProfile?> GetOrCreateStaffProfileAsync(
            Guid userId,
            CancellationToken ct)
        {
            var staffProfile = await _context.StaffProfiles
                .Include(sp => sp.User)
                .FirstOrDefaultAsync(sp => sp.UserId == userId, ct);

            if (staffProfile != null)
            {
                return staffProfile;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
            {
                return null;
            }

            staffProfile = new Models.StaffProfile
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                UserId = user.Id,
                StaffNo = StaffNumberHelper.BuildGeneratedStaffNumber(user.Username, user.Id)
            };

            _context.StaffProfiles.Add(staffProfile);
            await _context.SaveChangesAsync(ct);

            staffProfile.User = user;
            return staffProfile;
        }

        private async Task<AttendanceLocationAudit?> ValidateAttendanceLocationAsync(
            Guid userId,
            Models.StaffProfile staffProfile,
            decimal? latitude,
            decimal? longitude,
            string action,
            CancellationToken ct)
        {
            var branchLocation = await GetBranchLocationConfigAsync(staffProfile.TenantId, ct);
            if (branchLocation == null)
            {
                return null;
            }

            ValidateEmployeeCoordinates(userId, latitude, longitude, action, branchLocation.AllowedRadiusMeters);

            var distance = CalculateDistanceMeters(
                latitude!.Value,
                longitude!.Value,
                branchLocation.Latitude,
                branchLocation.Longitude);
            var audit = new AttendanceLocationAudit(
                latitude.Value,
                longitude.Value,
                RoundDistanceMeters(distance));

            return EnsureWithinAllowedRadius(userId, action, distance, audit, branchLocation.AllowedRadiusMeters);
        }

        private async Task<BranchLocationConfig?> GetBranchLocationConfigAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _settingsService.GetSettingsAsync(tenantId, ct);
            if (!HasAnyBranchLocationSetting(settings))
            {
                return null;
            }

            if (!IsBranchLocationConfigured(settings))
            {
                throw new ValidationException(BranchLocationIncompleteMessage, BranchLocationIncompleteStatus);
            }

            return new BranchLocationConfig(
                settings.Latitude!.Value,
                settings.Longitude!.Value,
                settings.AllowedRadiusMeters!.Value);
        }

        private void ValidateEmployeeCoordinates(
            Guid userId,
            decimal? latitude,
            decimal? longitude,
            string action,
            int allowedRadiusMeters)
        {
            if (!latitude.HasValue || !longitude.HasValue)
            {
                LogMissingLocation(userId, action, allowedRadiusMeters);
                throw new ValidationException(LocationRequiredMessage, LocationRequiredStatus);
            }

            if (latitude is < -90m or > 90m || longitude is < -180m or > 180m)
            {
                _logger.LogWarning(
                    "Attendance {Action} rejected for user {UserId}: invalid coordinates.",
                    action,
                    userId);
                throw new ValidationException(LocationInvalidMessage, LocationInvalidStatus);
            }
        }

        private void LogMissingLocation(Guid userId, string action, int allowedRadiusMeters)
        {
            _logger.LogWarning(
                "Attendance {Action} rejected for user {UserId}: location missing for configured radius {AllowedRadiusMeters}m.",
                action,
                userId,
                allowedRadiusMeters);
        }

        private AttendanceLocationAudit EnsureWithinAllowedRadius(
            Guid userId,
            string action,
            double distanceMeters,
            AttendanceLocationAudit audit,
            int allowedRadiusMeters)
        {
            if (distanceMeters <= allowedRadiusMeters)
            {
                return audit;
            }

            LogOutsideRadius(userId, action, audit.DistanceMeters, allowedRadiusMeters);
            throw new ValidationException(LocationOutsideRadiusMessage, LocationOutsideRadiusStatus);
        }

        private void LogOutsideRadius(
            Guid userId,
            string action,
            decimal distanceMeters,
            int allowedRadiusMeters)
        {
            _logger.LogWarning(
                "Attendance {Action} rejected for user {UserId}: distance {DistanceMeters}m exceeds radius {AllowedRadiusMeters}m.",
                action,
                userId,
                distanceMeters,
                allowedRadiusMeters);
        }

        private static bool HasAnyBranchLocationSetting(Models.SystemSettings settings)
            => settings.Latitude.HasValue
                || settings.Longitude.HasValue
                || settings.AllowedRadiusMeters.HasValue;

        private static bool IsBranchLocationConfigured(Models.SystemSettings settings)
            => settings.Latitude.HasValue
                && settings.Longitude.HasValue
                && settings.AllowedRadiusMeters.HasValue;

        private static double CalculateDistanceMeters(
            decimal employeeLatitude,
            decimal employeeLongitude,
            decimal branchLatitude,
            decimal branchLongitude)
        {
            var employeeLatitudeRadians = ToRadians((double)employeeLatitude);
            var branchLatitudeRadians = ToRadians((double)branchLatitude);
            var latitudeDelta = ToRadians((double)(branchLatitude - employeeLatitude));
            var longitudeDelta = ToRadians((double)(branchLongitude - employeeLongitude));

            var haversine = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d)
                + Math.Cos(employeeLatitudeRadians)
                * Math.Cos(branchLatitudeRadians)
                * Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
            var clamped = Math.Min(1d, Math.Max(0d, haversine));
            return EarthRadiusMeters * 2d * Math.Atan2(Math.Sqrt(clamped), Math.Sqrt(1d - clamped));
        }

        private static double ToRadians(double degrees)
            => degrees * Math.PI / DegreesInHalfCircle;

        private static decimal RoundDistanceMeters(double distanceMeters)
            => decimal.Round((decimal)distanceMeters, 2, MidpointRounding.AwayFromZero);

        private static string? TrimAndCap(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private sealed record AttendanceLocationAudit(
            decimal Latitude,
            decimal Longitude,
            decimal DistanceMeters);

        private sealed record BranchLocationConfig(
            decimal Latitude,
            decimal Longitude,
            int AllowedRadiusMeters);
    }
}
