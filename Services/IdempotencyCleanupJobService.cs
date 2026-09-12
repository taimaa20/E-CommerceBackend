using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public interface IIdempotencyCleanupJobService
    {
        Task<IdempotencyCleanupJobResult> RunAsync(CancellationToken cancellationToken = default);
    }

    public sealed class IdempotencyCleanupJobResult
    {
        public string Message { get; init; } = string.Empty;
        public int EntriesDeleted { get; init; }
        public int Batches { get; init; }
        public bool SkippedBecauseAlreadyRunning { get; init; }
    }

    /// <summary>
    /// Purges <see cref="IdempotencyEntry"/> rows whose work is long-done and
    /// can no longer participate in any client replay window.
    ///
    /// Safety contract:
    ///   • Only deletes rows in state <c>Completed</c>. Rows still in
    ///     <c>Processing</c> are NEVER touched, even if their TTL has
    ///     elapsed — those belong to the middleware's reconciliation path
    ///     (see <see cref="RestaurantPos.Api.Middleware.IdempotencyMiddleware"/>).
    ///   • Only deletes rows older than the configured retention window
    ///     (default 7 days) so the offline queue's effective replay window
    ///     is fully covered. The offline retry backoff caps at ~120 s and
    ///     real-world restaurant outages rarely exceed a single day, so 7
    ///     days is a wide margin.
    ///   • Deletes in capped batches (default 1000 rows / batch) so a
    ///     long-accumulated backlog can't take a row-lock long enough to
    ///     interfere with live POST/PUT traffic flowing through the
    ///     middleware. Each batch is its own transaction.
    ///   • Acquires a PostgreSQL advisory lock so two app instances (blue/green
    ///     deploy, scaled web tier) never run the job concurrently.
    ///
    /// All thresholds are configurable through <c>Idempotency:Cleanup</c> in
    /// appsettings:
    ///   <code>
    ///   "Idempotency": {
    ///     "Cleanup": {
    ///       "RetentionDays": 7,
    ///       "BatchSize": 1000,
    ///       "MaxBatchesPerRun": 50
    ///     }
    ///   }
    ///   </code>
    /// </summary>
    public class IdempotencyCleanupJobService : IIdempotencyCleanupJobService
    {
        // Distinct advisory-lock key from the other cleanup jobs in the codebase
        // (RefreshTokenCleanup uses 2026042002) so the locks don't collide.
        private const long AdvisoryLockKey = 2026061101;

        private const int DefaultRetentionDays = 7;
        private const int DefaultBatchSize = 1_000;
        private const int DefaultMaxBatchesPerRun = 50;

        private readonly PosDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<IdempotencyCleanupJobService> _logger;

        public IdempotencyCleanupJobService(
            PosDbContext context,
            IConfiguration configuration,
            ILogger<IdempotencyCleanupJobService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IdempotencyCleanupJobResult> RunAsync(CancellationToken cancellationToken = default)
        {
            var lockAcquired = await TryAcquireLockAsync(cancellationToken);
            if (!lockAcquired)
            {
                await _context.Database.CloseConnectionAsync();
                _logger.LogInformation("[IdempotencyCleanup] Skipped — another runner holds the advisory lock.");
                return new IdempotencyCleanupJobResult
                {
                    Message = "Idempotency cleanup already running.",
                    SkippedBecauseAlreadyRunning = true
                };
            }

            try
            {
                var retentionDays = _configuration.GetValue<int?>("Idempotency:Cleanup:RetentionDays") ?? DefaultRetentionDays;
                var batchSize = _configuration.GetValue<int?>("Idempotency:Cleanup:BatchSize") ?? DefaultBatchSize;
                var maxBatchesPerRun = _configuration.GetValue<int?>("Idempotency:Cleanup:MaxBatchesPerRun") ?? DefaultMaxBatchesPerRun;

                // Defensive clamping — bad config values must not turn this
                // into either a no-op or a runaway full-table-scan delete.
                retentionDays = Math.Max(1, retentionDays);
                batchSize = Math.Clamp(batchSize, 100, 10_000);
                maxBatchesPerRun = Math.Clamp(maxBatchesPerRun, 1, 1_000);

                var cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);

                _logger.LogInformation(
                    "[IdempotencyCleanup] Starting. RetentionDays={RetentionDays} BatchSize={BatchSize} MaxBatches={MaxBatches} Cutoff={Cutoff:O}",
                    retentionDays, batchSize, maxBatchesPerRun, cutoffUtc);

                var totalDeleted = 0;
                var batches = 0;

                while (batches < maxBatchesPerRun)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // ExecuteDeleteAsync with a Take() projection generates a
                    // bounded DELETE — PostgreSQL handles `DELETE … WHERE id IN
                    // (SELECT id … LIMIT n)` efficiently and keeps the
                    // per-batch lock window short. Critically we filter on:
                    //   • State == Completed       → never touch in-flight rows
                    //   • CompletedAt < cutoff      → never touch rows that
                    //                                  could still serve a replay
                    var deletedThisBatch = await _context.IdempotencyEntries
                        .Where(e => e.State == IdempotencyEntryState.Completed
                                 && e.CompletedAt != null
                                 && e.CompletedAt < cutoffUtc)
                        .OrderBy(e => e.CompletedAt)
                        .Take(batchSize)
                        .ExecuteDeleteAsync(cancellationToken);

                    if (deletedThisBatch == 0) break;

                    totalDeleted += deletedThisBatch;
                    batches++;

                    _logger.LogInformation(
                        "[IdempotencyCleanup] Batch {Batch} removed {Deleted} entries.",
                        batches, deletedThisBatch);
                }

                _logger.LogInformation(
                    "[IdempotencyCleanup] Completed. TotalDeleted={Total} Batches={Batches}",
                    totalDeleted, batches);

                return new IdempotencyCleanupJobResult
                {
                    Message = "Idempotency cleanup completed.",
                    EntriesDeleted = totalDeleted,
                    Batches = batches
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
                _logger.LogWarning(ex, "[IdempotencyCleanup] Failed to release advisory lock.");
            }
        }
    }
}
