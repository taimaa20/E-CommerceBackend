using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Payments.Application.Integration;

public interface IOrderPreparationCoordinator
{
    Task StartAfterPaymentAsync(Order order, CancellationToken ct);
}
