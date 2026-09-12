using MediatR;

namespace RestaurantPos.Api.Modules.Payments.Application.Integration;

public sealed class PaymentCompletedDomainEventHandler :
    INotificationHandler<PaymentCompletedDomainEvent>,
    INotificationHandler<PaymentPendingDomainEvent>,
    INotificationHandler<PaymentCancelledDomainEvent>
{
    private readonly IOrderPaymentService _orderPaymentService;

    public PaymentCompletedDomainEventHandler(IOrderPaymentService orderPaymentService)
    {
        _orderPaymentService = orderPaymentService ?? throw new ArgumentNullException(nameof(orderPaymentService));
    }

    public Task Handle(PaymentCompletedDomainEvent notification, CancellationToken cancellationToken)
        => _orderPaymentService.ApplyCompletedPaymentAsync(notification, cancellationToken);

    public Task Handle(PaymentPendingDomainEvent notification, CancellationToken cancellationToken)
        => _orderPaymentService.ApplyPendingPaymentAsync(notification, cancellationToken);

    public Task Handle(PaymentCancelledDomainEvent notification, CancellationToken cancellationToken)
        => _orderPaymentService.ApplyCancelledPaymentAsync(notification, cancellationToken);
}
