using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    public class PosLogService : IPosLogService
    {
        private static readonly string[] AllowedDateFilters =
        {
            PosLogDateFilters.Today,
            PosLogDateFilters.All
        };

        private static readonly string[] AllowedTypeFilters =
        {
            PosLogTypes.Cancel,
            PosLogTypes.Waste,
            PosLogTypeFilters.Cancel,
            PosLogTypeFilters.Cancelled,
            PosLogTypeFilters.Waste
        };

        private readonly IPosLogRepository _repository;

        public PosLogService(IPosLogRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<PosLogPageDto> GetLogsAsync(
            Guid tenantId,
            PosLogQueryDto query,
            Guid currentUserId,
            bool isAdmin,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            Validate(query);

            var shift = query.ShiftId.HasValue
                ? await _repository.GetShiftAsync(tenantId, query.ShiftId.Value, cancellationToken)
                : null;

            if (query.ShiftId.HasValue && shift == null)
                throw new ValidationException("Shift not found.");

            return await _repository.GetLogsAsync(
                tenantId,
                query,
                currentUserId,
                isAdmin,
                shift,
                cancellationToken);
        }

        private static void Validate(PosLogQueryDto query)
        {
            var date = string.IsNullOrWhiteSpace(query.Date)
                ? PosLogDateFilters.Today
                : query.Date.Trim();

            if (!AllowedDateFilters.Any(v => string.Equals(v, date, StringComparison.OrdinalIgnoreCase)))
                throw new ValidationException("Date filter must be today or all.");

            query.Date = date;

            if (string.IsNullOrWhiteSpace(query.Type))
                return;

            var type = query.Type.Trim();
            if (!AllowedTypeFilters.Any(v => string.Equals(v, type, StringComparison.OrdinalIgnoreCase)))
                throw new ValidationException("Type filter must be cancel or waste.");

            query.Type = type;
        }
    }
}
