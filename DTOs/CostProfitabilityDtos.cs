using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public class CostSharingProviderDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string Code { get; set; } = string.Empty;
        public CostProviderType Type { get; set; }
        public bool IsActive { get; set; }
        public Guid? ExternalReferenceId { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CostSharingProviderUpsertDto
    {
        [Required]
        [MaxLength(140)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(140)]
        public string? NameAr { get; set; }

        [Required]
        [MaxLength(60)]
        public string Code { get; set; } = string.Empty;

        [EnumDataType(typeof(CostProviderType))]
        public CostProviderType Type { get; set; } = CostProviderType.Other;

        public bool IsActive { get; set; } = true;

        public Guid? ExternalReferenceId { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }

    public class CostSharingRuleDto
    {
        public Guid Id { get; set; }
        public CostRuleScope Scope { get; set; }
        public CostProviderType ProviderType { get; set; }
        public Guid? ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public string? ProviderCode { get; set; }
        public Guid? DeliveryPartnerId { get; set; }
        public Guid? PaymentMethodId { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? OrderItemId { get; set; }
        public string? CardTypeCode { get; set; }
        public bool IsActive { get; set; }
        public int Priority { get; set; }
        public CostSharingMode Mode { get; set; }
        public CostFeeType FeeType { get; set; }
        public decimal FeePercentage { get; set; }
        public decimal FixedFeeAmount { get; set; }
        public decimal? MinimumFeeAmount { get; set; }
        public decimal? MaximumFeeAmount { get; set; }
        public bool ApplyVatOnFee { get; set; }
        public decimal VatPercentage { get; set; }
        public decimal RestaurantPercentage { get; set; }
        public decimal CounterpartyPercentage { get; set; }
        public DateTime? EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
        public string? TierDefinitionJson { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CostSharingRuleUpsertDto
    {
        [EnumDataType(typeof(CostRuleScope))]
        public CostRuleScope Scope { get; set; } = CostRuleScope.GlobalDefault;

        [EnumDataType(typeof(CostProviderType))]
        public CostProviderType ProviderType { get; set; } = CostProviderType.Other;

        public Guid? ProviderId { get; set; }
        public Guid? DeliveryPartnerId { get; set; }
        public Guid? PaymentMethodId { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? OrderItemId { get; set; }

        [MaxLength(60)]
        public string? CardTypeCode { get; set; }

        public bool IsActive { get; set; } = true;
        public int Priority { get; set; }

        [EnumDataType(typeof(CostSharingMode))]
        public CostSharingMode Mode { get; set; } = CostSharingMode.RestaurantBearsAll;

        [EnumDataType(typeof(CostFeeType))]
        public CostFeeType FeeType { get; set; } = CostFeeType.Percentage;

        [Range(0, 100)]
        public decimal FeePercentage { get; set; }

        [Range(0, 9999999999999999.99)]
        public decimal FixedFeeAmount { get; set; }

        [Range(0, 9999999999999999.99)]
        public decimal? MinimumFeeAmount { get; set; }

        [Range(0, 9999999999999999.99)]
        public decimal? MaximumFeeAmount { get; set; }

        public bool ApplyVatOnFee { get; set; }

        [Range(0, 100)]
        public decimal VatPercentage { get; set; }

        [Range(0, 100)]
        public decimal RestaurantPercentage { get; set; } = 100m;

        [Range(0, 100)]
        public decimal CounterpartyPercentage { get; set; }

        public DateTime? EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
        public string? TierDefinitionJson { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class CostProfitabilityFilterDto
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public Guid? PartnerId { get; set; }
        public Guid? PaymentMethodId { get; set; }
        public Guid? ProviderId { get; set; }
        public Guid? CashierId { get; set; }
        public Guid? CustomerId { get; set; }
        public OrderType? OrderType { get; set; }
        public OrderSource? OrderSource { get; set; }
        public OrderStatus? OrderStatus { get; set; }
    }

    public class CostProfitabilityDashboardDto
    {
        public CostProfitabilitySummaryDto Summary { get; set; } = new();
        public List<CostPartnerAnalyticsDto> Partners { get; set; } = new();
        public List<CostPaymentMethodAnalyticsDto> PaymentMethods { get; set; } = new();
        public List<CostProductProfitabilityDto> Products { get; set; } = new();
        public List<CostCustomerProfitabilityDto> Customers { get; set; } = new();
    }

    public class CostProfitabilitySummaryDto
    {
        public int OrderCount { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalNetSales { get; set; }
        public decimal TotalFoodCost { get; set; }
        public decimal TotalDeliveryCost { get; set; }
        public decimal TotalPartnerFees { get; set; }
        public decimal TotalCardFees { get; set; }
        public decimal TotalRestaurantCostShare { get; set; }
        public decimal TotalProviderCostShare { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal NetProfit { get; set; }
        public decimal GrossMarginPercentage { get; set; }
        public decimal NetMarginPercentage { get; set; }
        public decimal AverageProfitPerOrder { get; set; }
        public decimal AverageCommissionPerOrder { get; set; }
        public decimal AverageFeePercentage { get; set; }
        public decimal AverageTransactionCost { get; set; }
    }

    public class CostPartnerAnalyticsDto
    {
        public Guid? PartnerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Code { get; set; }
        public int OrderCount { get; set; }
        public decimal GrossSales { get; set; }
        public decimal NetSales { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal RestaurantShare { get; set; }
        public decimal PartnerShare { get; set; }
        public decimal TotalCost { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitMargin { get; set; }
    }

    public class CostPaymentMethodAnalyticsDto
    {
        public Guid? PaymentMethodId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Code { get; set; }
        public int Transactions { get; set; }
        public decimal Sales { get; set; }
        public decimal Fees { get; set; }
        public decimal RestaurantShare { get; set; }
        public decimal ProviderShare { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal AverageFee { get; set; }
        public decimal AverageCost { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitMargin { get; set; }
    }

    public class CostProductProfitabilityDto
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
        public decimal FoodCost { get; set; }
        public decimal CommissionCost { get; set; }
        public decimal CardFeeCost { get; set; }
        public decimal OperationalCost { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal NetProfit { get; set; }
        public decimal Margin { get; set; }
        public int Ranking { get; set; }
    }

    public class CostCustomerProfitabilityDto
    {
        public Guid? CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Visits { get; set; }
        public decimal LifetimeRevenue { get; set; }
        public decimal LifetimeCost { get; set; }
        public decimal LifetimeProfit { get; set; }
        public decimal AverageProfitPerVisit { get; set; }
    }

    public class OrderProfitabilitySnapshotDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal GrossSales { get; set; }
        public decimal Discounts { get; set; }
        public decimal ServiceCharges { get; set; }
        public decimal Taxes { get; set; }
        public decimal NetSales { get; set; }
        public decimal ProviderCommission { get; set; }
        public decimal RestaurantShare { get; set; }
        public decimal ProviderShare { get; set; }
        public decimal CardFees { get; set; }
        public decimal GatewayFees { get; set; }
        public decimal DeliveryFees { get; set; }
        public decimal OtherOperationalFees { get; set; }
        public decimal TotalExternalFees { get; set; }
        public decimal TotalRestaurantFees { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal FoodCost { get; set; }
        public decimal PackagingCost { get; set; }
        public decimal DeliveryCost { get; set; }
        public decimal PartnerCommission { get; set; }
        public decimal PaymentProcessingFee { get; set; }
        public decimal RestaurantCostShare { get; set; }
        public decimal ProviderCostShare { get; set; }
        public decimal LaborCost { get; set; }
        public decimal OperationalCost { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossMarginPercentage { get; set; }
        public decimal OperatingProfit { get; set; }
        public decimal OperatingMarginPercentage { get; set; }
        public decimal NetProfit { get; set; }
        public decimal NetMarginPercentage { get; set; }
        public decimal ProfitPerOrder { get; set; }
        public decimal ContributionMargin { get; set; }
        public decimal ContributionPercentage { get; set; }
        public ProfitabilitySnapshotSource Source { get; set; }
        public bool IsFinalized { get; set; }
        public DateTime SnapshotAt { get; set; }
        public DateTime? FinalizedAt { get; set; }
        public List<OrderProfitabilitySnapshotItemDto> Items { get; set; } = new();
    }

    public class OrderProfitabilitySnapshotItemDto
    {
        public Guid OrderItemId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductNameAr { get; set; }
        public int Quantity { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal FoodCost { get; set; }
        public decimal CommissionCost { get; set; }
        public decimal CardFeeCost { get; set; }
        public decimal OperationalCost { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal NetProfit { get; set; }
        public decimal MarginPercentage { get; set; }
    }
}
