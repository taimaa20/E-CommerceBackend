using RestaurantPos.Api.Modules.Payments.Application.ProcessWebhook;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;

namespace RestaurantPos.Api.Modules.Payments.Application.CreatePayment;

public interface IPaymentOrchestrator
{
    Task<CreatePaymentResult> CreatePaymentAsync(CreatePaymentCommand command, CancellationToken ct);

    Task<ProcessWebhookResult> ProcessWebhookAsync(GatewayWebhookEvent webhookEvent, CancellationToken ct);
}
