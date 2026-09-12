using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerDeviceService : ICustomerDeviceService
    {
        private readonly PosDbContext _context;
        private readonly ICustomerMobileContext _mobileContext;

        public CustomerDeviceService(PosDbContext context, ICustomerMobileContext mobileContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
        }

        public async Task<CustomerDeviceDto> RegisterAsync(CustomerDeviceRegisterRequest request, CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            var deviceId = request.DeviceId.Trim();
            if (string.IsNullOrWhiteSpace(deviceId))
                throw new ValidationException("Device id is required.");

            var device = await _context.CustomerDevices
                .FirstOrDefaultAsync(d => d.CustomerId == customerId && d.DeviceId == deviceId, ct);

            if (device == null)
            {
                device = new CustomerDevice
                {
                    Id = Guid.NewGuid(),
                    TenantId = _mobileContext.GetTenantId(),
                    CustomerId = customerId,
                    DeviceId = deviceId
                };
                _context.CustomerDevices.Add(device);
            }

            device.DeviceType = ParseDeviceType(request.DeviceType);
            device.FcmToken = NormalizeNullable(request.FcmToken);
            device.IsActive = true;
            device.LastSeenAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            return MapDevice(device);
        }

        private static CustomerDeviceDto MapDevice(CustomerDevice device)
            => new()
            {
                Id = device.Id,
                DeviceId = device.DeviceId,
                DeviceType = device.DeviceType.ToString(),
                HasFcmToken = !string.IsNullOrWhiteSpace(device.FcmToken),
                LastSeenAt = device.LastSeenAt
            };

        private static CustomerDeviceType ParseDeviceType(string? value)
            => Enum.TryParse<CustomerDeviceType>(value, true, out var parsed)
                ? parsed
                : CustomerDeviceType.Unknown;

        private static string? NormalizeNullable(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
