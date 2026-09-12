using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly PosDbContext _context;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(PosDbContext context, ILogger<CustomerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Customer> CreateAsync(Guid tenantId, CustomerCreateDto input, CancellationToken ct = default)
        {
            // Service owns TenantId, audit fields, and loyalty defaults — never trust the request.
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = input.Name.Trim(),
                NameAr = string.IsNullOrWhiteSpace(input.NameAr) ? null : input.NameAr.Trim(),
                PhoneNumber = input.PhoneNumber.Trim(),
                Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
                BirthDate = input.BirthDate,
                Tier = CustomerTier.Standard,
                LoyaltyPoints = 0m,
                LastVisit = DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync(ct);
            return customer;
        }

        public async Task<Customer?> GetCustomerByPhoneAsync(string phoneNumber)
        {
            return await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);
        }

        public async Task<CustomerInsightsDto> GetCustomerInsightsAsync(Guid customerId)
        {
            var customer = await _context.Customers
                .Include(c => c.Orders)
                    .ThenInclude(o => o.OrderItems)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer == null) return null;

            // 1. Last 5 Orders
            var lastOrders = customer.Orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    Date = o.CreatedAt,
                    Total = o.TotalAmount
                })
                .ToList();

            // 2. Favorite Products
            var favoriteProducts = customer.Orders
                .SelectMany(o => o.OrderItems)
                .GroupBy(oi => oi.ProductName)
                .Select(g => new FavoriteProductDto
                {
                    ProductName = g.Key,
                    Count = g.Sum(oi => oi.Quantity)
                })
                .OrderByDescending(x => x.Count)
                .Take(3)
                .ToList();

            return new CustomerInsightsDto
            {
                CustomerId = customer.Id,
                Name = customer.Name,
                NameAr = customer.NameAr,
                DisplayName = ResolveDisplayName(customer.Name, customer.NameAr),
                PhoneNumber = customer.PhoneNumber,
                Tier = customer.Tier.ToString(),
                Points = customer.LoyaltyPoints,
                LastVisit = customer.LastVisit,
                LastOrders = lastOrders,
                FavoriteProducts = favoriteProducts
            };
        }

        public async Task AddLoyaltyPointsAsync(Guid customerId, decimal amount)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null) return;

            // Tier Logic
            decimal rate = customer.Tier switch
            {
                CustomerTier.Bronze => 0.03m,
                CustomerTier.Silver => 0.05m,
                CustomerTier.Gold => 0.10m,
                CustomerTier.VIP => 0.15m,
                _ => 0.01m // Standard %1
            };

            decimal pointsEarned = amount * rate;
            customer.LoyaltyPoints += pointsEarned;
            customer.LastVisit = DateTime.UtcNow;

            // Tier Upgrade Logic (Simple)
            if (customer.Tier == CustomerTier.Standard && customer.LoyaltyPoints > 500) customer.Tier = CustomerTier.Bronze;
            else if (customer.Tier == CustomerTier.Bronze && customer.LoyaltyPoints > 1500) customer.Tier = CustomerTier.Silver;
            else if (customer.Tier == CustomerTier.Silver && customer.LoyaltyPoints > 5000) customer.Tier = CustomerTier.Gold;

            _logger.LogInformation($"Customer {customer.Name} earned {pointsEarned:C2} points. New Balance: {customer.LoyaltyPoints:C2}");
            
            await _context.SaveChangesAsync();
        }

        public async Task<bool> RedeemPointsAsync(Guid customerId, Guid orderId)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            var order = await _context.Orders.FindAsync(orderId);

            if (customer == null || order == null) return false;
            if (customer.LoyaltyPoints <= 0) return false;

            // Apply Discount
            decimal discountAmount = customer.LoyaltyPoints; 
            
            // Limit discount to order total
            if (discountAmount > order.TotalAmount)
            {
                 discountAmount = order.TotalAmount;
            }

            order.TotalAmount -= discountAmount; // Apply discount
            customer.LoyaltyPoints -= discountAmount; // Deduct points
            
            // Note: In a real system, we should likely store 'PointsRedeemed' field on Order to track this accounting-wise
            // putting it as a simple reduction for this MVP
            
            _logger.LogInformation($"Redeemed {discountAmount:C2} points for Order {order.OrderNumber}");
            
            await _context.SaveChangesAsync();
            return true;
        }

        private static string ResolveDisplayName(string name, string? nameAr)
        {
            var currentLanguage = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return currentLanguage == "ar" && !string.IsNullOrWhiteSpace(nameAr) ? nameAr : name;
        }
    }
}
