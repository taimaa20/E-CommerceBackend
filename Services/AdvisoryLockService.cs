using System.Data;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Reusable PostgreSQL session-advisory-lock framework so background jobs run single-instance
    /// across a scaled / blue-green Azure App Service deployment. Mirrors the inline pattern used by
    /// the existing cleanup jobs (pg_try_advisory_lock) but as one shared, testable abstraction that
    /// every future job can consume instead of re-implementing.
    /// </summary>
    public interface IAdvisoryLockService
    {
        /// <summary>
        /// Tries to take the session advisory lock for <paramref name="key"/>. Returns a handle that
        /// releases the lock (and closes the connection) on dispose, or <c>null</c> if another runner
        /// already holds it. The lock is bound to the scoped DbContext connection, which is held open
        /// for the lock's lifetime — so all work done within the handle scope is serialized cluster-wide.
        /// </summary>
        Task<IAsyncDisposable?> TryAcquireAsync(long key, CancellationToken ct);
    }

    /// <inheritdoc cref="IAdvisoryLockService"/>
    public sealed class AdvisoryLockService : IAdvisoryLockService
    {
        private readonly PosDbContext _context;
        private readonly ILogger<AdvisoryLockService> _logger;

        public AdvisoryLockService(PosDbContext context, ILogger<AdvisoryLockService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IAsyncDisposable?> TryAcquireAsync(long key, CancellationToken ct)
        {
            await _context.Database.OpenConnectionAsync(ct);

            await using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                // key is an internal constant, never user input — safe to interpolate.
                command.CommandText = $"SELECT pg_try_advisory_lock({key})";
                var result = await command.ExecuteScalarAsync(ct);
                if (result is bool acquired && acquired)
                    return new LockHandle(_context, key, _logger);
            }

            await _context.Database.CloseConnectionAsync();
            return null;
        }

        private sealed class LockHandle : IAsyncDisposable
        {
            private readonly PosDbContext _context;
            private readonly long _key;
            private readonly ILogger _logger;

            public LockHandle(PosDbContext context, long key, ILogger logger)
            {
                _context = context;
                _key = key;
                _logger = logger;
            }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    if (_context.Database.GetDbConnection().State == ConnectionState.Open)
                    {
                        await using var command = _context.Database.GetDbConnection().CreateCommand();
                        command.CommandText = $"SELECT pg_advisory_unlock({_key})";
                        await command.ExecuteScalarAsync();
                    }
                }
                catch (Exception ex)
                {
                    // The lock auto-releases when the session ends, so a failed explicit unlock is non-fatal.
                    _logger.LogWarning(ex, "[AdvisoryLock] Failed to release advisory lock {Key}.", _key);
                }
                finally
                {
                    await _context.Database.CloseConnectionAsync();
                }
            }
        }
    }

    /// <summary>Stable, non-colliding advisory-lock keys for the loyalty background jobs.</summary>
    public static class LoyaltyLockKeys
    {
        public const long Expiration = 2026070001;
        public const long TierRecalculation = 2026070002;
        public const long SegmentComputation = 2026070003;
        public const long PendingApproval = 2026070004;
        public const long CampaignProcessing = 2026070005;
    }
}
