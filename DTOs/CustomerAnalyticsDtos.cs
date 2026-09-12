using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public sealed class CustomerAnalyticsQueryDto
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public Guid? BranchId { get; set; }

        [EnumDataType(typeof(CustomerManagementType))]
        public CustomerManagementType? CustomerType { get; set; }

        [StringLength(140)]
        public string? Partner { get; set; }

        public bool? IsActive { get; set; }

        [Range(0, int.MaxValue)]
        public int? MinOrders { get; set; }

        [Range(0, int.MaxValue)]
        public int? MaxOrders { get; set; }

        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal? MinSpending { get; set; }

        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal? MaxSpending { get; set; }

        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }
    }

    public sealed class CustomerAnalyticsSnapshotDto
    {
        public CustomerAnalyticsSummaryDto Summary { get; set; } = new();
        public CustomerAnalyticsInsightsDto Insights { get; set; } = new();
        public List<CustomerAnalyticsPointDto> CustomerGrowth { get; set; } = new();
        public List<CustomerAnalyticsPointDto> OrdersTrend { get; set; } = new();
        public List<CustomerAnalyticsPointDto> RevenueTrend { get; set; } = new();
        public List<CustomerAnalyticsPointDto> CustomerActivity { get; set; } = new();
        public List<CustomerAnalyticsBreakdownDto> CustomerDistribution { get; set; } = new();
        public List<CustomerAnalyticsBreakdownDto> CustomerStatus { get; set; } = new();
        public List<CustomerAnalyticsPartnerBreakdownDto> PartnerDistribution { get; set; } = new();
        public List<CustomerAnalyticsPointDto> CustomerRetention { get; set; } = new();
        public List<CustomerAnalyticsCustomerDto> TopCustomers { get; set; } = new();
        public List<CustomerAnalyticsCustomerDto> RecentCustomers { get; set; } = new();
        public List<CustomerAnalyticsActivityDto> RecentActivity { get; set; } = new();
        public List<CustomerAnalyticsSegmentDto> Segments { get; set; } = new();
        public List<CustomerAnalyticsPartnerOptionDto> PartnerOptions { get; set; } = new();
        public bool BranchesEnabled { get; set; }
        public DateTime GeneratedAtUtc { get; set; }
    }

    public sealed class CustomerAnalyticsSummaryDto
    {
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public int NewCustomers { get; set; }
        public int ReturningCustomers { get; set; }
        public int InactiveCustomers { get; set; }
        public int LostCustomers { get; set; }
        public int DirectCustomers { get; set; }
        public int PartnerCustomers { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal AverageCustomerSpend { get; set; }
        public decimal AverageOrdersPerCustomer { get; set; }
        public decimal CustomerGrowthPercent { get; set; }
        public decimal RetentionRatePercent { get; set; }
    }

    public sealed class CustomerAnalyticsInsightsDto
    {
        public CustomerAnalyticsCustomerDto? HighestSpendingCustomer { get; set; }
        public CustomerAnalyticsCustomerDto? MostFrequentCustomer { get; set; }
        public CustomerAnalyticsPartnerOptionDto? MostActivePartner { get; set; }
        public string? MostActiveDay { get; set; }
        public string? MostActiveMonth { get; set; }
        public decimal AverageCustomerLifetimeDays { get; set; }
        public decimal AverageDaysBetweenOrders { get; set; }
    }

    public sealed class CustomerAnalyticsPointDto
    {
        public DateTime Date { get; set; }
        public decimal Value { get; set; }
    }

    public sealed class CustomerAnalyticsBreakdownDto
    {
        public string Label { get; set; } = string.Empty;
        public string? LabelAr { get; set; }
        public int Value { get; set; }
        public decimal Percent { get; set; }
    }

    public sealed class CustomerAnalyticsPartnerBreakdownDto
    {
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public int Customers { get; set; }
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
    }

    public sealed class CustomerAnalyticsCustomerDto
    {
        public Guid Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public CustomerManagementType CustomerType { get; set; }
        public string? PartnerName { get; set; }
        public string? PartnerNameAr { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSpending { get; set; }
        public decimal AverageOrderValue { get; set; }
        public DateTime? FirstOrderDate { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class CustomerAnalyticsActivityDto
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ActivityType { get; set; } = string.Empty;
        public string ActivityTypeAr { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public string? OrderNumber { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    public sealed class CustomerAnalyticsSegmentDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string LabelAr { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percent { get; set; }
        public List<CustomerAnalyticsCustomerDto> Customers { get; set; } = new();
    }

    public sealed class CustomerAnalyticsPartnerOptionDto
    {
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
    }
}
