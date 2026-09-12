using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Modules.Marketing.Interfaces;

namespace RestaurantPos.Api.Modules.Marketing.Handlers
{
    /// <summary>
    /// Reverses loyalty points when an order is refunded (proportional to the refunded amount).
    /// Runs in its own DI scope because the event is published fire-and-forget after the request.
    /// </summary>
    public class OrderRefundedLoyaltyHandler : INotificationHandler<OrderRefundedEvent>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OrderRefundedLoyaltyHandler> _logger;

        public OrderRefundedLoyaltyHandler(IServiceScopeFactory scopeFactory, ILogger<OrderRefundedLoyaltyHandler> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task Handle(OrderRefundedEvent notification, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var wallet = scope.ServiceProvider.GetRequiredService<IWalletService>();

            var fraction = notification.OrderTotal > 0
                ? notification.RefundAmount / notification.OrderTotal
                : 1m;

            _logger.LogInformation("[Loyalty] Reversing points for refunded order {OrderId} (fraction {Fraction}).",
                notification.OrderId, fraction);

            await wallet.ReverseOrderAsync(notification.OrderId, fraction, $"refund:{notification.RefundLogId}", cancellationToken);
        }
    }

    /// <summary>
    /// Reverses loyalty points when an order is cancelled. Today cancellation only applies to
    /// unpaid orders (which never earned), so this is an idempotent safety net.
    /// </summary>
    public class OrderCancelledLoyaltyHandler : INotificationHandler<OrderCancelledEvent>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OrderCancelledLoyaltyHandler> _logger;

        public OrderCancelledLoyaltyHandler(IServiceScopeFactory scopeFactory, ILogger<OrderCancelledLoyaltyHandler> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task Handle(OrderCancelledEvent notification, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var wallet = scope.ServiceProvider.GetRequiredService<IWalletService>();

            await wallet.ReverseOrderAsync(notification.OrderId, 1m, "cancel", cancellationToken);
        }
    }
}
