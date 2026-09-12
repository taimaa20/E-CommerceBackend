using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    [ApiController]
    [Route("api/mobile/settings")]
    [Authorize(Roles = AppRoleNames.Customer)]
    [EnableRateLimiting("api")]
    public class CustomerSettingsController : ControllerBase
    {
        private readonly ICustomerMobileSettingsService _settingsService;

        public CustomerSettingsController(ICustomerMobileSettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        [HttpGet("follow-us")]
        public async Task<IActionResult> GetFollowUs(CancellationToken ct)
            => Ok(await _settingsService.GetFollowUsAsync(ct));

        [HttpGet("contact")]
        public async Task<IActionResult> GetContact(CancellationToken ct)
            => Ok(await _settingsService.GetContactAsync(ct));

        [HttpGet("terms")]
        public async Task<IActionResult> GetTerms(CancellationToken ct)
            => Ok(await _settingsService.GetTermsAsync(ct));

        [HttpGet("privacy-policy")]
        public async Task<IActionResult> GetPrivacyPolicy(CancellationToken ct)
            => Ok(await _settingsService.GetPrivacyPolicyAsync(ct));
    }
}
