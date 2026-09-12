namespace RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;

public interface IPaymentWebhookMapper
{
    string ProviderCode { get; }

    GatewayWebhookEvent Map(
        string rawBody,
        string payloadHash,
        string correlationId);
}
