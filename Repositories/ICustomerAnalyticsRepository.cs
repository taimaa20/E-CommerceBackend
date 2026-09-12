using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface ICustomerAnalyticsRepository
    {
        Task<CustomerAnalyticsSnapshotDto> GetDashboardAsync(
            CustomerAnalyticsFilter filter,
            bool isArabic,
            CancellationToken ct);
    }

    public sealed class CustomerAnalyticsFilter
    {
        public Guid TenantId { get; init; }
        public DateTime? DateFrom { get; init; }
        public DateTime? DateToExclusive { get; init; }
        public Guid? BranchId { get; init; }
        public string? Name { get; init; }
        public string? Phone { get; init; }
        public string? Partner { get; init; }
        public CustomerManagementType? CustomerType { get; init; }
        public bool? IsActive { get; init; }
        public int? MinOrders { get; init; }
        public int? MaxOrders { get; init; }
        public decimal? MinSpending { get; init; }
        public decimal? MaxSpending { get; init; }
    }
}
