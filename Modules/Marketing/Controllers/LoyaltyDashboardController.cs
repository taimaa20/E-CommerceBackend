using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Controllers
{
    [ApiController]
    [Route("api/v1/marketing/dashboard")]
    [Authorize(Roles = AppRoleGroups.LoyaltyDashboardViewers)]
    public sealed class LoyaltyDashboardController : ControllerBase
    {
        private readonly ILoyaltyDashboardService _service;

        public LoyaltyDashboardController(ILoyaltyDashboardService service)
            => _service = service ?? throw new ArgumentNullException(nameof(service));

        [HttpGet]
        public async Task<ActionResult<LoyaltyDashboardDto>> Get(
            [FromQuery] string? period = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int trendDays = 30,
            [FromQuery] int topN = 10,
            CancellationToken ct = default)
            => Ok(await _service.GetAsync(period, from, to, trendDays, topN, ct));
    }
}
