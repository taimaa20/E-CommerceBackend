using Microsoft.Extensions.Options;
using RestaurantPos.Api.Modules.Payments.Application.Abstractions;
using RestaurantPos.Api.Modules.Payments.Data;
using RestaurantPos.Api.Modules.Payments.Data.Repositories;
using RestaurantPos.Api.Modules.Payments.Application.CreatePayment;
using RestaurantPos.Api.Modules.Payments.Application.Integration;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob;
using RestaurantPos.Api.Modules.Payments.Interfaces;

namespace RestaurantPos.Api.Modules.Payments;

public static class PaymentEngineServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentEngineInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<PaymobOptions>()
            .BindConfiguration(PaymobOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PaymobOptions>, PaymobOptionsValidator>();
        services.AddOptions<PaymentRestaurantPosOptions>()
            .BindConfiguration(PaymentRestaurantPosOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PaymentRestaurantPosOptions>, PaymentRestaurantPosOptionsValidator>();
        services.AddTransient<PaymobResilienceHandler>();
        services.AddHttpClient(PaymobClient.HttpClientName, (serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<PaymobOptions>>().CurrentValue;
            httpClient.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }).AddHttpMessageHandler<PaymobResilienceHandler>();

        services.AddScoped<IPaymobClient, PaymobClient>();
        services.AddScoped<IPaymentGateway, PaymobGateway>();
        services.AddScoped<IPaymentWebhookVerifier, PaymobWebhookVerifier>();
        services.AddScoped<IPaymentWebhookMapper, PaymobWebhookMapper>();
        services.AddScoped<CreatePaymentCommandValidator>();
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
        services.AddScoped<IOrderPreparationCoordinator, OrderPreparationCoordinator>();
        services.AddScoped<IOrderPaymentService, OrderPaymentService>();

        services.AddScoped<IPaymentEngineRepository, PaymentEngineRepository>();
        services.AddScoped<IPaymentAttemptRepository, PaymentAttemptRepository>();
        services.AddScoped<IPaymentSessionRepository, PaymentSessionRepository>();
        services.AddScoped<IPaymentEventRepository, PaymentEventRepository>();
        services.AddScoped<IPaymentEngineUnitOfWork, PaymentEngineUnitOfWork>();

        return services;
    }
}
