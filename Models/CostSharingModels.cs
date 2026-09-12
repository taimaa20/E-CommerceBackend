using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    public enum CostSharingMode
    {
        RestaurantBearsAll = 0,
        CounterpartyBearsAll = 1,
        Shared = 2
    }

    public enum CostSharingScope
    {
        AllOrders = 0,
        PerPartnerOrCard = 1,
        PerOrder = 2
    }

    public enum CostSharingTargetType
    {
        DeliveryPartner = 0,
        PaymentMethod = 1
    }

    public enum CostProviderType
    {
        DeliveryPartner = 0,
        PaymentCard = 1,
        CreditCard = 2,
        DigitalWallet = 3,
        OnlinePaymentProvider = 4,
        BankPaymentProvider = 5,
        QrPaymentProvider = 6,
        PaymentMethod = 7,
        ServiceProvider = 8,
        Other = 9
    }

    public enum CostFeeType
    {
        Percentage = 0,
        FixedAmount = 1,
        PercentagePlusFixed = 2,
        Tiered = 3
    }

    public enum CostRuleScope
    {
        GlobalDefault = 0,
        PartnerDefault = 1,
        PaymentMethodDefault = 2,
        CardTypeDefault = 3,
        BranchDefault = 4,
        Order = 5,
        OrderItem = 6
    }

    public enum ProfitabilitySnapshotSource
    {
        PaymentFinalized = 0,
        StockProcessed = 1,
        ManualRefresh = 2
    }

    public sealed record CostSharingRuleSnapshot(
        CostSharingTargetType TargetType,
        Guid? TargetId,
        string? TargetName,
        string? TargetCode,
        CostSharingMode Mode,
        CostSharingScope Scope,
        decimal TotalCommissionPercentage,
        decimal RestaurantPercentage,
        decimal CounterpartyPercentage);

    public sealed record CostSharingCalculation(
        CostSharingRuleSnapshot Rule,
        decimal GrossAmount,
        decimal TotalCommission,
        decimal RestaurantShare,
        decimal CounterpartyShare);

    public readonly record struct CostSharingPercentages(
        decimal RestaurantPercentage,
        decimal CounterpartyPercentage);

    public class CostSharingProvider : BaseEntity
    {
        [Required]
        [MaxLength(140)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(140)]
        public string? NameAr { get; set; }

        [Required]
        [MaxLength(60)]
        public string Code { get; set; } = string.Empty;

        public CostProviderType Type { get; set; }

        public bool IsActive { get; set; } = true;

        public Guid? ExternalReferenceId { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public ICollection<CostSharingRule> Rules { get; set; } = new List<CostSharingRule>();
    }

    public class CostSharingRule : BaseEntity
    {
        public CostRuleScope Scope { get; set; } = CostRuleScope.GlobalDefault;

        public CostProviderType ProviderType { get; set; } = CostProviderType.Other;

        public Guid? ProviderId { get; set; }
        public CostSharingProvider? Provider { get; set; }

        public Guid? DeliveryPartnerId { get; set; }
        public DeliveryPartner? DeliveryPartner { get; set; }

        public Guid? PaymentMethodId { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }

        public Guid? BranchId { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? OrderItemId { get; set; }

        [MaxLength(60)]
        public string? CardTypeCode { get; set; }

        public bool IsActive { get; set; } = true;

        public int Priority { get; set; }

        public CostSharingMode Mode { get; set; } = CostSharingMode.RestaurantBearsAll;

        public CostFeeType FeeType { get; set; } = CostFeeType.Percentage;

        [Column(TypeName = "decimal(5,2)")]
        public decimal FeePercentage { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FixedFeeAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinimumFeeAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaximumFeeAmount { get; set; }

        public bool ApplyVatOnFee { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal VatPercentage { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal RestaurantPercentage { get; set; } = 100m;

        [Column(TypeName = "decimal(5,2)")]
        public decimal CounterpartyPercentage { get; set; }

        public DateTime? EffectiveFromUtc { get; set; }

        public DateTime? EffectiveToUtc { get; set; }

        [Column(TypeName = "text")]
        public string? TierDefinitionJson { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class CostSharingOverrideAudit : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid? OrderItemId { get; set; }

        public CostSharingTargetType TargetType { get; set; }

        public Guid? TargetId { get; set; }

        [Column(TypeName = "text")]
        public string? PreviousRuleJson { get; set; }

        [Column(TypeName = "text")]
        public string? NewRuleJson { get; set; }

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public Guid? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public Guid? PerformedByUserId { get; set; }
        public User? PerformedByUser { get; set; }
    }

    public class OrderProfitabilitySnapshot : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;

        [MaxLength(64)]
        public string OrderNumber { get; set; } = string.Empty;

        public OrderType OrderType { get; set; }
        public OrderSource OrderSource { get; set; }
        public OrderStatus OrderStatus { get; set; }

        public DateTime? PaidAt { get; set; }
        public Guid? CashierId { get; set; }

        [MaxLength(150)]
        public string? CashierName { get; set; }

        public Guid? CustomerId { get; set; }

        [MaxLength(140)]
        public string? CustomerName { get; set; }

        public Guid? DeliveryPartnerId { get; set; }

        [MaxLength(140)]
        public string? DeliveryPartnerName { get; set; }

        [MaxLength(140)]
        public string? DeliveryPartnerNameAr { get; set; }

        [MaxLength(60)]
        public string? DeliveryPartnerCode { get; set; }

        public Guid? PaymentMethodId { get; set; }

        [MaxLength(120)]
        public string? PaymentMethodName { get; set; }

        [MaxLength(120)]
        public string? PaymentMethodNameAr { get; set; }

        [MaxLength(60)]
        public string? PaymentMethodCode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discounts { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ServiceCharges { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Taxes { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProviderCommission { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RestaurantShare { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProviderShare { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CardFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GatewayFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OtherOperationalFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalExternalFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRestaurantFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossRevenue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetRevenue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FoodCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PackagingCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PartnerCommission { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaymentProcessingFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RestaurantCostShare { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProviderCostShare { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LaborCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OperationalCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossProfit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossMarginPercentage { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OperatingProfit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OperatingMarginPercentage { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetProfit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetMarginPercentage { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProfitPerOrder { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ContributionMargin { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ContributionPercentage { get; set; }

        [Column(TypeName = "text")]
        public string? CostSharingDetailsJson { get; set; }

        public ProfitabilitySnapshotSource Source { get; set; }

        public bool IsFinalized { get; set; }

        public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;

        public DateTime? FinalizedAt { get; set; }

        public ICollection<OrderProfitabilitySnapshotItem> Items { get; set; } =
            new List<OrderProfitabilitySnapshotItem>();
    }

    public class OrderProfitabilitySnapshotItem : BaseEntity
    {
        public Guid SnapshotId { get; set; }
        public OrderProfitabilitySnapshot Snapshot { get; set; } = null!;

        public Guid OrderId { get; set; }
        public Guid OrderItemId { get; set; }
        public Guid ProductId { get; set; }

        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? ProductNameAr { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossRevenue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FoodCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CommissionCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CardFeeCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OperationalCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossProfit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetProfit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MarginPercentage { get; set; }
    }
}
