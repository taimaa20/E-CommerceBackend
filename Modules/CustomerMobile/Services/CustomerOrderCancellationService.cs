using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerOrderCancellationService : ICustomerOrderCancellationService
    {
        private readonly PosDbContext _context;
        private readonly ICustomerMobileContext _mobileContext;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<CustomerOrderCancellationService> _logger;

        public CustomerOrderCancellationService(
            PosDbContext context,
            ICustomerMobileContext mobileContext,
            ISettingsService settingsService,
            ILogger<CustomerOrderCancellationService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CustomerCancelOrderResponse> CancelAsync(
            Guid orderId,
            CustomerCancelOrderRequest request,
            CancellationToken ct)
        {
            var reason = NormalizeReason(request.Reason);
            var link = await LoadOrderAsync(orderId, ct);
            var settings = await _settingsService.GetSettingsAsync(link.TenantId, ct);
            ValidateCanCancel(link.Order, settings);

            var now = DateTime.UtcNow;
            ApplyCancellation(link.Order, reason, now);
            AddAuditLog(link, reason, now);

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Customer {CustomerId} cancelled mobile order {OrderId}", link.CustomerId, link.OrderId);
            return new CustomerCancelOrderResponse { Success = true, Status = link.Order.Status.ToString() };
        }

        private async Task<CustomerMobileOrder> LoadOrderAsync(Guid orderId, CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            return await _context.CustomerMobileOrders
                .Include(m => m.Order)
                .FirstOrDefaultAsync(m => m.CustomerId == customerId && m.OrderId == orderId, ct)
                ?? throw new NotFoundException("Customer order was not found.");
        }

        private static void ValidateCanCancel(Order order, SystemSettings settings)
        {
            if (order.PaidAt.HasValue)
                throw new ValidationException("Paid orders cannot be cancelled.");

            if (order.Status == OrderStatus.New)
                return;

            if (order.Status == OrderStatus.Preparing && settings.AllowMobileCancelPreparing)
                return;

            throw new ValidationException($"Order cannot be cancelled while it is {order.Status}.");
        }

        private static void ApplyCancellation(Order order, string reason, DateTime now)
        {
            order.Status = OrderStatus.Cancelled;
            order.CancelMode = OrderCancelModes.Direct;
            order.CanceledAt = now;
            order.CancelReason = reason;
            order.LastUpdatedAt = now;
            order.SyncedAt = now;
        }

        private void AddAuditLog(CustomerMobileOrder link, string reason, DateTime now)
            => _context.CustomerMobileAuditLogs.Add(new CustomerMobileAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = link.TenantId,
                CustomerId = link.CustomerId,
                OrderId = link.OrderId,
                Action = CustomerMobileAuditAction.OrderCancelled,
                Reason = reason,
                ActionAt = now
            });

        private static string NormalizeReason(string reason)
        {
            var normalized = reason.Trim();
            return normalized.Length >= 3
                ? normalized
                : throw new ValidationException("Cancellation reason is required.");
        }
    }
}
