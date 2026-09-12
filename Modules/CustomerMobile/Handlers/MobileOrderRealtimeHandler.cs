using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Modules.CustomerMobile.Hubs;

namespace RestaurantPos.Api.Modules.CustomerMobile.Handlers
{
    public class MobileOrderRealtimeHandler : INotificationHandler<OrderCreatedForPrintingEvent>
    {
        private readonly PosDbContext _context;
        private readonly IHubContext<MobileOrderHub> _hubContext;

        public MobileOrderRealtimeHandler(PosDbContext context, IHubContext<MobileOrderHub> hubContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task Handle(OrderCreatedForPrintingEvent notification, CancellationToken cancellationToken)
        {
            var mobileOrder = await _context.CustomerMobileOrders
                .AsNoTracking()
                .Where(o => o.OrderId == notification.OrderId)
                .Select(o => new
                {
                    o.OrderId,
                    o.OrderNumber
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (mobileOrder == null)
                return;

            await _hubContext.Clients
                .Group(MobileOrderHub.GroupName(mobileOrder.OrderId))
                .SendAsync(MobileOrderHubEvents.OrderPlaced, new
                {
                    mobileOrder.OrderId,
                    mobileOrder.OrderNumber,
                    Status = MobileOrderHubEvents.OrderPlaced
                }, cancellationToken);
        }
    }
}
