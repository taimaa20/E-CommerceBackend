using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    [ApiController]
    [Route("api/mobile/devices")]
    [Authorize(Roles = AppRoleNames.Customer)]
    [EnableRateLimiting("api")]
    public class CustomerDevicesController : ControllerBase
    {
        private readonly ICustomerDeviceService _deviceService;

        public CustomerDevicesController(ICustomerDeviceService deviceService)
        {
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
        }

        [HttpPost]
        public async Task<IActionResult> Register(CustomerDeviceRegisterRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _deviceService.RegisterAsync(request, ct));
        }
    }
}
