using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Modules.Payments.Application.CreatePayment;

namespace RestaurantPos.Api.Services;

public interface IOnlineShoppingCheckoutService
{
    Task<CreatePaymentResult> CreateAsync(OnlineShoppingCheckoutRequest request, CancellationToken ct);
}
