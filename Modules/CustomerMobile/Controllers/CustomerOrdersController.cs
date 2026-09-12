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
    public class CustomerOrdersController : ControllerBase
    {
        private readonly ICustomerOrderService _orderService;
        private readonly ICustomerCheckoutService _checkoutService;
        private readonly ICustomerOrderTrackingService _trackingService;
        private readonly ICustomerReorderService _reorderService;

        public CustomerOrdersController(
            ICustomerOrderService orderService,
            ICustomerCheckoutService checkoutService,
            ICustomerOrderTrackingService trackingService,
            ICustomerReorderService reorderService)
        {
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _checkoutService = checkoutService ?? throw new ArgumentNullException(nameof(checkoutService));
            _trackingService = trackingService ?? throw new ArgumentNullException(nameof(trackingService));
            _reorderService = reorderService ?? throw new ArgumentNullException(nameof(reorderService));
        }

        [HttpPost("calculate")]
        public async Task<IActionResult> Calculate(CustomerOrderCalculateRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _checkoutService.CalculateAsync(request, ct));
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder(CustomerOrderCreateRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _orderService.CreateOrderAsync(request, ct));
        }

        [HttpGet]
        public async Task<IActionResult> GetOrders(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
            => Ok(await _orderService.GetOrdersAsync(page, pageSize, ct));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetOrder(Guid id, CancellationToken ct)
            => Ok(await _orderService.GetOrderAsync(id, ct));

        [HttpGet("{id:guid}/tracking")]
        public async Task<IActionResult> GetTracking(Guid id, CancellationToken ct)
            => Ok(await _trackingService.GetTrackingAsync(id, ct));

        [HttpPost("{id:guid}/reorder")]
        public async Task<IActionResult> Reorder(Guid id, CancellationToken ct)
            => Ok(await _reorderService.ReorderAsync(id, ct));
    }
}
