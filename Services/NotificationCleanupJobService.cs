using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public interface INotificationCleanupJobService
    {
        Task<NotificationCleanupJobResult> RunAsync(CancellationToken cancellationToken = default);
    }

    public sealed class NotificationCleanupJobResult
    {
        public string Message { get; init; } = string.Empty;
        public int NotificationsDeleted { get; init; }
        public bool SkippedBecauseAlreadyRunning { get; init; }
    }

    /// <summary>
    /// Weekly hygiene pass on the Notifications table.
    /// Deletes notifications that satisfy BOTH:
    ///   • IsRead = true
    ///   • CreatedAt older than <see cref="ReadRetentionDays"/>
    /// Also hard-deletes unread notifications older than <see cref="UnreadRetentionDays"/>
    /// (safety net — unread items that old are stale anyway).
    /// </summary>
    public class NotificationCleanupJobService : INotificationCleanupJobService
    {
        private const long AdvisoryLockKey = 2026042005;
        private const int ReadRetentionDays = 30;
        private const int UnreadRetentionDays = 180;

        private readonly PosDbContext _context;
        private readonly ILogger<NotificationCleanupJobService> _logger;

        public NotificationCleanupJobService(
            PosDbContext context,
            ILogger<NotificationCleanupJobService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<NotificationCleanupJobResult> RunAsync(CancellationToken cancellationToken = default)
        {
            var lockAcquired = await TryAcquireLockAsync(cancellationToken);
            if (!lockAcquired)
            {
                await _context.Database.CloseConnectionAsync();
                _logger.LogInformation("[NotificationCleanup] Skipped — another runner holds the advisory lock.");
                return new NotificationCleanupJobResult
                {
                    Message = "Notification cleanup already running.",
                    SkippedBecauseAlreadyRunning = true
                };
            }

            try
            {
                var nowUtc = DateTime.UtcNow;
                var readCutoff = nowUtc.AddDays(-ReadRetentionDays);
                var unreadCutoff = nowUtc.AddDays(-UnreadRetentionDays);

                var deleted = await _context.Notifications
                    .IgnoreQueryFilters()
                    .Where(n => (n.IsRead && n.CreatedAt < readCutoff)
                             || (!n.IsRead && n.CreatedAt < unreadCutoff))
                    .ExecuteDeleteAsync(cancellationToken);

                _logger.LogInformation("[NotificationCleanup] Deleted {Count} notification(s).", deleted);

                return new NotificationCleanupJobResult
                {
                    Message = "Notification cleanup completed.",
                    NotificationsDeleted = deleted
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
                _logger.LogWarning(ex, "[NotificationCleanup] Failed to release advisory lock.");
            }
        }
    }
}
