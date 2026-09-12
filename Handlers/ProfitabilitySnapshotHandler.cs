using MediatR;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Handlers
{
    public class ProfitabilitySnapshotHandler :
        INotificationHandler<OrderPaidEvent>,
        INotificationHandler<OrderStockProcessedEvent>
    {
        private readonly ICostProfitabilityService _service;
        private readonly ILogger<ProfitabilitySnapshotHandler> _logger;

        public ProfitabilitySnapshotHandler(
            ICostProfitabilityService service,
            ILogger<ProfitabilitySnapshotHandler> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task Handle(OrderPaidEvent notification, CancellationToken cancellationToken)
            => CaptureAsync(notification.OrderId, ProfitabilitySnapshotSource.PaymentFinalized, cancellationToken);

        public Task Handle(OrderStockProcessedEvent notification, CancellationToken cancellationToken)
            => CaptureAsync(notification.OrderId, ProfitabilitySnapshotSource.StockProcessed, cancellationToken);

        private async Task CaptureAsync(
            Guid orderId,
            ProfitabilitySnapshotSource source,
            CancellationToken ct)
        {
            try
            {
                await _service.CaptureSnapshotAsync(orderId, source, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Profitability snapshot capture failed for order {OrderId}", orderId);
            }
        }
    }
}
