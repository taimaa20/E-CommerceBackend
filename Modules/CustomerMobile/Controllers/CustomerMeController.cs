using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    /// <summary>Customer self-service endpoints scoped to the phone-OTP flow.
    /// CustomerId is always taken from the JWT — never from the request body.</summary>
    [ApiController]
    [Route("api/mobile/customer")]
    [Authorize(Roles = AppRoleNames.Customer)]
    [EnableRateLimiting("api")]
    public class CustomerMeController : ControllerBase
    {
        private readonly ICustomerPhoneAuthService _phoneAuthService;

        public CustomerMeController(ICustomerPhoneAuthService phoneAuthService)
        {
            _phoneAuthService = phoneAuthService ?? throw new ArgumentNullException(nameof(phoneAuthService));
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken ct)
            => Ok(await _phoneAuthService.GetCurrentAsync(ct));

        /// <summary>First-time profile completion or subsequent profile updates.
        /// Setting IsProfileCompleted = true on success.</summary>
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(CustomerCompleteProfileRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _phoneAuthService.CompleteProfileAsync(request, ct));
        }
    }
}
