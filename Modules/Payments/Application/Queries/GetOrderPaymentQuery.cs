using MediatR;
using RestaurantPos.Api.Modules.Payments.Api;

namespace RestaurantPos.Api.Modules.Payments.Application.Queries;

public sealed record GetOrderPaymentQuery(
    Guid TenantId,
    Guid BranchId,
    Guid OrderId) : IRequest<PaymentEngineOrderPaymentDto>;
