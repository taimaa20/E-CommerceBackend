using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    [ApiController]
    [Route("api/mobile/profile")]
    [Authorize(Roles = AppRoleNames.Customer)]
    [EnableRateLimiting("api")]
    public class CustomerProfileController : ControllerBase
    {
        private readonly ICustomerProfileService _profileService;

        public CustomerProfileController(ICustomerProfileService profileService)
        {
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        }

        [HttpGet]
        public async Task<IActionResult> GetProfile(CancellationToken ct)
            => Ok(await _profileService.GetProfileAsync(ct));

        [HttpPut]
        public async Task<IActionResult> UpdateProfile(CustomerProfileUpdateRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _profileService.UpdateProfileAsync(request, ct));
        }

        [HttpGet("addresses")]
        public async Task<IActionResult> GetAddresses(CancellationToken ct)
            => Ok(await _profileService.GetAddressesAsync(ct));

        [HttpPost("address")]
        public async Task<IActionResult> AddAddress(CustomerAddressRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _profileService.AddAddressAsync(request, ct));
        }

        [HttpPut("address/{id:guid}")]
        public async Task<IActionResult> UpdateAddress(Guid id, CustomerAddressRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _profileService.UpdateAddressAsync(id, request, ct));
        }

        [HttpDelete("address/{id:guid}")]
        public async Task<IActionResult> DeleteAddress(Guid id, CancellationToken ct)
        {
            await _profileService.DeleteAddressAsync(id, ct);
            return NoContent();
        }

        [HttpDelete("account")]
        public async Task<IActionResult> DeleteAccount(CancellationToken ct)
            => Ok(await _profileService.DeleteAccountAsync(ct));
    }
}
