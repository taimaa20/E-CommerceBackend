using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <inheritdoc cref="IWalletService"/>
    public class WalletService : IWalletService
    {
        private const int MaxConcurrencyRetries = 3;

        private readonly PosDbContext _context;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMarketingAuditLogger _audit;
        private readonly ILogger<WalletService> _logger;

        public WalletService(
            PosDbContext context,
            ICurrentUserAccessor currentUser,
            IMarketingAuditLogger audit,
            ILogger<WalletService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Wallet/transaction/lot keys are globally unique GUIDs, so lookups bypass the tenant
        // query filter to stay correct in both request and background (event) scopes.

        public Task<CustomerWallet?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct)
            => _context.CustomerWallets.IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.CustomerId == customerId, ct);

        public async Task<CustomerWallet> GetOrCreateWalletAsync(Guid customerId, CancellationToken ct)
        {
            var existing = await GetByCustomerIdAsync(customerId, ct);
            if (existing != null) return existing;

            var customer = await _context.Customers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == customerId, ct)
                ?? throw new NotFoundException("Customer was not found.");

            var settings = await _context.MarketingSettings.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == customer.TenantId, ct);

            var wallet = new CustomerWallet
            {
                Id = Guid.NewGuid(),
                TenantId = customer.TenantId,
                CustomerId = customerId,
                CurrencyCode = settings?.BaseCurrencyCode ?? "JOD",
                Status = WalletStatus.Active,
                CreatedByUserId = _currentUser.UserIdOrNull
            };
            _context.CustomerWallets.Add(wallet);

            // Lazily mint the public loyalty reference on first wallet creation (deterministic, unique).
            if (string.IsNullOrEmpty(customer.LoyaltyCustomerCode))
                customer.LoyaltyCustomerCode = GenerateLoyaltyCode(customerId);

            _audit.Track(wallet.TenantId, wallet.BranchId, MarketingAuditAction.Created,
                nameof(CustomerWallet), wallet.Id, customerId);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Lost a create race — the unique (TenantId, CustomerId) index rejected us. Reload the winner.
                Detach(wallet);
                var winner = await GetByCustomerIdAsync(customerId, ct);
                if (winner != null) return winner;
                throw;
            }

            _logger.LogInformation("Created loyalty wallet {WalletId} for customer {CustomerId}", wallet.Id, customerId);
            return wallet;
        }

        public async Task<WalletTransaction> CreditAsync(WalletCreditRequest request, CancellationToken ct)
        {
            if (request.Points <= 0) throw new ValidationException("Credit points must be positive.");
            var walletRef = await GetOrCreateWalletAsync(request.CustomerId, ct);

            return await ExecuteWithRetryAsync(async () =>
            {
                var wallet = await LoadWalletAsync(walletRef.Id, ct);

                var existing = await FindByIdempotencyKeyAsync(wallet.Id, request.IdempotencyKey, ct);
                if (existing != null) return existing;

                if (wallet.Status != WalletStatus.Active)
                    throw new ValidationException("Wallet is not active and cannot earn points.");

                var customer = await LoadCustomerAsync(wallet.CustomerId, ct);

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                var balanceAfter = WalletMath.ApplyCredit(wallet, request.Points, request.IsPending);
                wallet.LastRecalculatedAt = DateTime.UtcNow;
                wallet.UpdatedByUserId = _currentUser.UserIdOrNull;

                var transaction = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = wallet.TenantId,
                    BranchId = request.BranchId,
                    WalletId = wallet.Id,
                    Type = request.Type,
                    Points = request.Points,
                    BalanceAfter = balanceAfter,
                    Status = request.IsPending ? WalletTransactionStatus.Pending : WalletTransactionStatus.Available,
                    Source = request.Source,
                    CustomerId = wallet.CustomerId,
                    RuleVersionId = request.RuleVersionId,
                    CampaignId = request.CampaignId,
                    CurrencyCode = request.CurrencyCode ?? wallet.CurrencyCode,
                    IdempotencyKey = request.IdempotencyKey,
                    ExpiresAt = request.ExpiresAt,
                    Reason = request.Reason,
                    ReasonAr = request.ReasonAr,
                    OrderId = request.OrderId,
                    CreatedByUserId = _currentUser.UserIdOrNull
                };
                ApplySnapshot(transaction, request);
                _context.WalletTransactions.Add(transaction);

                // Available credit opens a FIFO lot; pending credit gets its lot on approval (later sprint).
                if (!request.IsPending)
                {
                    _context.PointsLots.Add(new PointsLot
                    {
                        Id = Guid.NewGuid(),
                        TenantId = wallet.TenantId,
                        BranchId = request.BranchId,
                        WalletId = wallet.Id,
                        SourceTransactionId = transaction.Id,
                        OriginalPoints = request.Points,
                        RemainingPoints = request.Points,
                        EarnedAt = DateTime.UtcNow,
                        ExpiresAt = request.ExpiresAt,
                        Status = PointsLotStatus.Active,
                        CreatedByUserId = _currentUser.UserIdOrNull
                    });
                }

                customer.LoyaltyPoints = wallet.AvailablePoints; // legacy projection sync

                _audit.Track(wallet.TenantId, request.BranchId, MarketingAuditAction.PointsEarned,
                    nameof(WalletTransaction), transaction.Id, wallet.CustomerId,
                    $"points={request.Points};pending={request.IsPending};source={request.Source}");

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return transaction;
            }, ct);
        }

        public async Task<WalletTransaction> RedeemAsync(WalletRedeemRequest request, CancellationToken ct)
        {
            if (request.Points <= 0) throw new ValidationException("Redeem points must be positive.");
            var walletRef = await GetByCustomerIdAsync(request.CustomerId, ct)
                ?? throw new NotFoundException("Wallet was not found.");

            return await ExecuteWithRetryAsync(async () =>
            {
                var wallet = await LoadWalletAsync(walletRef.Id, ct);

                var existing = await FindByIdempotencyKeyAsync(wallet.Id, request.IdempotencyKey, ct);
                if (existing != null) return existing;

                if (wallet.Status != WalletStatus.Active)
                    throw new ValidationException("Wallet is not active and cannot redeem points.");
                if (wallet.AvailablePoints < request.Points)
                    throw new ValidationException("Insufficient available points.");

                var lots = await _context.PointsLots.IgnoreQueryFilters()
                    .Where(l => l.WalletId == wallet.Id
                                && l.Status == PointsLotStatus.Active
                                && l.RemainingPoints > 0)
                    .OrderBy(l => l.EarnedAt)
                    .ToListAsync(ct);

                var deductions = WalletMath.AllocateFifo(
                    lots.Select(l => new WalletMath.LotSnapshot(l.Id, l.RemainingPoints, l.EarnedAt)).ToList(),
                    request.Points);

                var customer = await LoadCustomerAsync(wallet.CustomerId, ct);

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                foreach (var deduction in deductions)
                {
                    var lot = lots.First(l => l.Id == deduction.LotId);
                    lot.RemainingPoints = WalletMath.Round(lot.RemainingPoints - deduction.Deducted);
                    if (lot.RemainingPoints <= 0) lot.Status = PointsLotStatus.Depleted;
                }

                var balanceAfter = WalletMath.ApplyRedeem(wallet, request.Points);
                wallet.LastRecalculatedAt = DateTime.UtcNow;
                wallet.UpdatedByUserId = _currentUser.UserIdOrNull;

                var transaction = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = wallet.TenantId,
                    BranchId = request.BranchId,
                    WalletId = wallet.Id,
                    Type = WalletTransactionType.Redeem,
                    Points = -request.Points,
                    BalanceAfter = balanceAfter,
                    Status = WalletTransactionStatus.Available,
                    Source = request.Source,
                    CustomerId = wallet.CustomerId,
                    OrderId = request.OrderId,
                    CurrencyCode = wallet.CurrencyCode,
                    IdempotencyKey = request.IdempotencyKey,
                    Reason = request.Reason,
                    ReasonAr = request.ReasonAr,
                    CreatedByUserId = _currentUser.UserIdOrNull
                };
                _context.WalletTransactions.Add(transaction);

                customer.LoyaltyPoints = wallet.AvailablePoints; // legacy projection sync

                _audit.Track(wallet.TenantId, request.BranchId, MarketingAuditAction.PointsRedeemed,
                    nameof(WalletTransaction), transaction.Id, wallet.CustomerId,
                    $"points={request.Points};source={request.Source}");

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return transaction;
            }, ct);
        }

        public async Task<WalletTransaction> AdjustAsync(
            Guid customerId, decimal points, string reason, string? reasonAr, CancellationToken ct)
        {
            if (points == 0) throw new ValidationException("Adjustment amount must be non-zero.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ValidationException("A reason is required for manual adjustments.");

            var walletRef = await GetOrCreateWalletAsync(customerId, ct);
            var idempotencyKey = $"manual-adjust:{Guid.NewGuid():N}";
            var magnitude = Math.Abs(points);
            var isCredit = points > 0;

            return await ExecuteWithRetryAsync(async () =>
            {
                var wallet = await LoadWalletAsync(walletRef.Id, ct);
                if (wallet.Status != WalletStatus.Active)
                    throw new ValidationException("Wallet is not active and cannot be adjusted.");
                if (!isCredit && wallet.AvailablePoints < magnitude)
                    throw new ValidationException("Insufficient available points for this deduction.");

                var customer = await LoadCustomerAsync(wallet.CustomerId, ct);
                var transactionId = Guid.NewGuid();

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                decimal balanceAfter;
                if (isCredit)
                {
                    balanceAfter = WalletMath.ApplyCredit(wallet, magnitude, isPending: false);
                    _context.PointsLots.Add(new PointsLot
                    {
                        Id = Guid.NewGuid(),
                        TenantId = wallet.TenantId,
                        BranchId = wallet.BranchId,
                        WalletId = wallet.Id,
                        SourceTransactionId = transactionId,
                        OriginalPoints = magnitude,
                        RemainingPoints = magnitude,
                        EarnedAt = DateTime.UtcNow,
                        Status = PointsLotStatus.Active,
                        CreatedByUserId = _currentUser.UserIdOrNull
                    });
                }
                else
                {
                    var lots = await _context.PointsLots.IgnoreQueryFilters()
                        .Where(l => l.WalletId == wallet.Id
                                    && l.Status == PointsLotStatus.Active
                                    && l.RemainingPoints > 0)
                        .OrderBy(l => l.EarnedAt)
                        .ToListAsync(ct);

                    var deductions = WalletMath.AllocateFifo(
                        lots.Select(l => new WalletMath.LotSnapshot(l.Id, l.RemainingPoints, l.EarnedAt)).ToList(),
                        magnitude);

                    foreach (var deduction in deductions)
                    {
                        var lot = lots.First(l => l.Id == deduction.LotId);
                        lot.RemainingPoints = WalletMath.Round(lot.RemainingPoints - deduction.Deducted);
                        if (lot.RemainingPoints <= 0) lot.Status = PointsLotStatus.Depleted;
                    }

                    balanceAfter = WalletMath.ApplyRedeem(wallet, magnitude);
                }

                wallet.LastRecalculatedAt = DateTime.UtcNow;
                wallet.UpdatedByUserId = _currentUser.UserIdOrNull;

                var transaction = new WalletTransaction
                {
                    Id = transactionId,
                    TenantId = wallet.TenantId,
                    BranchId = wallet.BranchId,
                    WalletId = wallet.Id,
                    Type = WalletTransactionType.Adjust,
                    Points = isCredit ? magnitude : -magnitude,
                    BalanceAfter = balanceAfter,
                    Status = WalletTransactionStatus.Available,
                    Source = WalletTransactionSource.Manual,
                    CustomerId = wallet.CustomerId,
                    CurrencyCode = wallet.CurrencyCode,
                    IdempotencyKey = idempotencyKey,
                    Reason = reason,
                    ReasonAr = reasonAr,
                    CreatedByUserId = _currentUser.UserIdOrNull
                };
                _context.WalletTransactions.Add(transaction);

                customer.LoyaltyPoints = wallet.AvailablePoints; // legacy projection sync

                _audit.Track(wallet.TenantId, wallet.BranchId,
                    isCredit ? MarketingAuditAction.PointsEarned : MarketingAuditAction.PointsRedeemed,
                    nameof(WalletTransaction), transaction.Id, wallet.CustomerId,
                    $"manual-adjust;points={transaction.Points};before={balanceAfter - transaction.Points};after={balanceAfter};reason={reason}");

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return transaction;
            }, ct);
        }

        public async Task ReverseOrderAsync(Guid orderId, decimal fraction, string idempotencyScope, CancellationToken ct)
        {
            if (fraction <= 0) return;
            if (fraction > 1m) fraction = 1m;

            // Earn rows for this order not already fully reversed. Read ids only; each is reversed
            // in its own transaction so a partial failure replays cleanly via the idempotency key.
            var earnIds = await _context.WalletTransactions.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.OrderId == orderId
                            && (t.Type == WalletTransactionType.Earn || t.Type == WalletTransactionType.EarnPendingApproved)
                            && t.Status != WalletTransactionStatus.Reversed)
                .Select(t => t.Id)
                .ToListAsync(ct);

            foreach (var earnId in earnIds)
                await ReverseSingleEarnAsync(earnId, orderId, fraction, idempotencyScope, ct);
        }

        private Task ReverseSingleEarnAsync(Guid earnId, Guid orderId, decimal fraction, string scope, CancellationToken ct)
        {
            var idempotencyKey = $"reverse:{orderId}:{scope}:{earnId}";

            return ExecuteWithRetryAsync(async () =>
            {
                var earn = await _context.WalletTransactions.IgnoreQueryFilters().FirstAsync(t => t.Id == earnId, ct);
                if (earn.Status == WalletTransactionStatus.Reversed) return true;

                var duplicate = await _context.WalletTransactions.IgnoreQueryFilters()
                    .AnyAsync(t => t.WalletId == earn.WalletId && t.IdempotencyKey == idempotencyKey, ct);
                if (duplicate) return true;

                var wallet = await LoadWalletAsync(earn.WalletId, ct);
                var customer = await LoadCustomerAsync(wallet.CustomerId, ct);

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                var requested = WalletMath.Round(earn.Points * fraction); // earn.Points is the gross positive amount
                decimal actual;

                if (earn.Status == WalletTransactionStatus.Pending)
                {
                    // Pending points were never available — voiding simply removes them from pending.
                    actual = Math.Min(requested, wallet.PendingPoints);
                    wallet.PendingPoints = WalletMath.Round(wallet.PendingPoints - actual);
                }
                else
                {
                    // Claw back only the un-spent remainder of this earn's lot (never goes negative).
                    var lot = await _context.PointsLots.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(l => l.SourceTransactionId == earn.Id, ct);
                    var lotRemaining = lot?.RemainingPoints ?? 0m;
                    actual = Math.Min(requested, lotRemaining);

                    if (lot is not null && actual > 0)
                    {
                        lot.RemainingPoints = WalletMath.Round(lot.RemainingPoints - actual);
                        if (lot.RemainingPoints <= 0) lot.Status = PointsLotStatus.Depleted;
                    }

                    wallet.AvailablePoints = WalletMath.Round(wallet.AvailablePoints - actual);
                    wallet.LifetimePoints = WalletMath.Round(wallet.LifetimePoints - actual);

                    if (actual < requested)
                        _logger.LogWarning(
                            "Reversal shortfall on order {OrderId}: {Shortfall} pts already spent and cannot be clawed back.",
                            orderId, requested - actual);
                }

                if (fraction >= 1m)
                    earn.Status = WalletTransactionStatus.Reversed;

                wallet.LastRecalculatedAt = DateTime.UtcNow;
                wallet.UpdatedByUserId = _currentUser.UserIdOrNull;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = wallet.TenantId,
                    BranchId = earn.BranchId,
                    WalletId = wallet.Id,
                    Type = WalletTransactionType.ReverseEarn,
                    Points = -actual,
                    BalanceAfter = wallet.AvailablePoints,
                    Status = WalletTransactionStatus.Available,
                    Source = earn.Source,
                    CustomerId = wallet.CustomerId,
                    OrderId = orderId,
                    ReversesTransactionId = earn.Id,
                    CurrencyCode = wallet.CurrencyCode,
                    IdempotencyKey = idempotencyKey,
                    Reason = $"Reversal ({scope})",
                    CreatedByUserId = _currentUser.UserIdOrNull
                });

                customer.LoyaltyPoints = wallet.AvailablePoints; // legacy projection sync

                _audit.Track(wallet.TenantId, earn.BranchId, MarketingAuditAction.PointsReversed,
                    nameof(WalletTransaction), earn.Id, wallet.CustomerId,
                    $"reversed={actual};requested={requested};scope={scope}");

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return true;
            }, ct);
        }

        public async Task<int> ExpireDueLotsAsync(Guid walletId, DateTime asOfUtc, CancellationToken ct)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var wallet = await LoadWalletAsync(walletId, ct);

                // Expiration is a time-based system cleanup — it runs regardless of freeze/close
                // so balances never carry points past their expiry, and the job always makes progress.
                var dueLots = await _context.PointsLots.IgnoreQueryFilters()
                    .Where(l => l.WalletId == wallet.Id
                                && l.Status == PointsLotStatus.Active
                                && l.RemainingPoints > 0
                                && l.ExpiresAt != null
                                && l.ExpiresAt <= asOfUtc)
                    .OrderBy(l => l.ExpiresAt)
                    .ToListAsync(ct);
                if (dueLots.Count == 0) return 0;

                var customer = await LoadCustomerAsync(wallet.CustomerId, ct);

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                var expired = 0;
                foreach (var lot in dueLots)
                {
                    var idempotencyKey = $"expire:{lot.Id}";
                    var duplicate = await _context.WalletTransactions.IgnoreQueryFilters()
                        .AnyAsync(t => t.WalletId == wallet.Id && t.IdempotencyKey == idempotencyKey, ct);

                    var amount = lot.RemainingPoints;
                    lot.RemainingPoints = 0m;
                    lot.Status = PointsLotStatus.Expired;

                    if (duplicate) continue; // ledger row already written on a previous run

                    wallet.AvailablePoints = WalletMath.Round(wallet.AvailablePoints - amount);
                    wallet.ExpiredPoints = WalletMath.Round(wallet.ExpiredPoints + amount);

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        Id = Guid.NewGuid(),
                        TenantId = wallet.TenantId,
                        BranchId = lot.BranchId,
                        WalletId = wallet.Id,
                        Type = WalletTransactionType.Expire,
                        Points = -amount,
                        BalanceAfter = wallet.AvailablePoints,
                        Status = WalletTransactionStatus.Expired,
                        Source = WalletTransactionSource.Manual,
                        CustomerId = wallet.CustomerId,
                        CurrencyCode = wallet.CurrencyCode,
                        IdempotencyKey = idempotencyKey,
                        ExpiresAt = lot.ExpiresAt,
                        Reason = "Points expired",
                        CreatedByUserId = _currentUser.UserIdOrNull
                    });

                    _audit.Track(wallet.TenantId, lot.BranchId, MarketingAuditAction.PointsExpired,
                        nameof(PointsLot), lot.Id, wallet.CustomerId,
                        $"expired={amount};lot={lot.Id};expiresAt={lot.ExpiresAt:o}");

                    expired++;
                }

                if (wallet.AvailablePoints < 0) wallet.AvailablePoints = 0m;
                wallet.LastRecalculatedAt = DateTime.UtcNow;
                wallet.UpdatedByUserId = _currentUser.UserIdOrNull;
                customer.LoyaltyPoints = wallet.AvailablePoints;

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return expired;
            }, ct);
        }

        public async Task<decimal> ApprovePendingAsync(Guid pendingTransactionId, string trigger, CancellationToken ct)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var pending = await _context.WalletTransactions.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Id == pendingTransactionId, ct);
                if (pending is null) return 0m;

                // Idempotency: the lot's unique SourceTransactionId means a prior approval already
                // opened it. If a lot exists, just ensure the row is marked Available and stop.
                var lotExists = await _context.PointsLots.IgnoreQueryFilters()
                    .AnyAsync(l => l.SourceTransactionId == pending.Id, ct);
                if (lotExists || pending.Status != WalletTransactionStatus.Pending)
                {
                    if (pending.Status == WalletTransactionStatus.Pending && lotExists)
                    {
                        pending.Status = WalletTransactionStatus.Available;
                        await _context.SaveChangesAsync(ct);
                    }
                    return 0m;
                }

                var points = pending.Points; // pending earns are stored positive
                if (points <= 0)
                {
                    pending.Status = WalletTransactionStatus.Available;
                    await _context.SaveChangesAsync(ct);
                    return 0m;
                }

                var wallet = await LoadWalletAsync(pending.WalletId, ct);
                var customer = await LoadCustomerAsync(wallet.CustomerId, ct);

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                // The pending earn row transitions to Available — that status change IS the approval
                // record (no second points row, so the balance can never be double-counted).
                pending.Status = WalletTransactionStatus.Available;

                wallet.PendingPoints = WalletMath.Round(Math.Max(0m, wallet.PendingPoints - points));
                wallet.AvailablePoints = WalletMath.Round(wallet.AvailablePoints + points);
                wallet.LastRecalculatedAt = DateTime.UtcNow;
                wallet.UpdatedByUserId = _currentUser.UserIdOrNull;

                _context.PointsLots.Add(new PointsLot
                {
                    Id = Guid.NewGuid(),
                    TenantId = wallet.TenantId,
                    BranchId = pending.BranchId,
                    WalletId = wallet.Id,
                    SourceTransactionId = pending.Id,
                    OriginalPoints = points,
                    RemainingPoints = points,
                    EarnedAt = DateTime.UtcNow,
                    ExpiresAt = pending.ExpiresAt,
                    Status = PointsLotStatus.Active,
                    CreatedByUserId = _currentUser.UserIdOrNull
                });

                customer.LoyaltyPoints = wallet.AvailablePoints; // legacy projection sync

                _audit.Track(wallet.TenantId, pending.BranchId, MarketingAuditAction.PointsEarned,
                    nameof(WalletTransaction), pending.Id, wallet.CustomerId,
                    $"pending-approved;points={points};trigger={trigger}");

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return points;
            }, ct);
        }

        public Task FreezeAsync(Guid walletId, string reason, CancellationToken ct)
            => SetStatusAsync(walletId, WalletStatus.Frozen, reason, MarketingAuditAction.Frozen, ct);

        public Task UnfreezeAsync(Guid walletId, CancellationToken ct)
            => SetStatusAsync(walletId, WalletStatus.Active, null, MarketingAuditAction.Unfrozen, ct);

        public Task CloseAsync(Guid walletId, CancellationToken ct)
            => SetStatusAsync(walletId, WalletStatus.Closed, null, MarketingAuditAction.Closed, ct);

        public async Task<CustomerWallet> RecalculateAsync(Guid walletId, CancellationToken ct)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var wallet = await LoadWalletAsync(walletId, ct);

                var available = await _context.PointsLots.IgnoreQueryFilters()
                    .Where(l => l.WalletId == wallet.Id && l.Status == PointsLotStatus.Active)
                    .SumAsync(l => (decimal?)l.RemainingPoints, ct) ?? 0m;

                var txns = await _context.WalletTransactions.IgnoreQueryFilters()
                    .Where(t => t.WalletId == wallet.Id)
                    .Select(t => new { t.Type, t.Status, t.Points })
                    .ToListAsync(ct);

                var pending = txns.Where(t => t.Status == WalletTransactionStatus.Pending).Sum(t => t.Points);
                var redeemed = txns.Where(t => t.Type == WalletTransactionType.Redeem).Sum(t => -t.Points);
                var expired = txns.Where(t => t.Type == WalletTransactionType.Expire).Sum(t => -t.Points);

                wallet.AvailablePoints = WalletMath.Round(available);
                wallet.PendingPoints = WalletMath.Round(pending);
                wallet.RedeemedPoints = WalletMath.Round(redeemed);
                wallet.ExpiredPoints = WalletMath.Round(expired);
                wallet.LifetimePoints = WalletMath.Round(available + redeemed + expired);
                wallet.LastRecalculatedAt = DateTime.UtcNow;
                wallet.UpdatedByUserId = _currentUser.UserIdOrNull;

                var customer = await LoadCustomerAsync(wallet.CustomerId, ct);
                customer.LoyaltyPoints = wallet.AvailablePoints;

                await _context.SaveChangesAsync(ct);
                return wallet;
            }, ct);
        }

        private async Task SetStatusAsync(
            Guid walletId, WalletStatus status, string? reason, MarketingAuditAction action, CancellationToken ct)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                var wallet = await LoadWalletAsync(walletId, ct);
                wallet.Status = status;
                wallet.FrozenReason = status == WalletStatus.Frozen ? reason : null;
                wallet.UpdatedByUserId = _currentUser.UserIdOrNull;

                _audit.Track(wallet.TenantId, wallet.BranchId, action,
                    nameof(CustomerWallet), wallet.Id, wallet.CustomerId, reason);

                await _context.SaveChangesAsync(ct);
                return true;
            }, ct);
        }

        private Task<CustomerWallet> LoadWalletAsync(Guid walletId, CancellationToken ct)
            => _context.CustomerWallets.IgnoreQueryFilters().FirstAsync(w => w.Id == walletId, ct);

        private Task<Customer> LoadCustomerAsync(Guid customerId, CancellationToken ct)
            => _context.Customers.IgnoreQueryFilters().FirstAsync(c => c.Id == customerId, ct);

        private Task<WalletTransaction?> FindByIdempotencyKeyAsync(Guid walletId, string key, CancellationToken ct)
            => _context.WalletTransactions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.WalletId == walletId && t.IdempotencyKey == key, ct);

        private static void ApplySnapshot(WalletTransaction transaction, WalletCreditRequest request)
        {
            if (request.OrderSnapshot is not { } s) return;

            transaction.CustomerPhoneSnapshot = s.CustomerPhone;
            transaction.OrderSourceSnapshot = s.OrderSource;
            transaction.SubtotalSnapshot = s.Subtotal;
            transaction.DiscountSnapshot = s.Discount;
            transaction.TaxSnapshot = s.Tax;
            transaction.TotalAmountSnapshot = s.TotalAmount;
            transaction.EarnedPoints = s.EarnedPoints ?? request.Points;
        }

        private static string GenerateLoyaltyCode(Guid customerId)
            => "LC" + customerId.ToString("N")[..10].ToUpperInvariant();

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, CancellationToken ct)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
                {
                    _logger.LogWarning("Wallet concurrency conflict (attempt {Attempt}); retrying.", attempt);
                    DetachMarketingEntries();
                }
            }
        }

        private void DetachMarketingEntries()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList())
            {
                if (entry.Entity is MarketingBaseEntity or Customer)
                    entry.State = EntityState.Detached;
            }
        }

        private void Detach(object entity) => _context.Entry(entity).State = EntityState.Detached;
    }
}
