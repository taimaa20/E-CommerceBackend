namespace RestaurantPos.Api.Modules.Payments.Application.Integration;

public interface IOrderPaymentService
{
    Task ApplyCompletedPaymentAsync(PaymentCompletedDomainEvent paymentEvent, CancellationToken ct);

    Task ApplyPendingPaymentAsync(PaymentPendingDomainEvent paymentEvent, CancellationToken ct);

    Task ApplyCancelledPaymentAsync(PaymentCancelledDomainEvent paymentEvent, CancellationToken ct);
}
