using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    [ApiController]
    [Route("api/mobile/cart")]
    [Authorize(Roles = AppRoleNames.Customer)]
    [EnableRateLimiting("api")]
    public class CustomerCartController : ControllerBase
    {
        private readonly ICustomerCartService _cartService;

        public CustomerCartController(ICustomerCartService cartService)
        {
            _cartService = cartService ?? throw new ArgumentNullException(nameof(cartService));
        }

        [HttpGet]
        public async Task<IActionResult> GetCart(CancellationToken ct)
            => Ok(await _cartService.GetCartAsync(ct));

        [HttpPost("items")]
        public async Task<IActionResult> AddItem(CustomerCartItemRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _cartService.AddItemAsync(request, ct));
        }

        [HttpPut("items/{id:guid}")]
        public async Task<IActionResult> UpdateItem(Guid id, CustomerCartItemUpdateRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _cartService.UpdateItemAsync(id, request, ct));
        }

        [HttpDelete("items/{id:guid}")]
        public async Task<IActionResult> RemoveItem(Guid id, CancellationToken ct)
            => Ok(await _cartService.RemoveItemAsync(id, ct));

        [HttpDelete("clear")]
        public async Task<IActionResult> Clear(CancellationToken ct)
        {
            await _cartService.ClearAsync(ct);
            return NoContent();
        }
    }
}
