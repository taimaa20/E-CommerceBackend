using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public interface IStaleCashierShiftAlertJobService
    {
        Task<StaleCashierShiftAlertJobResult> RunAsync(CancellationToken cancellationToken = default);
    }

    public sealed class StaleCashierShiftAlertJobResult
    {
        public string Message { get; init; } = string.Empty;
        public int ShiftsAlerted { get; init; }
        public bool SkippedBecauseAlreadyRunning { get; init; }
    }

    /// <summary>
    /// Scans for cashier balance shifts left open for longer than
    /// <see cref="StaleThresholdHours"/> and emits a <see cref="NotificationType.ShiftLeftOpen"/>
    /// notification so a manager can reconcile. DOES NOT auto-close — financial audit risk.
    /// Duplicate notifications for the same shift are suppressed via the reference-id lookup.
    /// </summary>
    public class StaleCashierShiftAlertJobService : IStaleCashierShiftAlertJobService
    {
        private const long AdvisoryLockKey = 2026042003;
        private const int StaleThresholdHours = 18;
        private const string NotifyRoles = "Admin,Manager";

        private readonly PosDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<StaleCashierShiftAlertJobService> _logger;

        public StaleCashierShiftAlertJobService(
            PosDbContext context,
            INotificationService notificationService,
            ILogger<StaleCashierShiftAlertJobService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StaleCashierShiftAlertJobResult> RunAsync(CancellationToken cancellationToken = default)
        {
            var lockAcquired = await TryAcquireLockAsync(cancellationToken);
            if (!lockAcquired)
            {
                await _context.Database.CloseConnectionAsync();
                _logger.LogInformation("[StaleShiftAlert] Skipped — another runner holds the advisory lock.");
                return new StaleCashierShiftAlertJobResult
                {
                    Message = "Stale-shift alert already running.",
                    SkippedBecauseAlreadyRunning = true
                };
            }

            try
            {
                var nowUtc = DateTime.UtcNow;
                var staleCutoff = nowUtc.AddHours(-StaleThresholdHours);

                var staleShifts = await _context.CashierBalanceShifts
                    .IgnoreQueryFilters()
                    .Where(s => s.ClosedAt == null && s.OpenedAt <= staleCutoff)
                    .ToListAsync(cancellationToken);

                var alerted = 0;

                foreach (var shift in staleShifts)
                {
                    var referenceId = shift.Id.ToString();

                    // Suppress duplicates — if we've already alerted for this shift, skip.
                    var alreadyAlerted = await _context.Notifications
                        .IgnoreQueryFilters()
                        .AnyAsync(n => n.Type == NotificationType.ShiftLeftOpen
                                    && n.ReferenceId == referenceId,
                                  cancellationToken);

                    if (alreadyAlerted) continue;

                    var hoursOpen = (int)Math.Round((nowUtc - shift.OpenedAt).TotalHours);

                    _ = _notificationService.SendAsync(
                        NotificationType.ShiftLeftOpen,
                        $"Cashier shift left open for {hoursOpen}h",
                        $"{shift.CashierName}'s shift opened on {shift.OpenedAt:yyyy-MM-dd HH:mm} UTC has not been closed. Please reconcile.",
                        referenceId,
                        NotifyRoles,
                        shift.TenantId);

                    alerted++;
                    _logger.LogWarning("[StaleShiftAlert] Shift {ShiftId} open {HoursOpen}h for tenant {TenantId}", shift.Id, hoursOpen, shift.TenantId);
                }

                return new StaleCashierShiftAlertJobResult
                {
                    Message = "Stale-shift alert completed.",
                    ShiftsAlerted = alerted
                };
            }
            finally
            {
                await ReleaseLockAsync();
                await _context.Database.CloseConnectionAsync();
            }
        }

        private async Task<bool> TryAcquireLockAsync(CancellationToken cancellationToken)
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);

            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT pg_try_advisory_lock({AdvisoryLockKey})";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is bool acquired && acquired;
        }

        private async Task ReleaseLockAsync()
        {
            if (_context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            {
                return;
            }

            try
            {
                await using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = $"SELECT pg_advisory_unlock({AdvisoryLockKey})";
                await command.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[StaleShiftAlert] Failed to release advisory lock.");
            }
        }
    }
}
