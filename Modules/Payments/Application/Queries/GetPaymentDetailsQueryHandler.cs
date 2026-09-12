using MediatR;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Modules.Payments.Api;
using RestaurantPos.Api.Modules.Payments.Interfaces;

namespace RestaurantPos.Api.Modules.Payments.Application.Queries;

public sealed class GetPaymentDetailsQueryHandler : IRequestHandler<GetPaymentDetailsQuery, PaymentEnginePaymentDetailsDto>
{
    private readonly IPaymentEngineRepository _payments;

    public GetPaymentDetailsQueryHandler(IPaymentEngineRepository payments)
    {
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
    }

    public async Task<PaymentEnginePaymentDetailsDto> Handle(GetPaymentDetailsQuery request, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetAggregateAsync(request.PaymentId, cancellationToken);
        if (payment is null ||
            payment.TenantId != request.TenantId ||
            payment.BranchId != request.BranchId)
        {
            throw new NotFoundException("Payment", request.PaymentId);
        }

        return PaymentEngineQueryMapper.Map(payment);
    }
}
