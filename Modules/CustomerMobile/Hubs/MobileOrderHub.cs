using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Hubs
{
    public static class MobileOrderHubEvents
    {
        public const string OrderPlaced = "OrderPlaced";
        public const string Preparing = "Preparing";
        public const string Ready = "Ready";
        public const string OutForDelivery = "OutForDelivery";
        public const string Delivered = "Delivered";
        public const string Cancelled = "Cancelled";
    }

    [Authorize(Roles = AppRoleNames.Customer)]
    public class MobileOrderHub : Hub
    {
        private readonly PosDbContext _context;

        public MobileOrderHub(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task SubscribeToOrder(Guid orderId)
        {
            var customerId = ResolveCustomerId();
            var ownsOrder = await _context.CustomerMobileOrders
                .AsNoTracking()
                .AnyAsync(o => o.CustomerId == customerId && o.OrderId == orderId, Context.ConnectionAborted);

            if (!ownsOrder)
                throw new HubException("Order was not found.");

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(orderId), Context.ConnectionAborted);
        }

        public Task UnsubscribeFromOrder(Guid orderId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(orderId), Context.ConnectionAborted);

        public static string GroupName(Guid orderId) => $"mobile-order:{orderId}";

        private Guid ResolveCustomerId()
        {
            var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id)
                ? id
                : throw new HubException("Invalid customer session.");
        }
    }
}
