using RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob.Contracts;

namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob;

public interface IPaymobClient
{
    Task<PaymobCreateIntentionResponseDto> CreatePaymentIntentionAsync(
        PaymobCreateIntentionRequestDto request,
        string correlationId,
        CancellationToken ct);
}
