using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerProfileService : ICustomerProfileService
    {
        private readonly PosDbContext _context;
        private readonly ICustomerMobileContext _mobileContext;
        private readonly ILogger<CustomerProfileService> _logger;

        public CustomerProfileService(
            PosDbContext context,
            ICustomerMobileContext mobileContext,
            ILogger<CustomerProfileService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CustomerProfileDto> GetProfileAsync(CancellationToken ct)
            => CustomerAuthService.MapProfile(await GetCustomerAsync(ct));

        public async Task<CustomerProfileDto> UpdateProfileAsync(CustomerProfileUpdateRequest request, CancellationToken ct)
        {
            var customer = await GetCustomerAsync(ct);
            customer.FirstName = request.FirstName.Trim();
            customer.LastName = request.LastName.Trim();
            customer.PreferredLanguage = ParseLanguage(request.PreferredLanguage);
            await _context.SaveChangesAsync(ct);
            return CustomerAuthService.MapProfile(customer);
        }

        public async Task<List<CustomerAddressDto>> GetAddressesAsync(CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            var addresses = await _context.CustomerAddresses
                .AsNoTracking()
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync(ct);

            return addresses.Select(MapAddress).ToList();
        }

        public async Task<CustomerAddressDto> AddAddressAsync(CustomerAddressRequest request, CancellationToken ct)
        {
            var tenantId = _mobileContext.GetTenantId();
            var customerId = _mobileContext.GetCustomerId();
            await ValidateDeliveryZoneAsync(request.DeliveryZoneId, ct);

            var address = new CustomerAddress
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = customerId,
                AddressName = request.AddressName.Trim(),
                Area = request.Area.Trim(),
                Street = request.Street.Trim(),
                Building = NormalizeNullable(request.Building),
                Floor = NormalizeNullable(request.Floor),
                Apartment = NormalizeNullable(request.Apartment),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                DeliveryZoneId = request.DeliveryZoneId,
                IsDefault = request.IsDefault
            };

            if (address.IsDefault)
                await ClearDefaultAddressAsync(customerId, ct);

            _context.CustomerAddresses.Add(address);
            await _context.SaveChangesAsync(ct);
            return MapAddress(address);
        }

        public async Task<CustomerAddressDto> UpdateAddressAsync(Guid id, CustomerAddressRequest request, CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            var address = await GetAddressAsync(customerId, id, ct);
            await ValidateDeliveryZoneAsync(request.DeliveryZoneId, ct);

            address.AddressName = request.AddressName.Trim();
            address.Area = request.Area.Trim();
            address.Street = request.Street.Trim();
            address.Building = NormalizeNullable(request.Building);
            address.Floor = NormalizeNullable(request.Floor);
            address.Apartment = NormalizeNullable(request.Apartment);
            address.Latitude = request.Latitude;
            address.Longitude = request.Longitude;
            address.DeliveryZoneId = request.DeliveryZoneId;

            if (request.IsDefault && !address.IsDefault)
                await ClearDefaultAddressAsync(customerId, ct);

            address.IsDefault = request.IsDefault;
            await _context.SaveChangesAsync(ct);
            return MapAddress(address);
        }

        public async Task DeleteAddressAsync(Guid id, CancellationToken ct)
        {
            var address = await GetAddressAsync(_mobileContext.GetCustomerId(), id, ct);
            address.IsDefault = false;
            address.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        public async Task<CustomerMobileActionResponse> DeleteAccountAsync(CancellationToken ct)
        {
            var customer = await LoadCustomerForDeletionAsync(ct);
            var now = DateTime.UtcNow;

            customer.IsActive = false;
            customer.IsDeleted = true;
            customer.DeletedAt = now;

            RevokeRefreshTokens(customer, now);
            DisableDevices(customer);
            ClearActiveCarts(customer);
            AddAuditLog(customer.Id, customer.TenantId, now);

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Customer mobile account {CustomerId} deleted", customer.Id);
            return new CustomerMobileActionResponse { Success = true, Message = "Account deleted successfully." };
        }

        private async Task<CustomerAccount> LoadCustomerForDeletionAsync(CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            return await _context.CustomerAccounts
                .Include(c => c.RefreshTokens)
                .Include(c => c.Devices)
                .Include(c => c.Carts)
                .FirstOrDefaultAsync(c => c.Id == customerId, ct)
                ?? throw new NotFoundException("Customer profile was not found.");
        }

        private static void RevokeRefreshTokens(CustomerAccount customer, DateTime now)
        {
            foreach (var token in customer.RefreshTokens.Where(t => t.RevokedAt == null))
                token.RevokedAt = now;
        }

        private static void DisableDevices(CustomerAccount customer)
        {
            foreach (var device in customer.Devices)
            {
                device.FcmToken = null;
                device.IsActive = false;
            }
        }

        private static void ClearActiveCarts(CustomerAccount customer)
        {
            foreach (var cart in customer.Carts.Where(c => c.IsActive))
                cart.IsActive = false;
        }

        private void AddAuditLog(Guid customerId, Guid tenantId, DateTime now)
            => _context.CustomerMobileAuditLogs.Add(new CustomerMobileAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = customerId,
                Action = CustomerMobileAuditAction.AccountDeleted,
                ActionAt = now
            });

        private async Task<CustomerAccount> GetCustomerAsync(CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            return await _context.CustomerAccounts.FirstOrDefaultAsync(c => c.Id == customerId, ct)
                ?? throw new NotFoundException("Customer profile was not found.");
        }

        private async Task<CustomerAddress> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken ct)
        {
            return await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Id == addressId, ct)
                ?? throw new NotFoundException("Customer address was not found.");
        }

        private async Task ClearDefaultAddressAsync(Guid customerId, CancellationToken ct)
        {
            var defaults = await _context.CustomerAddresses
                .Where(a => a.CustomerId == customerId && a.IsDefault)
                .ToListAsync(ct);

            foreach (var address in defaults)
                address.IsDefault = false;
        }

        private async Task ValidateDeliveryZoneAsync(Guid? deliveryZoneId, CancellationToken ct)
        {
            if (!deliveryZoneId.HasValue)
                return;

            // Mobile orders always land on the Main Branch, so only Main-Branch zones apply.
            var exists = await _context.DeliveryZones
                .AsNoTracking()
                .AnyAsync(z => z.Id == deliveryZoneId.Value && z.IsActive && z.Branch!.IsMainBranch, ct);

            if (!exists)
                throw new ValidationException("Delivery zone is invalid or inactive.");
        }

        private static CustomerAddressDto MapAddress(CustomerAddress address)
            => new()
            {
                Id = address.Id,
                AddressName = address.AddressName,
                Area = address.Area,
                Street = address.Street,
                Building = address.Building,
                Floor = address.Floor,
                Apartment = address.Apartment,
                Latitude = address.Latitude,
                Longitude = address.Longitude,
                DeliveryZoneId = address.DeliveryZoneId,
                IsDefault = address.IsDefault,
                CreatedAt = address.CreatedAt
            };

        private static CustomerPreferredLanguage ParseLanguage(string? value)
            => string.Equals(value?.Trim(), "ar", StringComparison.OrdinalIgnoreCase)
                ? CustomerPreferredLanguage.Ar
                : CustomerPreferredLanguage.En;

        private static string? NormalizeNullable(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
