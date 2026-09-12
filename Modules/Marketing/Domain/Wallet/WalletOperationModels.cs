using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    /// <summary>Immutable order snapshot captured at earn time (set only for order-sourced credits).</summary>
    public sealed record OrderSnapshotData
    {
        public string? CustomerPhone { get; init; }
        public OrderSource? OrderSource { get; init; }
        public decimal? Subtotal { get; init; }
        public decimal? Discount { get; init; }
        public decimal? Tax { get; init; }
        public decimal? TotalAmount { get; init; }
        public decimal? EarnedPoints { get; init; }
    }

    /// <summary>Input to a wallet credit (earn / bonus / transfer-in). Internal service contract, not an API DTO.</summary>
    public sealed record WalletCreditRequest
    {
        public Guid CustomerId { get; init; }
        public decimal Points { get; init; }
        public WalletTransactionType Type { get; init; } = WalletTransactionType.Earn;
        public WalletTransactionSource Source { get; init; } = WalletTransactionSource.Manual;

        /// <summary>When true the credit is recorded as Pending (no lot) until later approval.</summary>
        public bool IsPending { get; init; }

        public required string IdempotencyKey { get; init; }
        public DateTime? ExpiresAt { get; init; }

        public Guid? BranchId { get; init; }
        public Guid? OrderId { get; init; }
        public Guid? RuleVersionId { get; init; }
        public Guid? CampaignId { get; init; }

        public string? CurrencyCode { get; init; }
        public string? Reason { get; init; }
        public string? ReasonAr { get; init; }

        public OrderSnapshotData? OrderSnapshot { get; init; }
    }

    /// <summary>Input to a wallet redemption (debit). Internal service contract, not an API DTO.</summary>
    public sealed record WalletRedeemRequest
    {
        public Guid CustomerId { get; init; }
        public decimal Points { get; init; }
        public WalletTransactionSource Source { get; init; } = WalletTransactionSource.Reward;

        public required string IdempotencyKey { get; init; }

        public Guid? BranchId { get; init; }
        public Guid? OrderId { get; init; }

        public string? Reason { get; init; }
        public string? ReasonAr { get; init; }
    }
}
