using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>Recomputes the materialized membership of one dynamic segment. Idempotent.</summary>
    public interface ISegmentComputationEngine
    {
        Task<SegmentComputeResultDto> ComputeAsync(CustomerSegment segment, CancellationToken ct);
    }

    /// <inheritdoc cref="ISegmentComputationEngine"/>
    public sealed class SegmentComputationEngine : ISegmentComputationEngine
    {
        private const int CandidateBatch = 500;

        private readonly PosDbContext _context;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMarketingAuditLogger _audit;

        public SegmentComputationEngine(
            PosDbContext context,
            ICurrentUserAccessor currentUser,
            IMarketingAuditLogger audit)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public async Task<SegmentComputeResultDto> ComputeAsync(CustomerSegment segment, CancellationToken ct)
        {
            var result = new SegmentComputeResultDto { SegmentId = segment.Id };

            // Static segments are curated by hand — never reconciled by the engine.
            if (segment.Type != CustomerSegmentType.Dynamic) return result;

            var rules = await _context.SegmentRules.IgnoreQueryFilters().AsNoTracking()
                .Where(r => r.SegmentId == segment.Id)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            var matched = rules.Count == 0
                ? new HashSet<Guid>()
                : await EvaluateMatchesAsync(segment.TenantId, rules, now, ct);
            result.Matched = matched.Count;

            var existingComputed = (await _context.SegmentMembers.IgnoreQueryFilters().AsNoTracking()
                    .Where(m => m.SegmentId == segment.Id && m.Source == SegmentMemberSource.Computed)
                    .Select(m => m.CustomerId)
                    .ToListAsync(ct))
                .ToHashSet();

            // Manual members are sacrosanct — never auto-removed even if they no longer match.
            var manual = (await _context.SegmentMembers.IgnoreQueryFilters().AsNoTracking()
                    .Where(m => m.SegmentId == segment.Id && m.Source != SegmentMemberSource.Computed)
                    .Select(m => m.CustomerId)
                    .ToListAsync(ct))
                .ToHashSet();

            var toAdd = matched.Where(id => !existingComputed.Contains(id) && !manual.Contains(id)).ToList();
            var toRemove = existingComputed.Where(id => !matched.Contains(id)).ToList();

            foreach (var customerId in toAdd)
                _context.SegmentMembers.Add(new SegmentMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = segment.TenantId,
                    BranchId = segment.BranchId,
                    SegmentId = segment.Id,
                    CustomerId = customerId,
                    AddedAt = now,
                    Source = SegmentMemberSource.Computed,
                    CreatedByUserId = _currentUser.UserIdOrNull
                });

            if (toRemove.Count > 0)
                await _context.SegmentMembers.IgnoreQueryFilters()
                    .Where(m => m.SegmentId == segment.Id
                                && m.Source == SegmentMemberSource.Computed
                                && toRemove.Contains(m.CustomerId))
                    .ExecuteDeleteAsync(ct);

            var tracked = await _context.CustomerSegments.IgnoreQueryFilters().FirstAsync(s => s.Id == segment.Id, ct);
            tracked.LastComputedAt = now;
            tracked.UpdatedByUserId = _currentUser.UserIdOrNull;

            _audit.Track(segment.TenantId, segment.BranchId, MarketingAuditAction.Updated,
                nameof(CustomerSegment), segment.Id, null,
                $"segment-compute;matched={matched.Count};added={toAdd.Count};removed={toRemove.Count}");

            await _context.SaveChangesAsync(ct);

            result.Added = toAdd.Count;
            result.Removed = toRemove.Count;
            return result;
        }

        private async Task<HashSet<Guid>> EvaluateMatchesAsync(
            Guid tenantId, List<SegmentRule> rules, DateTime now, CancellationToken ct)
        {
            var matched = new HashSet<Guid>();
            var offset = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // Loyalty universe = customers with a wallet. Left-join keeps wallet-less customers
                // out of wallet-based rules naturally (their aggregates read as zero/none).
                var batch = await (
                    from c in _context.Customers.IgnoreQueryFilters().AsNoTracking()
                    where c.TenantId == tenantId
                    join w in _context.CustomerWallets.IgnoreQueryFilters().AsNoTracking()
                        on c.Id equals w.CustomerId into wj
                    from w in wj.DefaultIfEmpty()
                    orderby c.Id
                    select new SegmentEvaluationSnapshot(
                        c.Id,
                        w != null ? w.LifetimeSpend : 0m,
                        w != null ? w.TotalOrders : 0,
                        w != null ? w.TotalVisits : 0,
                        w != null ? w.AvailablePoints : 0m,
                        w != null ? w.CurrentTierId : null,
                        c.LastVisit))
                    .Skip(offset).Take(CandidateBatch)
                    .ToListAsync(ct);

                if (batch.Count == 0) break;

                foreach (var snapshot in batch)
                    if (SegmentEvaluator.Matches(snapshot, rules, now))
                        matched.Add(snapshot.CustomerId);

                offset += batch.Count;
                if (batch.Count < CandidateBatch) break;
            }

            return matched;
        }
    }
}
