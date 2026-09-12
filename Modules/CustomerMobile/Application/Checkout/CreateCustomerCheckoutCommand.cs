using MediatR;
using RestaurantPos.Api.Modules.Payments.Application.CreatePayment;

namespace RestaurantPos.Api.Modules.CustomerMobile.Application.Checkout;

public sealed record CreateCustomerCheckoutCommand(
    Guid TenantId,
    Guid CustomerId,
    Guid OrderId,
    string Action,
    string Language,
    string CorrelationId) : IRequest<CreatePaymentResult>;
