using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerOrderTrackingService : ICustomerOrderTrackingService
    {
        private readonly PosDbContext _context;
        private readonly ICustomerMobileContext _mobileContext;
        private readonly ISettingsService _settingsService;

        public CustomerOrderTrackingService(
            PosDbContext context,
            ICustomerMobileContext mobileContext,
            ISettingsService settingsService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public async Task<CustomerOrderTrackingDto> GetTrackingAsync(Guid orderId, CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            var link = await _context.CustomerMobileOrders
                .AsNoTracking()
                .Include(m => m.Order)
                    .ThenInclude(o => o.OrderItems)
                .FirstOrDefaultAsync(m => m.CustomerId == customerId && m.OrderId == orderId, ct)
                ?? throw new NotFoundException("Customer order was not found.");

            var settings = await _settingsService.GetSettingsAsync(link.TenantId, ct);
            var currentStatus = ResolveCustomerStatus(link.Order);

            return new CustomerOrderTrackingDto
            {
                OrderId = link.OrderId,
                OrderNumber = link.Order.DisplayOrderNumber ?? link.OrderNumber,
                CurrentStatus = currentStatus,
                Eta = ResolveEta(link.Order, settings.OrderPrepTimeout),
                Timeline = BuildTimeline(link.Order, currentStatus)
            };
        }

        private static DateTime? ResolveEta(Order order, int prepMinutes)
        {
            if (order.Status is OrderStatus.Cancelled or OrderStatus.Completed or OrderStatus.Served)
                return null;

            return order.CreatedAt.AddMinutes(prepMinutes);
        }

        private static string ResolveCustomerStatus(Order order)
        {
            if (order.Status == OrderStatus.Cancelled)
                return "Cancelled";

            if (order.OrderType == OrderType.Delivery)
            {
                return order.Status switch
                {
                    OrderStatus.New => "OrderPlaced",
                    OrderStatus.Preparing => "Preparing",
                    OrderStatus.Ready => "Ready",
                    OrderStatus.Served => "OutForDelivery",
                    OrderStatus.Paid or OrderStatus.Completed => "Delivered",
                    _ => order.Status.ToString()
                };
            }

            return order.Status switch
            {
                OrderStatus.New => "OrderPlaced",
                OrderStatus.Preparing => "Preparing",
                OrderStatus.Ready => "Ready",
                OrderStatus.Paid or OrderStatus.Completed => "PickedUp",
                _ => order.Status.ToString()
            };
        }

        private static List<CustomerOrderTimelineItemDto> BuildTimeline(Order order, string currentStatus)
        {
            var statuses = order.OrderType == OrderType.Delivery
                ? new[] { "OrderPlaced", "Preparing", "Ready", "OutForDelivery", "Delivered" }
                : new[] { "OrderPlaced", "Preparing", "Ready", "PickedUp" };

            if (currentStatus == "Cancelled")
                statuses = statuses.Concat(new[] { "Cancelled" }).ToArray();

            var currentIndex = Array.IndexOf(statuses, currentStatus);
            return statuses.Select((status, index) => new CustomerOrderTimelineItemDto
            {
                Status = status,
                Label = SplitPascalCase(status),
                IsCompleted = currentIndex >= 0 && index <= currentIndex,
                IsCurrent = status == currentStatus,
                OccurredAt = ResolveOccurredAt(order, status, index <= currentIndex)
            }).ToList();
        }

        private static DateTime? ResolveOccurredAt(Order order, string status, bool completed)
        {
            if (!completed)
                return null;

            return status switch
            {
                "OrderPlaced" => order.CreatedAt,
                "Cancelled" => order.CanceledAt,
                "PickedUp" or "Delivered" => order.PaidAt ?? order.LastUpdatedAt ?? order.SyncedAt,
                _ => order.LastUpdatedAt ?? order.SyncedAt
            };
        }

        private static string SplitPascalCase(string value)
        {
            var chars = value.SelectMany((ch, index) =>
                index > 0 && char.IsUpper(ch) ? new[] { ' ', ch } : new[] { ch });
            return new string(chars.ToArray());
        }
    }
}
