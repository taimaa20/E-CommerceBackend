using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    /// <summary>
    /// Audit record of a customer/wallet merge. Balances move via dedicated
    /// MergeTransferOut/MergeTransferIn ledger rows (linked by <c>MergeHistoryId</c>); historical
    /// transactions are never re-owned. A customer can be merged away at most once.
    /// </summary>
    public class CustomerMergeHistory : MarketingBaseEntity
    {
        public Guid SurvivorCustomerId { get; set; }
        public Guid MergedCustomerId { get; set; }

        public DateTime MergedAt { get; set; }
        public Guid? MergedByUserId { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal PointsTransferred { get; set; }
        public int WalletTransactionsRekeyed { get; set; }

        [MaxLength(300)] public string? Reason { get; set; }

        public CustomerMergeStatus Status { get; set; } = CustomerMergeStatus.Completed;
    }
}
