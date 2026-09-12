using MediatR;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Modules.Retail.Services;

namespace RestaurantPos.Api.Modules.Retail.Handlers
{
    /// <summary>
    /// Relieves retail stock when an order is paid.
    ///
    /// A retail counter sale is created and paid in one motion — nobody presses "ready" for a
    /// bottle handed across the counter — so payment is that sale's fulfilment signal. Orders
    /// that DO go through a readiness step are already relieved by then; this handler finds the
    /// movement in place and posts nothing, because the reconciler derives what should exist
    /// rather than applying a delta.
    ///
    /// Restaurant orders are untouched: their products are not on the finished-goods ledger, so
    /// the reconciler returns without reading or writing anything.
    /// </summary>
    public sealed class RetailStockOnOrderPaidHandler : INotificationHandler<OrderPaidEvent>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RetailStockOnOrderPaidHandler> _logger;

        public RetailStockOnOrderPaidHandler(
            IServiceScopeFactory scopeFactory,
            ILogger<RetailStockOnOrderPaidHandler> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Handle(OrderPaidEvent notification, CancellationToken cancellationToken)
        {
            // OrderPaidEvent is published fire-and-forget from the payment path, so this runs
            // outside the caller's scope and needs its own.
            using var scope = _scopeFactory.CreateScope();
            var reconciler = scope.ServiceProvider.GetRequiredService<IRetailOrderStockService>();

            try
            {
                await reconciler.SyncOrderAsync(notification.OrderId, cancellationToken);
            }
            catch (Exception ex)
            {
                // A stock-ledger failure must never fail a payment that already succeeded. The
                // next fulfilment or cancellation signal on this order reconciles it.
                _logger.LogError(ex,
                    "Retail stock reconciliation failed after payment of order {OrderId}",
                    notification.OrderId);
            }
        }
    }
}
