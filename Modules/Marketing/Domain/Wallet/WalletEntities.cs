using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    /// <summary>
    /// Aggregate snapshot of a customer's loyalty wallet. Fast O(1) reads; the authoritative
    /// detail is the append-only <see cref="WalletTransaction"/> ledger. One row per customer.
    /// </summary>
    public class CustomerWallet : MarketingBaseEntity
    {
        public Guid CustomerId { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal AvailablePoints { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal PendingPoints { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal ExpiredPoints { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal RedeemedPoints { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal LifetimePoints { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal LifetimeSpend { get; set; }

        [Required, MaxLength(3)] public string CurrencyCode { get; set; } = "JOD";

        public int TotalOrders { get; set; }
        public int TotalVisits { get; set; }

        public Guid? CurrentTierId { get; set; }

        public WalletStatus Status { get; set; } = WalletStatus.Active;

        [MaxLength(300)] public string? FrozenReason { get; set; }

        public DateTime? LastRecalculatedAt { get; set; }
    }

    /// <summary>
    /// Append-only loyalty ledger — the source of truth. Never updated except controlled
    /// status flips; never soft-deleted. Reversal is a compensating row, not a delete.
    /// </summary>
    public class WalletTransaction : MarketingBaseEntity
    {
        public Guid WalletId { get; set; }

        public WalletTransactionType Type { get; set; }

        /// <summary>Signed: positive for earn/credit, negative for redeem/expire/transfer-out.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal Points { get; set; }

        /// <summary>Available balance after this row was applied.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal BalanceAfter { get; set; }

        public WalletTransactionStatus Status { get; set; }
        public WalletTransactionSource Source { get; set; }

        public Guid? CustomerId { get; set; }
        public Guid? RuleVersionId { get; set; }
        public Guid? CampaignId { get; set; } // FK added in Phase 4 when Campaign entity ships
        public Guid? ReversesTransactionId { get; set; }
        public Guid? MergeHistoryId { get; set; }

        [MaxLength(3)] public string? CurrencyCode { get; set; }

        [Required, MaxLength(80)] public string IdempotencyKey { get; set; } = string.Empty;

        public DateTime? ExpiresAt { get; set; }

        [MaxLength(300)] public string? Reason { get; set; }
        [MaxLength(300)] public string? ReasonAr { get; set; }

        // ── Immutable order snapshot (set only when Source = Order). Loyalty is computed
        //    once from these frozen values, never recomputed from the live order. ──────────
        public Guid? OrderId { get; set; }
        [MaxLength(20)] public string? CustomerPhoneSnapshot { get; set; }
        public OrderSource? OrderSourceSnapshot { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? SubtotalSnapshot { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? DiscountSnapshot { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? TaxSnapshot { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? TotalAmountSnapshot { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? EarnedPoints { get; set; }
    }

    /// <summary>
    /// FIFO points bucket. Makes earn/redeem/expire/reverse exact and O(open-lots) without
    /// recomputing history. One lot per earn transaction. Append-only.
    /// </summary>
    public class PointsLot : MarketingBaseEntity
    {
        public Guid WalletId { get; set; }
        public Guid SourceTransactionId { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal OriginalPoints { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal RemainingPoints { get; set; }

        public DateTime EarnedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public PointsLotStatus Status { get; set; } = PointsLotStatus.Active;
    }
}
