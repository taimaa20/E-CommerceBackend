using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.Services;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    [ApiController]
    [Route("api/mobile")]
    [EnableRateLimiting("api")]
    public class CustomerHomeController : ControllerBase
    {
        private readonly ICustomerHomeService _homeService;

        public CustomerHomeController(ICustomerHomeService homeService)
        {
            _homeService = homeService ?? throw new ArgumentNullException(nameof(homeService));
        }

        [HttpGet("home")]
        [AllowAnonymous]
        public async Task<IActionResult> GetHome([FromQuery] string language = "en", CancellationToken ct = default)
            => Ok(await _homeService.GetHomeAsync(language, ct));

        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> Search(
            [FromQuery] string keyword,
            [FromQuery] string language = "en",
            CancellationToken ct = default)
            => Ok(await _homeService.SearchAsync(keyword, language, ct));

        [HttpGet("products/{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProduct(
            Guid id,
            [FromQuery] string language = "en",
            CancellationToken ct = default)
            => Ok(await _homeService.GetProductAsync(id, language, ct));
    }
}
