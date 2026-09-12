using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    public sealed record PendingApprovalResult(int Approved, decimal PointsApproved, bool SkippedBecauseAlreadyRunning);

    /// <summary>Approves due pending earns across all tenants. Distributed-safe, batched, idempotent.</summary>
    public interface IPendingApprovalJobService
    {
        Task<PendingApprovalResult> RunAsync(CancellationToken ct);
    }

    /// <inheritdoc cref="IPendingApprovalJobService"/>
    public sealed class PendingApprovalJobService : IPendingApprovalJobService
    {
        private const int BatchSize = 200;

        private readonly PosDbContext _context;
        private readonly IWalletService _wallet;
        private readonly IAdvisoryLockService _lock;
        private readonly ILogger<PendingApprovalJobService> _logger;

        public PendingApprovalJobService(
            PosDbContext context,
            IWalletService wallet,
            IAdvisoryLockService @lock,
            ILogger<PendingApprovalJobService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _lock = @lock ?? throw new ArgumentNullException(nameof(@lock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PendingApprovalResult> RunAsync(CancellationToken ct)
        {
            await using var handle = await _lock.TryAcquireAsync(LoyaltyLockKeys.PendingApproval, ct);
            if (handle is null)
            {
                _logger.LogInformation("[PendingApproval] Skipped — another runner holds the lock.");
                return new PendingApprovalResult(0, 0m, true);
            }

            var now = DateTime.UtcNow;
            var approved = 0;
            var pointsApproved = 0m;
            var attempted = new HashSet<Guid>();

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // Candidate pending earns + the approval config from their rule version + order state.
                var candidates = await (
                    from t in _context.WalletTransactions.IgnoreQueryFilters().AsNoTracking()
                    where t.Type == WalletTransactionType.EarnPendingApproved
                          && t.Status == WalletTransactionStatus.Pending
                    join r in _context.EarningRules.IgnoreQueryFilters().AsNoTracking()
                        on t.RuleVersionId equals r.Id into rj
                    from r in rj.DefaultIfEmpty()
                    join o in _context.Orders.IgnoreQueryFilters().AsNoTracking()
                        on t.OrderId equals o.Id into oj
                    from o in oj.DefaultIfEmpty()
                    orderby t.CreatedAt
                    select new
                    {
                        t.Id,
                        t.CreatedAt,
                        ApprovalMode = r != null ? r.ApprovalMode : PointsApprovalMode.Immediate,
                        ApprovalDelayDays = r != null ? r.ApprovalDelayDays : null,
                        OrderStatus = (Models.OrderStatus?)(o != null ? o.Status : (Models.OrderStatus?)null)
                    })
                    .Take(BatchSize)
                    .ToListAsync(ct);

                var fresh = candidates.Where(c => !attempted.Contains(c.Id)).ToList();
                if (fresh.Count == 0) break;

                foreach (var c in fresh)
                {
                    ct.ThrowIfCancellationRequested();
                    attempted.Add(c.Id);

                    var due = PendingApprovalPolicy.IsDue(new PendingApprovalContext
                    {
                        ApprovalMode = c.ApprovalMode,
                        ApprovalDelayDays = c.ApprovalDelayDays,
                        EarnedAtUtc = c.CreatedAt,
                        OrderStatus = c.OrderStatus
                    }, now);

                    if (!due) continue;

                    try
                    {
                        var points = await _wallet.ApprovePendingAsync(c.Id, c.ApprovalMode.ToString(), ct);
                        if (points > 0)
                        {
                            approved++;
                            pointsApproved += points;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[PendingApproval] Failed to approve pending txn {TxnId}", c.Id);
                    }
                }
            }

            return new PendingApprovalResult(approved, pointsApproved, false);
        }
    }
}
