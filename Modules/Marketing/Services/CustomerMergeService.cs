using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <inheritdoc cref="ICustomerMergeService"/>
    public class CustomerMergeService : ICustomerMergeService
    {
        private readonly PosDbContext _context;
        private readonly IWalletService _wallet;
        private readonly IMarketingAuditLogger _audit;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ITenantResolver _tenant;

        public CustomerMergeService(
            PosDbContext context,
            IWalletService wallet,
            IMarketingAuditLogger audit,
            ICurrentUserAccessor currentUser,
            ITenantResolver tenant)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
        }

        // Merge is a tenant-facing admin operation; both customers must belong to the caller's tenant.
        private async Task EnsureBothInTenantAsync(Guid survivorCustomerId, Guid mergedCustomerId, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var count = await _context.Customers.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(c => (c.Id == survivorCustomerId || c.Id == mergedCustomerId) && c.TenantId == tenantId, ct);
            if (count < 2) throw new NotFoundException("One or both customers were not found.");
        }

        public async Task<DTOs.CustomerMergePreviewDto> PreviewAsync(
            Guid survivorCustomerId, Guid mergedCustomerId, bool isArabic, CancellationToken ct)
        {
            var preview = new DTOs.CustomerMergePreviewDto();

            if (survivorCustomerId == mergedCustomerId)
            {
                preview.Warnings.Add("Cannot merge a customer into itself.");
                preview.CanMerge = false;
                return preview;
            }

            await EnsureBothInTenantAsync(survivorCustomerId, mergedCustomerId, ct);

            var survivor = await BuildSideAsync(survivorCustomerId, isArabic, ct);
            var merged = await BuildSideAsync(mergedCustomerId, isArabic, ct);
            preview.Survivor = survivor;
            preview.Merged = merged;

            preview.AlreadyMerged = await _context.CustomerMergeHistories.IgnoreQueryFilters()
                .AnyAsync(h => h.MergedCustomerId == mergedCustomerId, ct);

            preview.ProjectedAvailablePoints = WalletMath.Round(survivor.AvailablePoints + merged.AvailablePoints);
            preview.ProjectedLifetimePoints = WalletMath.Round(survivor.LifetimePoints + merged.LifetimePoints);
            preview.ProjectedTransactionCount = survivor.TransactionCount + merged.TransactionCount;
            preview.ProjectedOrderCount = survivor.OrderCount + merged.OrderCount;

            if (preview.AlreadyMerged)
                preview.Warnings.Add("This customer has already been merged.");

            preview.CanMerge = !preview.AlreadyMerged;
            return preview;
        }

        private async Task<DTOs.MergeCustomerSideDto> BuildSideAsync(Guid customerId, bool isArabic, CancellationToken ct)
        {
            var customer = await _context.Customers.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId, ct);
            var wallet = await _context.CustomerWallets.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(w => w.CustomerId == customerId, ct);
            var txCount = await _context.WalletTransactions.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(t => t.CustomerId == customerId, ct);

            return new DTOs.MergeCustomerSideDto
            {
                CustomerId = customerId,
                Name = customer is null ? "—" : (isArabic ? customer.NameAr : customer.Name) ?? customer.Name,
                PhoneNumber = customer?.PhoneNumber,
                AvailablePoints = wallet?.AvailablePoints ?? customer?.LoyaltyPoints ?? 0m,
                LifetimePoints = wallet?.LifetimePoints ?? 0m,
                Tier = (customer?.Tier ?? CustomerTier.Standard).ToString(),
                TransactionCount = txCount,
                OrderCount = wallet?.TotalOrders ?? 0,
                HasWallet = wallet is not null
            };
        }

        public async Task<CustomerMergeHistory> MergeAsync(Guid survivorCustomerId, Guid mergedCustomerId, CancellationToken ct)
        {
            if (survivorCustomerId == mergedCustomerId)
                throw new ValidationException("Cannot merge a customer into itself.");

            await EnsureBothInTenantAsync(survivorCustomerId, mergedCustomerId, ct);

            // Idempotency: a customer is merged away at most once (unique MergedCustomerId).
            var existing = await _context.CustomerMergeHistories.IgnoreQueryFilters()
                .FirstOrDefaultAsync(h => h.MergedCustomerId == mergedCustomerId, ct);
            if (existing is not null)
                return existing;

            var mergedWallet = await _wallet.GetByCustomerIdAsync(mergedCustomerId, ct);
            var survivorWallet = await _wallet.GetOrCreateWalletAsync(survivorCustomerId, ct);

            if (mergedWallet is not null &&
                !string.Equals(mergedWallet.CurrencyCode, survivorWallet.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Cannot merge wallets with different currencies.");

            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var survivor = await _context.CustomerWallets.IgnoreQueryFilters().FirstAsync(w => w.Id == survivorWallet.Id, ct);
            var merged = mergedWallet is null
                ? null
                : await _context.CustomerWallets.IgnoreQueryFilters().FirstAsync(w => w.Id == mergedWallet.Id, ct);

            var transferable = merged?.AvailablePoints ?? 0m;

            var history = new CustomerMergeHistory
            {
                Id = Guid.NewGuid(),
                TenantId = survivor.TenantId,
                SurvivorCustomerId = survivorCustomerId,
                MergedCustomerId = mergedCustomerId,
                MergedAt = DateTime.UtcNow,
                MergedByUserId = _currentUser.UserIdOrNull,
                PointsTransferred = transferable,
                Status = CustomerMergeStatus.Completed
            };
            _context.CustomerMergeHistories.Add(history);

            if (merged is not null)
            {
                await TransferBalanceAsync(survivor, merged, transferable, history.Id, ct);
                AbsorbAggregates(survivor, merged, transferable);
                await SyncProjectionsAsync(survivorCustomerId, mergedCustomerId, survivor.AvailablePoints, ct);
            }

            _audit.Track(survivor.TenantId, null, MarketingAuditAction.Merged,
                nameof(CustomerMergeHistory), history.Id, survivorCustomerId,
                $"merged={mergedCustomerId};points={transferable}");

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return history;
        }

        private async Task TransferBalanceAsync(
            CustomerWallet survivor, CustomerWallet merged, decimal transferable, Guid historyId, CancellationToken ct)
        {
            var activeLots = await _context.PointsLots.IgnoreQueryFilters()
                .Where(l => l.WalletId == merged.Id && l.Status == PointsLotStatus.Active && l.RemainingPoints > 0)
                .ToListAsync(ct);

            var earliestExpiry = WalletMath.EarliestExpiry(activeLots.Select(l => l.ExpiresAt));

            // Deplete the merged wallet's lots — their points move to the survivor.
            foreach (var lot in activeLots)
            {
                lot.RemainingPoints = 0m;
                lot.Status = PointsLotStatus.Depleted;
            }

            if (transferable <= 0)
                return;

            // Transfer OUT of the merged wallet (its ledger stays immutable; only this row is added).
            _context.WalletTransactions.Add(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = merged.TenantId,
                WalletId = merged.Id,
                Type = WalletTransactionType.MergeTransferOut,
                Points = -transferable,
                BalanceAfter = 0m,
                Status = WalletTransactionStatus.Available,
                Source = WalletTransactionSource.Merge,
                CustomerId = merged.CustomerId,
                MergeHistoryId = historyId,
                CurrencyCode = merged.CurrencyCode,
                IdempotencyKey = $"merge:{historyId}:out",
                Reason = "Merge transfer out",
                CreatedByUserId = _currentUser.UserIdOrNull
            });

            // Transfer IN to the survivor wallet, opening a fresh lot (earliest source expiry preserved).
            var inTransactionId = Guid.NewGuid();
            var newAvailable = WalletMath.Round(survivor.AvailablePoints + transferable);

            _context.WalletTransactions.Add(new WalletTransaction
            {
                Id = inTransactionId,
                TenantId = survivor.TenantId,
                WalletId = survivor.Id,
                Type = WalletTransactionType.MergeTransferIn,
                Points = transferable,
                BalanceAfter = newAvailable,
                Status = WalletTransactionStatus.Available,
                Source = WalletTransactionSource.Merge,
                CustomerId = survivor.CustomerId,
                MergeHistoryId = historyId,
                CurrencyCode = survivor.CurrencyCode,
                IdempotencyKey = $"merge:{historyId}:in",
                ExpiresAt = earliestExpiry,
                Reason = "Merge transfer in",
                CreatedByUserId = _currentUser.UserIdOrNull
            });

            _context.PointsLots.Add(new PointsLot
            {
                Id = Guid.NewGuid(),
                TenantId = survivor.TenantId,
                WalletId = survivor.Id,
                SourceTransactionId = inTransactionId,
                OriginalPoints = transferable,
                RemainingPoints = transferable,
                EarnedAt = DateTime.UtcNow,
                ExpiresAt = earliestExpiry,
                Status = PointsLotStatus.Active,
                CreatedByUserId = _currentUser.UserIdOrNull
            });
        }

        private void AbsorbAggregates(CustomerWallet survivor, CustomerWallet merged, decimal transferable)
        {
            survivor.AvailablePoints = WalletMath.Round(survivor.AvailablePoints + transferable);
            survivor.PendingPoints = WalletMath.Round(survivor.PendingPoints + merged.PendingPoints);
            survivor.RedeemedPoints = WalletMath.Round(survivor.RedeemedPoints + merged.RedeemedPoints);
            survivor.ExpiredPoints = WalletMath.Round(survivor.ExpiredPoints + merged.ExpiredPoints);
            survivor.LifetimePoints = WalletMath.Round(survivor.LifetimePoints + merged.LifetimePoints);
            survivor.LifetimeSpend = WalletMath.Round(survivor.LifetimeSpend + merged.LifetimeSpend);
            survivor.TotalOrders += merged.TotalOrders;
            survivor.TotalVisits += merged.TotalVisits;
            survivor.LastRecalculatedAt = DateTime.UtcNow;
            survivor.UpdatedByUserId = _currentUser.UserIdOrNull;

            // The merged wallet keeps its historical aggregates for audit but is emptied & closed.
            merged.AvailablePoints = 0m;
            merged.PendingPoints = 0m;
            merged.Status = WalletStatus.Closed;
            merged.LastRecalculatedAt = DateTime.UtcNow;
            merged.UpdatedByUserId = _currentUser.UserIdOrNull;
        }

        private async Task SyncProjectionsAsync(Guid survivorCustomerId, Guid mergedCustomerId, decimal survivorAvailable, CancellationToken ct)
        {
            var survivorCustomer = await _context.Customers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == survivorCustomerId, ct);
            if (survivorCustomer is not null) survivorCustomer.LoyaltyPoints = survivorAvailable;

            var mergedCustomer = await _context.Customers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == mergedCustomerId, ct);
            if (mergedCustomer is not null) mergedCustomer.LoyaltyPoints = 0m;
        }
    }
}
