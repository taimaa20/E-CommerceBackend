using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    public sealed record LoyaltyExpirationResult(int WalletsProcessed, int LotsExpired, bool SkippedBecauseAlreadyRunning = false);

    /// <summary>Batch, resumable, idempotent expiration of due points lots across all tenants.</summary>
    public interface ILoyaltyExpirationJobService
    {
        Task<LoyaltyExpirationResult> RunAsync(CancellationToken ct);
    }

    /// <inheritdoc cref="ILoyaltyExpirationJobService"/>
    public sealed class LoyaltyExpirationJobService : ILoyaltyExpirationJobService
    {
        private const int BatchSize = 200;

        private readonly PosDbContext _context;
        private readonly IWalletService _wallet;
        private readonly IAdvisoryLockService _lock;
        private readonly ILogger<LoyaltyExpirationJobService> _logger;

        public LoyaltyExpirationJobService(
            PosDbContext context,
            IWalletService wallet,
            IAdvisoryLockService @lock,
            ILogger<LoyaltyExpirationJobService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _lock = @lock ?? throw new ArgumentNullException(nameof(@lock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<LoyaltyExpirationResult> RunAsync(CancellationToken ct)
        {
            await using var handle = await _lock.TryAcquireAsync(LoyaltyLockKeys.Expiration, ct);
            if (handle is null)
            {
                _logger.LogInformation("[LoyaltyExpiration] Skipped — another runner holds the lock.");
                return new LoyaltyExpirationResult(0, 0, true);
            }

            var asOf = DateTime.UtcNow;
            var walletsProcessed = 0;
            var lotsExpired = 0;
            var attempted = new HashSet<Guid>();

            // Page over distinct wallets that still own due lots. Each wallet commits independently,
            // so a crash mid-run resumes cleanly; expired lots drop out of the filter on the next pass.
            // Wallets whose expiration throws stay due — the attempted-set ensures the loop still
            // terminates instead of re-fetching the same failing wallet forever.
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var walletIds = await _context.PointsLots.IgnoreQueryFilters().AsNoTracking()
                    .Where(l => l.Status == PointsLotStatus.Active
                                && l.RemainingPoints > 0
                                && l.ExpiresAt != null
                                && l.ExpiresAt <= asOf)
                    .Select(l => l.WalletId)
                    .Distinct()
                    .Take(BatchSize)
                    .ToListAsync(ct);

                var fresh = walletIds.Where(id => !attempted.Contains(id)).ToList();
                if (fresh.Count == 0) break;

                foreach (var walletId in fresh)
                {
                    ct.ThrowIfCancellationRequested();
                    attempted.Add(walletId);
                    try
                    {
                        lotsExpired += await _wallet.ExpireDueLotsAsync(walletId, asOf, ct);
                        walletsProcessed++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[LoyaltyExpiration] Failed to expire wallet {WalletId}", walletId);
                    }
                }
            }

            return new LoyaltyExpirationResult(walletsProcessed, lotsExpired);
        }
    }
}
