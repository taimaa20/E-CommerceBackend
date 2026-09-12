using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    [ApiController]
    [Route("api/mobile/payment-methods")]
    [Authorize(Roles = AppRoleNames.Customer)]
    [EnableRateLimiting("api")]
    public class CustomerPaymentMethodsController : ControllerBase
    {
        public const string MobileBranchHeaderName = "X-Mobile-Branch-ID";

        private readonly ICustomerCheckoutService _checkoutService;

        public CustomerPaymentMethodsController(ICustomerCheckoutService checkoutService)
        {
            _checkoutService = checkoutService ?? throw new ArgumentNullException(nameof(checkoutService));
        }

        [HttpGet]
        public async Task<IActionResult> GetActive(
            [FromHeader(Name = MobileBranchHeaderName)] Guid branchId,
            CancellationToken ct)
            => Ok(await _checkoutService.GetPaymentMethodsAsync(branchId, ct));
    }
}
