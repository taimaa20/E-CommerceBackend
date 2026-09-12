using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    [ApiController]
    [Route("api/mobile/loyalty")]
    [Authorize(Roles = AppRoleNames.Customer)]
    [EnableRateLimiting("api")]
    public class CustomerLoyaltyController : ControllerBase
    {
        private readonly ICustomerLoyaltyService _loyaltyService;

        public CustomerLoyaltyController(ICustomerLoyaltyService loyaltyService)
        {
            _loyaltyService = loyaltyService ?? throw new ArgumentNullException(nameof(loyaltyService));
        }

        [HttpGet]
        public async Task<IActionResult> GetSummary(CancellationToken ct)
            => Ok(await _loyaltyService.GetSummaryAsync(ct));

        [HttpPost("calculate")]
        public async Task<IActionResult> Calculate(CustomerLoyaltyCalculateRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _loyaltyService.CalculateAsync(request, ct));
        }

        /// <summary>Authenticated customer's own wallet snapshot.</summary>
        [HttpGet("wallet")]
        public async Task<IActionResult> GetWallet(CancellationToken ct)
            => Ok(await _loyaltyService.GetWalletAsync(ct));

        /// <summary>Authenticated customer's own paginated wallet ledger.</summary>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
            => Ok(await _loyaltyService.GetTransactionsAsync(page, pageSize, ct));

        /// <summary>Active reward catalog (read-only) with affordability for the authenticated customer.</summary>
        [HttpGet("rewards")]
        public async Task<IActionResult> GetRewards(CancellationToken ct)
            => Ok(await _loyaltyService.GetRewardsAsync(ct));

        /// <summary>Authenticated customer's own reward redemption history.</summary>
        [HttpGet("rewards/redemptions")]
        public async Task<IActionResult> GetRewardRedemptions(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
            => Ok(await _loyaltyService.GetRewardRedemptionsAsync(page, pageSize, ct));
    }
}
