using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;

namespace RestaurantPos.Api.Modules.Dashboard.DTOs.Financial
{
    public sealed record CommissionAnalysisDto : DashboardSnapshotEnvelope
    {
        public CommissionSummaryDto Summary { get; init; } = new();
        public IReadOnlyList<PartnerCommissionDto> Partners { get; init; } = Array.Empty<PartnerCommissionDto>();
        public IReadOnlyList<PaymentProviderCommissionDto> PaymentProviders { get; init; } = Array.Empty<PaymentProviderCommissionDto>();
        public CommissionCoverageDto Coverage { get; init; } = new();
    }

    /// <summary>
    /// Analysis completeness indicator. Lets the owner see how much of the period
    /// was answered from frozen snapshots vs reconstructed from historical config,
    /// and how many legacy transactions were excluded for lack of a resolvable
    /// commission provider. Informational only — never affects financial totals.
    /// </summary>
    public sealed record CommissionCoverageDto
    {
        public int Analyzed { get; init; }
        public int FromSnapshot { get; init; }
        public int Reconstructed { get; init; }
        public int Excluded { get; init; }
    }

    public sealed record CommissionSummaryDto
    {
        public decimal TotalPartnerCommission { get; init; }
        public decimal TotalPaymentFees { get; init; }
        public decimal TotalRestaurantCostSharing { get; init; }
        public decimal TotalProviderCostSharing { get; init; }
        public int TotalCommissionTransactions { get; init; }
        public decimal AverageCommissionPercentage { get; init; }
        public decimal AverageFeePerOrder { get; init; }
    }

    public sealed record PartnerCommissionDto
    {
        public string Key { get; init; } = string.Empty;
        public string Partner { get; init; } = string.Empty;
        public string? PartnerAr { get; init; }
        public int NumberOfOrders { get; init; }
        public decimal GrossSales { get; init; }
        public decimal CommissionPercentage { get; init; }
        public decimal CommissionAmount { get; init; }
        public decimal RestaurantShare { get; init; }
        public decimal PartnerShare { get; init; }
        public decimal RestaurantFinalRevenue { get; init; }
        public decimal AverageCommissionPerOrder { get; init; }
        /// <summary>"Snapshot", "Reconstructed", or "Mixed" — basis of this group's figures.</summary>
        public string Basis { get; init; } = CommissionBasis.Snapshot;
        public IReadOnlyList<CommissionOrderDto> Orders { get; init; } = Array.Empty<CommissionOrderDto>();
    }

    public sealed record PaymentProviderCommissionDto
    {
        public string Key { get; init; } = string.Empty;
        public string PaymentMethod { get; init; } = string.Empty;
        public string? PaymentMethodAr { get; init; }
        public int Transactions { get; init; }
        public decimal GrossAmount { get; init; }
        public decimal FeePercentage { get; init; }
        public decimal FeeAmount { get; init; }
        public decimal RestaurantShare { get; init; }
        public decimal ProviderShare { get; init; }
        public decimal NetAmountReceived { get; init; }
        /// <summary>"Snapshot", "Reconstructed", or "Mixed" — basis of this group's figures.</summary>
        public string Basis { get; init; } = CommissionBasis.Snapshot;
        public IReadOnlyList<CommissionOrderDto> Orders { get; init; } = Array.Empty<CommissionOrderDto>();
    }

    public sealed record CommissionOrderDto
    {
        public Guid OrderId { get; init; }
        public string OrderNumber { get; init; } = string.Empty;
        public DateTime DateUtc { get; init; }
        public string Customer { get; init; } = string.Empty;
        public decimal OrderTotal { get; init; }
        public decimal CommissionPercentage { get; init; }
        public decimal CommissionAmount { get; init; }
        public decimal RestaurantShare { get; init; }
        public decimal ProviderShare { get; init; }
        public decimal NetAmount { get; init; }
        /// <summary>"Snapshot" (frozen at completion) or "Reconstructed" (from historical config).</summary>
        public string Basis { get; init; } = CommissionBasis.Snapshot;
    }

    /// <summary>Canonical basis labels shared by service and DTOs.</summary>
    public static class CommissionBasis
    {
        public const string Snapshot = "Snapshot";
        public const string Reconstructed = "Reconstructed";
        public const string Mixed = "Mixed";
    }
}
