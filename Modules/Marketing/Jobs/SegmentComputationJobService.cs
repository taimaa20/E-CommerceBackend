using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    public sealed record SegmentComputationResult(int SegmentsProcessed, int MembersAdded, int MembersRemoved, bool SkippedBecauseAlreadyRunning = false);

    public interface ISegmentComputationJobService
    {
        Task<SegmentComputationResult> RunAsync(CancellationToken ct);
    }

    /// <inheritdoc cref="ISegmentComputationJobService"/>
    public sealed class SegmentComputationJobService : ISegmentComputationJobService
    {
        private readonly PosDbContext _context;
        private readonly ISegmentComputationEngine _engine;
        private readonly IAdvisoryLockService _lock;
        private readonly ILogger<SegmentComputationJobService> _logger;

        public SegmentComputationJobService(
            PosDbContext context,
            ISegmentComputationEngine engine,
            IAdvisoryLockService @lock,
            ILogger<SegmentComputationJobService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _lock = @lock ?? throw new ArgumentNullException(nameof(@lock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SegmentComputationResult> RunAsync(CancellationToken ct)
        {
            await using var handle = await _lock.TryAcquireAsync(LoyaltyLockKeys.SegmentComputation, ct);
            if (handle is null)
            {
                _logger.LogInformation("[SegmentComputation] Skipped — another runner holds the lock.");
                return new SegmentComputationResult(0, 0, 0, true);
            }

            var processed = 0;
            var added = 0;
            var removed = 0;

            // Snapshot the work list up front (ids only) so the engine's per-segment SaveChanges
            // can't disturb iteration.
            var segmentIds = await _context.CustomerSegments.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.IsActive && s.Type == CustomerSegmentType.Dynamic)
                .Select(s => s.Id)
                .ToListAsync(ct);

            foreach (var segmentId in segmentIds)
            {
                ct.ThrowIfCancellationRequested();

                var segment = await _context.CustomerSegments.IgnoreQueryFilters().AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == segmentId, ct);
                if (segment is null) continue;

                try
                {
                    var result = await _engine.ComputeAsync(segment, ct);
                    processed++;
                    added += result.Added;
                    removed += result.Removed;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SegmentComputation] Failed to compute segment {SegmentId}", segmentId);
                }
            }

            return new SegmentComputationResult(processed, added, removed);
        }
    }
}
