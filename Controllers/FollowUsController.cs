using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FollowUsController : ControllerBase
    {
        private readonly IFollowUsClickService _clickService;

        public FollowUsController(IFollowUsClickService clickService)
        {
            _clickService = clickService ?? throw new ArgumentNullException(nameof(clickService));
        }

        [HttpPost("clicks")]
        [AllowAnonymous]
        public async Task<IActionResult> TrackClick([FromBody] FollowUsClickCreateDto dto, CancellationToken ct)
        {
            await _clickService.TrackClickAsync(
                dto,
                Request.Headers.UserAgent.ToString(),
                Request.Headers.Referer.ToString(),
                ct);

            return Accepted(new { message = "Accepted" });
        }
    }
}
