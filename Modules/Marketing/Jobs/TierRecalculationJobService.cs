using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    public sealed record TierRecalculationResult(int WalletsEvaluated, int TierChanges, bool SkippedBecauseAlreadyRunning = false);

    /// <summary>Batch, idempotent tier promotion/demotion across all tenants. Preserves history.</summary>
    public interface ITierRecalculationJobService
    {
        Task<TierRecalculationResult> RunAsync(CancellationToken ct);
    }

    /// <inheritdoc cref="ITierRecalculationJobService"/>
    public sealed class TierRecalculationJobService : ITierRecalculationJobService
    {
        private const int BatchSize = 200;

        private readonly PosDbContext _context;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMarketingAuditLogger _audit;
        private readonly IAdvisoryLockService _lock;
        private readonly ILogger<TierRecalculationJobService> _logger;

        public TierRecalculationJobService(
            PosDbContext context,
            ICurrentUserAccessor currentUser,
            IMarketingAuditLogger audit,
            IAdvisoryLockService @lock,
            ILogger<TierRecalculationJobService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _lock = @lock ?? throw new ArgumentNullException(nameof(@lock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TierRecalculationResult> RunAsync(CancellationToken ct)
        {
            await using var handle = await _lock.TryAcquireAsync(LoyaltyLockKeys.TierRecalculation, ct);
            if (handle is null)
            {
                _logger.LogInformation("[TierRecalc] Skipped — another runner holds the lock.");
                return new TierRecalculationResult(0, 0, true);
            }

            var now = DateTime.UtcNow;
            var evaluated = 0;
            var changes = 0;

            var tenantIds = await _context.LoyaltyTiers.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.IsActive)
                .Select(t => t.TenantId)
                .Distinct()
                .ToListAsync(ct);

            foreach (var tenantId in tenantIds)
            {
                ct.ThrowIfCancellationRequested();

                var tiers = await _context.LoyaltyTiers.IgnoreQueryFilters().AsNoTracking()
                    .Where(t => t.TenantId == tenantId && t.IsActive)
                    .OrderBy(t => t.SortOrder)
                    .ToListAsync(ct);
                if (tiers.Count == 0) continue;

                var demotionEnabled = await _context.MarketingSettings.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => s.TenantId == tenantId)
                    .Select(s => (bool?)s.TierDemotionEnabled)
                    .FirstOrDefaultAsync(ct) ?? false;

                var offset = 0;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    var batch = await _context.CustomerWallets.IgnoreQueryFilters().AsNoTracking()
                        .Where(w => w.TenantId == tenantId && w.Status != WalletStatus.Closed)
                        .OrderBy(w => w.Id)
                        .Skip(offset).Take(BatchSize)
                        .ToListAsync(ct);
                    if (batch.Count == 0) break;

                    evaluated += batch.Count;

                    var pending = batch
                        .Select(w => (Wallet: w, Target: TierResolver.Resolve(
                            w.LifetimePoints, w.LifetimeSpend, w.CurrentTierId, tiers, demotionEnabled)))
                        .Where(x => x.Target is not null && x.Target.Id != x.Wallet.CurrentTierId)
                        .ToList();

                    if (pending.Count > 0)
                        changes += await ApplyBatchAsync(tenantId, pending, now, ct);

                    offset += batch.Count;
                    if (batch.Count < BatchSize) break;
                }
            }

            return new TierRecalculationResult(evaluated, changes);
        }

        private async Task<int> ApplyBatchAsync(
            Guid tenantId,
            List<(CustomerWallet Wallet, LoyaltyTier? Target)> pending,
            DateTime now,
            CancellationToken ct)
        {
            var walletIds = pending.Select(p => p.Wallet.Id).ToList();
            var customerIds = pending.Select(p => p.Wallet.CustomerId).Distinct().ToList();

            var wallets = await _context.CustomerWallets.IgnoreQueryFilters()
                .Where(w => walletIds.Contains(w.Id)).ToListAsync(ct);
            var customers = await _context.Customers.IgnoreQueryFilters()
                .Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

            var applied = 0;
            foreach (var (snapshot, target) in pending)
            {
                if (target is null) continue;
                var wallet = wallets.FirstOrDefault(w => w.Id == snapshot.Id);
                if (wallet is null || wallet.CurrentTierId == target.Id) continue;

                customers.TryGetValue(wallet.CustomerId, out var customer);
                var previousLegacy = customer?.Tier ?? CustomerTier.Standard;
                var previousTierId = wallet.CurrentTierId;

                wallet.CurrentTierId = target.Id;
                wallet.UpdatedByUserId = _currentUser.UserIdOrNull;
                if (customer is not null) customer.Tier = target.LegacyTierEnum;

                _context.CustomerTierHistories.Add(new CustomerTierHistory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = wallet.BranchId,
                    CustomerId = wallet.CustomerId,
                    WalletId = wallet.Id,
                    PreviousTierId = previousTierId,
                    NewTierId = target.Id,
                    PreviousLegacyTier = previousLegacy,
                    NewLegacyTier = target.LegacyTierEnum,
                    Trigger = TierChangeTrigger.Recalculation,
                    LifetimePointsAtChange = wallet.LifetimePoints,
                    LifetimeSpendAtChange = wallet.LifetimeSpend,
                    ChangedAt = now,
                    CreatedByUserId = _currentUser.UserIdOrNull
                });

                _audit.Track(tenantId, wallet.BranchId, MarketingAuditAction.Updated,
                    "CustomerTier", wallet.CustomerId, wallet.CustomerId,
                    $"tier:{previousLegacy}->{target.LegacyTierEnum};trigger=Recalculation");

                applied++;
            }

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "[TierRecalc] Concurrency conflict on a batch for tenant {TenantId}; skipping batch.", tenantId);
                applied = 0;
            }
            finally
            {
                _context.ChangeTracker.Clear();
            }

            return applied;
        }
    }
}
