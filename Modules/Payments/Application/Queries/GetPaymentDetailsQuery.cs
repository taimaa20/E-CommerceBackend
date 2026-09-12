using MediatR;
using RestaurantPos.Api.Modules.Payments.Api;

namespace RestaurantPos.Api.Modules.Payments.Application.Queries;

public sealed record GetPaymentDetailsQuery(
    Guid TenantId,
    Guid BranchId,
    Guid PaymentId) : IRequest<PaymentEnginePaymentDetailsDto>;
