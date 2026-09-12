using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    [ApiController]
    [Route("api/mobile/orders")]
    [Authorize(Roles = AppRoleNames.Customer)]
    [EnableRateLimiting("api")]
    public class CustomerOrderCancellationController : ControllerBase
    {
        private readonly ICustomerOrderCancellationService _cancellationService;

        public CustomerOrderCancellationController(ICustomerOrderCancellationService cancellationService)
        {
            _cancellationService = cancellationService ?? throw new ArgumentNullException(nameof(cancellationService));
        }

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id, CustomerCancelOrderRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _cancellationService.CancelAsync(id, request, ct));
        }
    }
}
