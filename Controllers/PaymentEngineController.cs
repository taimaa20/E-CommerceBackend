using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Payments.Api;
using RestaurantPos.Api.Modules.Payments.Application.Queries;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoleGroups.TakeawayCheckoutOperators)]
[Route("api/v1/payment-engine")]
[Produces("application/json")]
public sealed class PaymentEngineController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITenantResolver _tenantResolver;
    private readonly IBranchContext _branchContext;

    public PaymentEngineController(
        ISender sender,
        ITenantResolver tenantResolver,
        IBranchContext branchContext)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
    }

    [HttpGet("payments/{paymentId:guid}")]
    [ProducesResponseType(typeof(PaymentEnginePaymentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentEnginePaymentDetailsDto>> GetPayment(
        Guid paymentId,
        CancellationToken ct)
    {
        var branch = await _branchContext.GetCurrentAsync(ct);
        var result = await _sender.Send(
            new GetPaymentDetailsQuery(
                _tenantResolver.GetTenantId(),
                branch.CurrentBranch.Id,
                paymentId),
            ct);

        return Ok(result);
    }

    [HttpGet("orders/{orderId:guid}/payment")]
    [ProducesResponseType(typeof(PaymentEngineOrderPaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentEngineOrderPaymentDto>> GetOrderPayment(
        Guid orderId,
        CancellationToken ct)
    {
        var branch = await _branchContext.GetCurrentAsync(ct);
        var result = await _sender.Send(
            new GetOrderPaymentQuery(
                _tenantResolver.GetTenantId(),
                branch.CurrentBranch.Id,
                orderId),
            ct);

        return Ok(result);
    }
}
