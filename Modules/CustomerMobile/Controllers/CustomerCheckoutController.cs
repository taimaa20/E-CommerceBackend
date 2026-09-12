using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.Application.Checkout;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Services;
using RestaurantPos.Api.Modules.Payments.Application.CreatePayment;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers;

[ApiController]
[Route("api/mobile/checkout")]
[Authorize(Roles = AppRoleNames.Customer)]
[EnableRateLimiting("api")]
public sealed class CustomerCheckoutController : ControllerBase
{
    private readonly ICustomerMobileContext _mobileContext;
    private readonly ISender _sender;

    public CustomerCheckoutController(ICustomerMobileContext mobileContext, ISender sender)
    {
        _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    [HttpPost("create")]
    [ProducesResponseType(typeof(CreatePaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatePaymentResult>> Create(
        CustomerCheckoutCreateRequest request,
        CancellationToken ct)
    {
        var command = new CreateCustomerCheckoutCommand(
            _mobileContext.GetTenantId(),
            _mobileContext.GetCustomerId(),
            request.OrderId,
            request.Action,
            request.Lang,
            HttpContext.TraceIdentifier);

        return Ok(await _sender.Send(command, ct));
    }
}
