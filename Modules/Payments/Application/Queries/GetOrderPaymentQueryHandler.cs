using MediatR;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Modules.Payments.Api;
using RestaurantPos.Api.Modules.Payments.Interfaces;

namespace RestaurantPos.Api.Modules.Payments.Application.Queries;

public sealed class GetOrderPaymentQueryHandler : IRequestHandler<GetOrderPaymentQuery, PaymentEngineOrderPaymentDto>
{
    private readonly IPaymentEngineRepository _payments;

    public GetOrderPaymentQueryHandler(IPaymentEngineRepository payments)
    {
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
    }

    public async Task<PaymentEngineOrderPaymentDto> Handle(GetOrderPaymentQuery request, CancellationToken cancellationToken)
    {
        var payments = await _payments.GetByOrderIdAsync(request.TenantId, request.OrderId, cancellationToken);
        var visiblePayments = payments
            .Where(payment => payment.BranchId == request.BranchId)
            .OrderByDescending(payment => payment.CreatedAt)
            .Select(PaymentEngineQueryMapper.Map)
            .ToArray();

        if (visiblePayments.Length == 0)
        {
            throw new NotFoundException("Payment for order", request.OrderId);
        }

        return new PaymentEngineOrderPaymentDto
        {
            OrderId = request.OrderId,
            Payments = visiblePayments,
            CurrentPayment = visiblePayments.FirstOrDefault()
        };
    }
}
