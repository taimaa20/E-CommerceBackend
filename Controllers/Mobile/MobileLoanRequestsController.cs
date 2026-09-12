using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs.MobileRequests;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers.Mobile
{
    [ApiController]
    [Route("api/mobile/loan-requests")]

    [Authorize]
    [EnableRateLimiting("api")]
    public class MobileLoanRequestsController : ControllerBase
    {
        private readonly IMobileLoanRequestService _service;
        private readonly ICurrentUserResolver _currentUserResolver;

        public MobileLoanRequestsController(
            IMobileLoanRequestService service,
            ICurrentUserResolver currentUserResolver)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _currentUserResolver = currentUserResolver ?? throw new ArgumentNullException(nameof(currentUserResolver));
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] MobileRequestListQuery query, CancellationToken ct)
        {
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue) return Unauthorized(new { message = "جلسة غير صالحة / Invalid session" });

            try
            {
                var data = await _service.GetAsync(userId.Value, query, ct);
                return Ok(new MobileDataResponse<IReadOnlyList<MobileLoanRequestDto>>(data));
            }
            catch (MobileRequestValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MobileLoanRequestCreateDto? request, CancellationToken ct)
        {
            if (request == null) return BadRequest(new { message = "Request body is required." });
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue) return Unauthorized(new { message = "جلسة غير صالحة / Invalid session" });

            try
            {
                var data = await _service.CreateAsync(userId.Value, request, ct);
                return Ok(new MobileDataResponse<MobileLoanRequestDto>(data));
            }
            catch (MobileRequestValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("meta")]
        public async Task<IActionResult> Meta(CancellationToken ct)
        {
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue) return Unauthorized(new { message = "جلسة غير صالحة / Invalid session" });

            try
            {
                var data = await _service.GetMetaAsync(userId.Value, ct);
                return Ok(new MobileDataResponse<MobileLoanRequestMetaDto>(data));
            }
            catch (MobileRequestValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private Task<Guid?> GetCurrentUserIdAsync()
            => _currentUserResolver.ResolveUserIdAsync(User);
    }
}
