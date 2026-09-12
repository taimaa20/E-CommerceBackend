using MediatR;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Handlers
{
    public class StockReductionHandler : INotificationHandler<OrderReadyEvent>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<StockReductionHandler> _logger;

        public StockReductionHandler(IServiceScopeFactory scopeFactory, ILogger<StockReductionHandler> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task Handle(OrderReadyEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"[StockHandler] Delegating stock processing for Order {notification.OrderId} to InventoryService");

            using (var scope = _scopeFactory.CreateScope())
            {
                var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();
                
                // Call FIFO Engine
                await inventoryService.ProcessOrderStockAsync(notification.OrderId);
            }
        }
    }
}
