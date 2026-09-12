using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Modules.Payments.Application.CreatePayment;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services;

public sealed class OnlineShoppingCheckoutService : IOnlineShoppingCheckoutService
{
    private readonly IOnlineShoppingRepository _repository;

    public OnlineShoppingCheckoutService(
        IOnlineShoppingRepository repository,
        ISettingsService settingsService,
        IPaymentService paymentService,
        ILogger<OnlineShoppingCheckoutService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<CreatePaymentResult> CreateAsync(OnlineShoppingCheckoutRequest request, CancellationToken ct)
    {
        _ = await _repository.GetOnlineOrderAsync(request.OrderId, request.ClientOrderUuid.Trim(), ct)
            ?? throw new NotFoundException("Online order was not found.");
        throw new ValidationException("Online payment processing is unavailable. Payment must be collected through the existing payment flow.");
    }
}
