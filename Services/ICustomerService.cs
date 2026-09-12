using System.Threading.Tasks;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public interface ICustomerService
    {
        Task<Customer?> GetCustomerByPhoneAsync(string phoneNumber);
        Task<CustomerInsightsDto> GetCustomerInsightsAsync(Guid customerId);
        Task AddLoyaltyPointsAsync(Guid customerId, decimal amount);
        Task<bool> RedeemPointsAsync(Guid customerId, Guid orderId);

        /// <summary>Creates a new customer for the given tenant. Tenant ID and audit fields
        /// are set by the service — never by the request DTO.</summary>
        Task<Customer> CreateAsync(Guid tenantId, CustomerCreateDto input, CancellationToken ct = default);
    }

    public class CustomerInsightsDto
    {
        public Guid CustomerId { get; set; }
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public string DisplayName { get; set; }
        public string? PhoneNumber { get; set; }
        public string Tier { get; set; }
        public decimal Points { get; set; }
        public DateTime LastVisit { get; set; }
        public List<OrderSummaryDto> LastOrders { get; set; }
        public List<FavoriteProductDto> FavoriteProducts { get; set; }
    }

    public class OrderSummaryDto
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
    }

    public class FavoriteProductDto
    {
        public string ProductName { get; set; }
        public int Count { get; set; }
    }
}
