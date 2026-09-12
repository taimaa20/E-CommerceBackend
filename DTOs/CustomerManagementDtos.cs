using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public enum CustomerManagementType
    {
        Direct = 0,
        Partner = 1
    }

    public enum CustomerSortField
    {
        CustomerId = 0,
        Name = 1,
        Phone = 2,
        CustomerType = 3,
        PartnerName = 4,
        TotalOrders = 5,
        LastOrderDate = 6,
        Status = 7
    }

    public enum CustomerOrderSortField
    {
        OrderDate = 0,
        OrderNumber = 1,
        TotalAmount = 2,
        Status = 3
    }

    public enum CustomerSortDirection
    {
        Ascending = 0,
        Descending = 1
    }

    public sealed class CustomerManagementQueryDto
    {
        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 25;

        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(140)]
        public string? Partner { get; set; }

        [EnumDataType(typeof(CustomerManagementType))]
        public CustomerManagementType? CustomerType { get; set; }

        public bool? IsActive { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public Guid? BranchId { get; set; }

        [EnumDataType(typeof(CustomerSortField))]
        public CustomerSortField SortBy { get; set; } = CustomerSortField.LastOrderDate;

        [EnumDataType(typeof(CustomerSortDirection))]
        public CustomerSortDirection SortDirection { get; set; } = CustomerSortDirection.Descending;
    }

    public sealed class CustomerOrdersQueryDto
    {
        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;

        [StringLength(64)]
        public string? Search { get; set; }

        [EnumDataType(typeof(OrderStatus))]
        public OrderStatus? Status { get; set; }

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public Guid? BranchId { get; set; }

        [EnumDataType(typeof(CustomerOrderSortField))]
        public CustomerOrderSortField SortBy { get; set; } = CustomerOrderSortField.OrderDate;

        [EnumDataType(typeof(CustomerSortDirection))]
        public CustomerSortDirection SortDirection { get; set; } = CustomerSortDirection.Descending;
    }

    public sealed class CustomerManagementListItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public CustomerManagementType CustomerType { get; set; }
        public string? PartnerName { get; set; }
        public string? PartnerNameAr { get; set; }
        public int TotalOrders { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class CustomerManagementDetailsDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime? BirthDate { get; set; }
        public CustomerManagementType CustomerType { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public CustomerOrderStatisticsDto OrderStatistics { get; set; } = new();
        public CustomerPartnerInfoDto? Partner { get; set; }
    }

    public sealed class CustomerOrderStatisticsDto
    {
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
        public decimal TotalSpend { get; set; }
        public decimal AverageOrderValue { get; set; }
        public DateTime? FirstOrderDate { get; set; }
        public DateTime? LastOrderDate { get; set; }
    }

    public sealed class CustomerPartnerInfoDto
    {
        public Guid? PartnerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Code { get; set; }
        public string? LastPartnerOrderNumber { get; set; }
        public DateTime LastPartnerOrderDate { get; set; }
    }

    public sealed class CustomerManagementOrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public OrderType OrderType { get; set; }
        public OrderSource OrderSource { get; set; }
        public string? PartnerName { get; set; }
        public string? PartnerNameAr { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime OrderDate { get; set; }
    }
}
