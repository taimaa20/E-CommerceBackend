using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/customer-analytics")]
    [Authorize(Roles = AppRoleGroups.CustomerAnalyticsViewers)]
    [EnableRateLimiting("api")]
    public sealed class CustomerAnalyticsController : ControllerBase
    {
        private readonly ICustomerAnalyticsService _service;

        public CustomerAnalyticsController(ICustomerAnalyticsService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<CustomerAnalyticsSnapshotDto>> GetDashboard(
            [FromQuery] CustomerAnalyticsQueryDto query,
            CancellationToken ct)
        {
            var result = await _service.GetDashboardAsync(
                query,
                GeneralHelper.IsArabicRequested(Request),
                ct);
            return Ok(result);
        }
    }
}
