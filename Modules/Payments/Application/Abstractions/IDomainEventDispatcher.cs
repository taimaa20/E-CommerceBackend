using MediatR;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;

namespace RestaurantPos.Api.Modules.Payments.Application.Abstractions;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<PaymentEvent> events, CancellationToken ct);

    Task DispatchAsync<TNotification>(TNotification notification, CancellationToken ct)
        where TNotification : INotification;
}
