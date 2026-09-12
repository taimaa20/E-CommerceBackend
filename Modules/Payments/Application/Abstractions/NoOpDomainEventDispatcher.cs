using MediatR;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;

namespace RestaurantPos.Api.Modules.Payments.Application.Abstractions;

public sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IReadOnlyCollection<PaymentEvent> events, CancellationToken ct)
        => Task.CompletedTask;

    public Task DispatchAsync<TNotification>(TNotification notification, CancellationToken ct)
        where TNotification : INotification
        => Task.CompletedTask;
}
