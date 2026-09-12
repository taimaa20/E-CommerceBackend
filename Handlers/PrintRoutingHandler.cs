using MediatR;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Services.Printing;

namespace RestaurantPos.Api.Handlers
{
    /// <summary>
    /// Glue between order events and the print pipeline.
    ///
    /// Runs in its OWN service scope (via <see cref="IServiceScopeFactory"/>) so
    /// that even if the controller fires this notification without awaiting, the
    /// scoped DbContext used by <see cref="IPrintDispatcher"/> is not the same
    /// one that's about to be disposed when the request scope ends.
    ///
    /// Failures here MUST NOT bubble up: the order is already saved; printing
    /// is a side-effect. We log + swallow so the caller's response is unaffected.
    /// </summary>
    public class PrintRoutingHandler :
        INotificationHandler<OrderCreatedForPrintingEvent>,
        INotificationHandler<OrderItemsAddedForPrintingEvent>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PrintRoutingHandler> _logger;

        public PrintRoutingHandler(IServiceScopeFactory scopeFactory, ILogger<PrintRoutingHandler> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Handle(OrderCreatedForPrintingEvent notification, CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IPrintDispatcher>();
                await dispatcher.DispatchOrderCreatedAsync(
                    notification.OrderId,
                    notification.TenantId,
                    notification.CashierUserId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[PrintRouting] Dispatch failed for created order {OrderId}; order remains valid.",
                    notification.OrderId);
            }
        }

        public async Task Handle(OrderItemsAddedForPrintingEvent notification, CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IPrintDispatcher>();
                await dispatcher.DispatchItemsAddedAsync(
                    notification.OrderId,
                    notification.TenantId,
                    notification.AddedOrderItemIds,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[PrintRouting] Dispatch failed for items added to order {OrderId}; order remains valid.",
                    notification.OrderId);
            }
        }
    }
}
