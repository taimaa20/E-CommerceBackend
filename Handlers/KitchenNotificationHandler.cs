using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Hubs;
using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Handlers
{
    public class KitchenNotificationHandler : INotificationHandler<OrderPaidEvent>
    {
        private readonly IHubContext<KitchenHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<KitchenNotificationHandler> _logger;

        public KitchenNotificationHandler(IHubContext<KitchenHub> hubContext, IServiceScopeFactory scopeFactory, ILogger<KitchenNotificationHandler> logger)
        {
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task Handle(OrderPaidEvent notification, CancellationToken cancellationToken)
        {
             _logger.LogInformation($"[KitchenHandler] Notifying Kitchen/POS for Order {notification.OrderId}");

            using (var scope = _scopeFactory.CreateScope())
            {
                 var context = scope.ServiceProvider.GetRequiredService<PosDbContext>();
                 
                 // Project only the fields the notification payload needs — avoids
                 // loading full Order graph + change-tracker registration.
                 var order = await context.Orders
                     .AsNoTracking()
                     .Where(o => o.Id == notification.OrderId)
                     .Select(o => new { o.BranchId, o.OrderNumber, o.TableName })
                     .FirstOrDefaultAsync(cancellationToken);

                 if (order != null)
                 {
                    await _hubContext.Clients.Group(KitchenHubGroups.Branch(order.BranchId)).SendAsync(KitchenHubEvents.OrderPaid, new
                    {
                        OrderId = notification.OrderId,
                        OrderNumber = order.OrderNumber,
                        TableName = order.TableName,
                        Status = "Paid",
                        TotalAmount = notification.TotalAmount,
                        PaymentMethod = notification.PaymentMethod
                    }, cancellationToken);

                    _logger.LogInformation($"[KitchenHandler] Notification sent for Order {order.OrderNumber}");
                 }
            }
        }
    }
}
