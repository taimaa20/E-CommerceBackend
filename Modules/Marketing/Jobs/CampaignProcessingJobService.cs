using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    public sealed record CampaignProcessingResult(
        int CampaignsAdvanced, int CampaignsProcessed, int AwardsIssued, decimal PointsIssued, bool SkippedBecauseAlreadyRunning);

    public interface ICampaignProcessingJobService
    {
        Task<CampaignProcessingResult> RunAsync(CancellationToken ct);
    }

    /// <summary>
    /// Auto-advances campaign lifecycle and issues campaign bonus points by feeding the existing
    /// <see cref="IWalletService.CreditAsync"/> — no new earning/wallet logic. Distributed-safe,
    /// idempotent (one award per order/customer via the credit idempotency key), retry-safe.
    /// </summary>
    public sealed class CampaignProcessingJobService : ICampaignProcessingJobService
    {
        private const int OrderPage = 500;
        private const int CustomerPage = 500;

        private readonly PosDbContext _context;
        private readonly IWalletService _wallet;
        private readonly IMarketingAuditLogger _audit;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IAdvisoryLockService _lock;
        private readonly ILogger<CampaignProcessingJobService> _logger;

        public CampaignProcessingJobService(
            PosDbContext context,
            IWalletService wallet,
            IMarketingAuditLogger audit,
            ICurrentUserAccessor currentUser,
            IAdvisoryLockService @lock,
            ILogger<CampaignProcessingJobService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _lock = @lock ?? throw new ArgumentNullException(nameof(@lock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CampaignProcessingResult> RunAsync(CancellationToken ct)
        {
            await using var handle = await _lock.TryAcquireAsync(LoyaltyLockKeys.CampaignProcessing, ct);
            if (handle is null)
            {
                _logger.LogInformation("[Campaign] Skipped — another runner holds the lock.");
                return new CampaignProcessingResult(0, 0, 0, 0m, true);
            }

            var now = DateTime.UtcNow;
            var advanced = await AutoAdvanceAsync(now, ct);

            var activeCampaigns = await _context.Campaigns.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.Status == CampaignStatus.Active && c.IsActive
                            && c.StartDate <= now && c.EndDate > now)
                .OrderByDescending(c => c.Priority)
                .ToListAsync(ct);

            var processed = 0;
            var awards = 0;
            var points = 0m;

            foreach (var campaign in activeCampaigns)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var (a, p) = campaign.Type == CampaignType.FixedReward
                        ? await ProcessThresholdAsync(campaign, now, ct)
                        : await ProcessPerOrderAsync(campaign, now, ct);
                    awards += a;
                    points += p;
                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Campaign] Failed processing campaign {CampaignId}", campaign.Id);
                }
            }

            return new CampaignProcessingResult(advanced, processed, awards, points, false);
        }

        private async Task<int> AutoAdvanceAsync(DateTime now, CancellationToken ct)
        {
            var pending = await _context.Campaigns.IgnoreQueryFilters()
                .Where(c => c.Status == CampaignStatus.Scheduled || c.Status == CampaignStatus.Active)
                .ToListAsync(ct);

            var changed = 0;
            foreach (var c in pending)
            {
                var next = CampaignStatusPolicy.AutoAdvance(c.Status, c.StartDate, c.EndDate, now);
                if (next is null) continue;

                var previous = c.Status;
                c.Status = next.Value;
                _audit.Track(c.TenantId, c.BranchId, MarketingAuditAction.Updated,
                    nameof(Campaign), c.Id, null, $"auto-status:{previous}->{next.Value}");
                changed++;
            }
            if (changed > 0) await _context.SaveChangesAsync(ct);
            return changed;
        }

        private async Task<(int Awards, decimal Points)> ProcessPerOrderAsync(Campaign campaign, DateTime now, CancellationToken ct)
        {
            var targetSet = await ResolveTargetSetAsync(campaign, ct);
            if (targetSet is { Count: 0 }) return (0, 0m);

            var expiresAt = ResolveExpiry(campaign, now);
            var awards = 0;
            var points = 0m;
            var offset = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var query = _context.Orders.IgnoreQueryFilters().AsNoTracking()
                    .Where(o => o.TenantId == campaign.TenantId
                                && o.CustomerId != null
                                && (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Completed)
                                && o.CreatedAt >= campaign.StartDate && o.CreatedAt <= campaign.EndDate);

                if (targetSet is not null)
                    query = query.Where(o => targetSet.Contains(o.CustomerId!.Value));

                var orders = await query
                    .OrderBy(o => o.Id)
                    .Skip(offset).Take(OrderPage)
                    .Select(o => new { o.Id, o.CustomerId, o.Subtotal })
                    .ToListAsync(ct);

                if (orders.Count == 0) break;

                foreach (var o in orders)
                {
                    var bonus = CampaignAwardCalculator.PerOrderBonus(
                        campaign.Type, o.Subtotal, campaign.BonusPoints, campaign.SpendThreshold,
                        campaign.Multiplier, campaign.PointsPerCurrencyUnit);
                    if (bonus <= 0) continue;

                    if (await CreditAsync(campaign, o.CustomerId!.Value, bonus,
                            $"campaign:{campaign.Id}:order:{o.Id}", o.Id, expiresAt, ct))
                    {
                        awards++;
                        points += bonus;
                    }
                }

                offset += orders.Count;
                if (orders.Count < OrderPage) break;
            }

            return (awards, points);
        }

        private async Task<(int Awards, decimal Points)> ProcessThresholdAsync(Campaign campaign, DateTime now, CancellationToken ct)
        {
            var targetIds = await ResolveTargetListAsync(campaign, ct);
            if (targetIds.Count == 0) return (0, 0m);

            var expiresAt = ResolveExpiry(campaign, now);
            var awards = 0;
            var points = 0m;

            foreach (var chunk in targetIds.Chunk(CustomerPage))
            {
                ct.ThrowIfCancellationRequested();

                var counts = await _context.Orders.IgnoreQueryFilters().AsNoTracking()
                    .Where(o => o.TenantId == campaign.TenantId
                                && o.CustomerId != null && chunk.Contains(o.CustomerId.Value)
                                && (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Completed)
                                && o.CreatedAt >= campaign.StartDate && o.CreatedAt <= campaign.EndDate)
                    .GroupBy(o => o.CustomerId!.Value)
                    .Select(g => new { CustomerId = g.Key, Count = g.Count() })
                    .ToListAsync(ct);

                foreach (var c in counts)
                {
                    var reward = CampaignAwardCalculator.ThresholdReward(
                        campaign.Type, c.Count, campaign.OrderCountThreshold, campaign.BonusPoints);
                    if (reward <= 0) continue;

                    if (await CreditAsync(campaign, c.CustomerId, reward,
                            $"campaign:{campaign.Id}:threshold:{c.CustomerId}", orderId: null, expiresAt, ct))
                    {
                        awards++;
                        points += reward;
                    }
                }
            }

            return (awards, points);
        }

        private async Task<bool> CreditAsync(
            Campaign campaign, Guid customerId, decimal points, string idempotencyKey, Guid? orderId, DateTime? expiresAt, CancellationToken ct)
        {
            try
            {
                await _wallet.CreditAsync(new WalletCreditRequest
                {
                    CustomerId = customerId,
                    Points = points,
                    Type = WalletTransactionType.CampaignBonus,
                    Source = WalletTransactionSource.Campaign,
                    IsPending = false,
                    IdempotencyKey = idempotencyKey,
                    CampaignId = campaign.Id,
                    OrderId = orderId,
                    ExpiresAt = expiresAt,
                    Reason = $"Campaign: {campaign.Name}",
                    ReasonAr = campaign.NameAr is null ? null : $"حملة: {campaign.NameAr}"
                }, ct);
                return true;
            }
            catch (Exception ex)
            {
                // Frozen/closed wallet or transient issue — skip this award; re-runs are idempotent.
                _logger.LogWarning(ex, "[Campaign] Skipped award for customer {CustomerId} on campaign {CampaignId}", customerId, campaign.Id);
                return false;
            }
        }

        /// <summary>Null = no membership restriction (All); otherwise the eligible customer id set.</summary>
        private async Task<HashSet<Guid>?> ResolveTargetSetAsync(Campaign campaign, CancellationToken ct)
            => campaign.TargetType == CampaignTargetType.All ? null : (await ResolveTargetListAsync(campaign, ct)).ToHashSet();

        private async Task<List<Guid>> ResolveTargetListAsync(Campaign campaign, CancellationToken ct)
        {
            switch (campaign.TargetType)
            {
                case CampaignTargetType.Segment when campaign.TargetSegmentId is { } sid:
                    return await _context.SegmentMembers.IgnoreQueryFilters().AsNoTracking()
                        .Where(m => m.SegmentId == sid).Select(m => m.CustomerId).Distinct().ToListAsync(ct);

                case CampaignTargetType.CustomerList:
                    return await _context.CampaignCustomers.IgnoreQueryFilters().AsNoTracking()
                        .Where(cc => cc.CampaignId == campaign.Id).Select(cc => cc.CustomerId).Distinct().ToListAsync(ct);

                case CampaignTargetType.Tier when campaign.TargetTierId is { } tid:
                    var legacy = await _context.LoyaltyTiers.IgnoreQueryFilters().AsNoTracking()
                        .Where(t => t.Id == tid).Select(t => (CustomerTier?)t.LegacyTierEnum).FirstOrDefaultAsync(ct);
                    if (legacy is null) return new List<Guid>();
                    return await _context.Customers.IgnoreQueryFilters().AsNoTracking()
                        .Where(c => c.TenantId == campaign.TenantId && c.Tier == legacy).Select(c => c.Id).ToListAsync(ct);

                case CampaignTargetType.All:
                    return await _context.Customers.IgnoreQueryFilters().AsNoTracking()
                        .Where(c => c.TenantId == campaign.TenantId).Select(c => c.Id).ToListAsync(ct);

                default:
                    return new List<Guid>();
            }
        }

        private static DateTime? ResolveExpiry(Campaign campaign, DateTime now) => campaign.ExpirationMode switch
        {
            PointsExpirationMode.Days when campaign.ExpirationValue is { } v => now.AddDays(v),
            PointsExpirationMode.Months when campaign.ExpirationValue is { } v => now.AddMonths(v),
            PointsExpirationMode.Years when campaign.ExpirationValue is { } v => now.AddYears(v),
            _ => null
        };
    }
}
