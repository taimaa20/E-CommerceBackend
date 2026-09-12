using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    [ApiController]
    [Route("api/mobile/notifications")]
    [Authorize(Roles = AppRoleNames.Customer)]
    [EnableRateLimiting("api")]
    public class CustomerNotificationsController : ControllerBase
    {
        private readonly ICustomerDeviceService _deviceService;
        private readonly ICustomerNotificationService _notificationService;

        public CustomerNotificationsController(
            ICustomerDeviceService deviceService,
            ICustomerNotificationService notificationService)
        {
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
            => Ok(await _notificationService.GetNotificationsAsync(page, pageSize, ct));

        [HttpPost("register")]
        public async Task<IActionResult> Register(CustomerDeviceRegisterRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _deviceService.RegisterAsync(request, ct));
        }

        [HttpPut("{id:guid}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
            => Ok(await _notificationService.MarkAsReadAsync(id, ct));
    }
}
