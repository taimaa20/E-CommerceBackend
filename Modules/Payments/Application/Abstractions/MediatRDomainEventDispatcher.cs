using MediatR;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;

namespace RestaurantPos.Api.Modules.Payments.Application.Abstractions;

public sealed class MediatRDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IPublisher _publisher;

    public MediatRDomainEventDispatcher(IPublisher publisher)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public Task DispatchAsync(IReadOnlyCollection<PaymentEvent> events, CancellationToken ct)
        => Task.CompletedTask;

    public Task DispatchAsync<TNotification>(TNotification notification, CancellationToken ct)
        where TNotification : INotification
        => _publisher.Publish(notification, ct);
}
