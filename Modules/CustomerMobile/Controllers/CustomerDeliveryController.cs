using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    [ApiController]
    [Route("api/mobile/delivery")]
    [Authorize(Roles = AppRoleNames.Customer)]
    [EnableRateLimiting("api")]
    public class CustomerDeliveryController : ControllerBase
    {
        private readonly ICustomerDeliveryService _deliveryService;

        public CustomerDeliveryController(ICustomerDeliveryService deliveryService)
        {
            _deliveryService = deliveryService ?? throw new ArgumentNullException(nameof(deliveryService));
        }

        [HttpPost("check-zone")]
        public async Task<IActionResult> CheckZone(CustomerDeliveryCheckRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _deliveryService.CheckZoneAsync(request, ct));
        }
    }
}
