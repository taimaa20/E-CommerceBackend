using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Interfaces
{
    // Service contracts for the Marketing module. Implementations land per sprint
    // (2 = wallet + audit, 3 = earning, 4 = events/merge).

    /// <summary>
    /// Owns every wallet mutation. The append-only ledger is the source of truth; aggregates and
    /// the legacy <c>Customer.LoyaltyPoints</c> projection are kept in sync inside one transaction.
    /// All mutations are idempotent and guarded by optimistic concurrency.
    /// </summary>
    public interface IWalletService
    {
        Task<CustomerWallet> GetOrCreateWalletAsync(Guid customerId, CancellationToken ct);
        Task<CustomerWallet?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct);

        Task<WalletTransaction> CreditAsync(WalletCreditRequest request, CancellationToken ct);
        Task<WalletTransaction> RedeemAsync(WalletRedeemRequest request, CancellationToken ct);

        /// <summary>
        /// Reverses the points earned for an order by <paramref name="fraction"/> (1.0 = full
        /// cancel/refund). Idempotent per (order, scope, earn-transaction); only un-spent points
        /// are clawed back, so balances never go negative. A no-op when the order earned nothing.
        /// </summary>
        Task ReverseOrderAsync(Guid orderId, decimal fraction, string idempotencyScope, CancellationToken ct);

        /// <summary>
        /// Manual administrative points adjustment. <paramref name="points"/> is signed: positive
        /// credits (opens a FIFO lot), negative debits (FIFO, never below zero). Recorded as an
        /// <c>Adjust</c>/<c>Manual</c> ledger row with a mandatory reason and audit entry.
        /// </summary>
        Task<WalletTransaction> AdjustAsync(Guid customerId, decimal points, string reason, string? reasonAr, CancellationToken ct);

        Task FreezeAsync(Guid walletId, string reason, CancellationToken ct);
        Task UnfreezeAsync(Guid walletId, CancellationToken ct);
        Task CloseAsync(Guid walletId, CancellationToken ct);

        /// <summary>Repairs the wallet aggregate snapshot from the ledger + lots, then re-syncs the projection.</summary>
        Task<CustomerWallet> RecalculateAsync(Guid walletId, CancellationToken ct);

        /// <summary>
        /// Expires every active lot on the wallet whose <c>ExpiresAt</c> is at/before <paramref name="asOfUtc"/>:
        /// writes an <c>Expire</c> ledger row per lot, moves the points from available to expired, and
        /// re-syncs the projection. Idempotent per lot (keyed <c>expire:{lotId}</c>) — safe to re-run.
        /// Returns the number of lots expired.
        /// </summary>
        Task<int> ExpireDueLotsAsync(Guid walletId, DateTime asOfUtc, CancellationToken ct);

        /// <summary>
        /// Approves a single pending earn: flips it Pending→Available, opens its FIFO lot, moves the
        /// points from pending to available, and re-syncs the projection. Idempotent — the lot's unique
        /// <c>SourceTransactionId</c> guards against a second approval. Returns the approved points
        /// (0 if it was already approved or not pending). <paramref name="trigger"/> is audited.
        /// </summary>
        Task<decimal> ApprovePendingAsync(Guid pendingTransactionId, string trigger, CancellationToken ct);
    }

    /// <summary>
    /// Evaluates configured, versioned earning rules for a paid order and credits the resulting
    /// awards to the customer's wallet (idempotent per order+rule). Reversal arrives in Sprint 4.
    /// </summary>
    public interface IEarningEngine
    {
        Task ApplyEarningForOrderAsync(Guid orderId, CancellationToken ct);
    }

    /// <summary>Transfer-row customer/wallet merge with full audit history.</summary>
    public interface ICustomerMergeService
    {
        Task<CustomerMergeHistory> MergeAsync(Guid survivorCustomerId, Guid mergedCustomerId, CancellationToken ct);

        /// <summary>Read-only projection of a prospective merge (no mutation) for the admin confirm step.</summary>
        Task<DTOs.CustomerMergePreviewDto> PreviewAsync(Guid survivorCustomerId, Guid mergedCustomerId, bool isArabic, CancellationToken ct);
    }

    /// <summary>
    /// Enlists an append-only marketing audit entry in the current DbContext transaction
    /// (no SaveChanges), so the audit row commits atomically with the mutation it records.
    /// </summary>
    public interface IMarketingAuditLogger
    {
        void Track(
            Guid tenantId,
            Guid? branchId,
            MarketingAuditAction action,
            string entityType,
            Guid? entityId,
            Guid? customerId,
            string? metadata = null,
            string? beforeJson = null,
            string? afterJson = null);
    }
}
