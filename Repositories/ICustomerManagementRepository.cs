using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface ICustomerManagementRepository
    {
        Task<PaginatedResponse<CustomerManagementListItemDto>> GetPagedAsync(
            CustomerManagementFilter filter,
            bool isArabic,
            CancellationToken ct);

        Task<CustomerManagementDetailsDto?> GetByIdAsync(
            Guid tenantId,
            Guid customerId,
            Guid? branchId,
            bool isArabic,
            CancellationToken ct);

        Task<PaginatedResponse<CustomerManagementOrderDto>?> GetOrdersAsync(
            CustomerOrdersFilter filter,
            bool isArabic,
            CancellationToken ct);
    }

    public sealed class CustomerManagementFilter
    {
        public Guid TenantId { get; init; }
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public string? Name { get; init; }
        public string? Phone { get; init; }
        public string? Partner { get; init; }
        public CustomerManagementType? CustomerType { get; init; }
        public bool? IsActive { get; init; }
        public DateTime? DateFrom { get; init; }
        public DateTime? DateToExclusive { get; init; }
        // Resolved branch scope (never the raw client value); null = all branches.
        public Guid? BranchId { get; init; }
        public CustomerSortField SortBy { get; init; }
        public CustomerSortDirection SortDirection { get; init; }
    }

    public sealed class CustomerOrdersFilter
    {
        public Guid TenantId { get; init; }
        public Guid CustomerId { get; init; }
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public string? Search { get; init; }
        public OrderStatus? Status { get; init; }
        public DateTime? DateFrom { get; init; }
        public DateTime? DateToExclusive { get; init; }
        // Resolved branch scope (never the raw client value); null = all branches.
        public Guid? BranchId { get; init; }
        public CustomerOrderSortField SortBy { get; init; }
        public CustomerSortDirection SortDirection { get; init; }
    }
}
