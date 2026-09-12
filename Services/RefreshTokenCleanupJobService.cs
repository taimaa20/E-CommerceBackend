using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public interface IRefreshTokenCleanupJobService
    {
        Task<RefreshTokenCleanupJobResult> RunAsync(CancellationToken cancellationToken = default);
    }

    public sealed class RefreshTokenCleanupJobResult
    {
        public string Message { get; init; } = string.Empty;
        public int TokensDeleted { get; init; }
        public bool SkippedBecauseAlreadyRunning { get; init; }
    }

    /// <summary>
    /// Purges refresh tokens that are no longer usable:
    ///   • Expired (ExpiresAt in the past)
    ///   • Revoked more than <see cref="RevokedRetentionDays"/> days ago (kept briefly for audit)
    /// Non-destructive for active sessions — expired/revoked tokens cannot authenticate anyway.
    /// </summary>
    public class RefreshTokenCleanupJobService : IRefreshTokenCleanupJobService
    {
        private const long AdvisoryLockKey = 2026042002;
        private const int RevokedRetentionDays = 30;

        private readonly PosDbContext _context;
        private readonly ILogger<RefreshTokenCleanupJobService> _logger;

        public RefreshTokenCleanupJobService(PosDbContext context, ILogger<RefreshTokenCleanupJobService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<RefreshTokenCleanupJobResult> RunAsync(CancellationToken cancellationToken = default)
        {
            var lockAcquired = await TryAcquireLockAsync(cancellationToken);
            if (!lockAcquired)
            {
                await _context.Database.CloseConnectionAsync();
                _logger.LogInformation("[RefreshTokenCleanup] Skipped — another runner holds the advisory lock.");
                return new RefreshTokenCleanupJobResult
                {
                    Message = "Refresh-token cleanup already running.",
                    SkippedBecauseAlreadyRunning = true
                };
            }

            try
            {
                var nowUtc = DateTime.UtcNow;
                var revokedCutoff = nowUtc.AddDays(-RevokedRetentionDays);

                var deleted = await _context.RefreshTokens
                    .Where(t => t.ExpiresAt < nowUtc
                             || (t.RevokedAt != null && t.RevokedAt < revokedCutoff))
                    .ExecuteDeleteAsync(cancellationToken);

                _logger.LogInformation("[RefreshTokenCleanup] Deleted {Count} stale token(s).", deleted);

                return new RefreshTokenCleanupJobResult
                {
                    Message = "Refresh-token cleanup completed.",
                    TokensDeleted = deleted
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
                _logger.LogWarning(ex, "[RefreshTokenCleanup] Failed to release advisory lock.");
            }
        }
    }
}
