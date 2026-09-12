using MediatR;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Payments.Application.Integration;

public sealed class OrderPreparationCoordinator : IOrderPreparationCoordinator
{
    private readonly IPublisher _publisher;
    private readonly IOptionsMonitor<PaymentRestaurantPosOptions> _options;

    public OrderPreparationCoordinator(
        IPublisher publisher,
        IOptionsMonitor<PaymentRestaurantPosOptions> options)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task StartAfterPaymentAsync(Order order, CancellationToken ct)
    {
        if (_options.CurrentValue.StartPreparationPolicy != StartPreparationPolicy.AfterPayment)
        {
            return;
        }

        await _publisher.Publish(new OrderCreatedForPrintingEvent(order.Id, order.TenantId, order.PaidByUserId), ct);
    }
}
