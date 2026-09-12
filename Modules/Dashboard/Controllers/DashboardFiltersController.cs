using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Dashboard.DTOs;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Modules.Dashboard.Services;

namespace RestaurantPos.Api.Modules.Dashboard.Controllers
{
    /// <summary>
    /// Returns the option lists used to populate the dashboard filter bar
    /// (cashiers, waiters, categories, payment methods). Authenticated only —
    /// the same as the cheapest dashboard, since this is a discovery endpoint.
    /// </summary>
    [ApiController]
    [Route("api/dashboards/filters")]
    [Authorize]
    [DashboardScope(DashboardScope.Operations, DefaultPreset = DashboardFilterPreset.Today)]
    public sealed class DashboardFiltersController : ControllerBase
    {
        private readonly IDashboardMetricsService _metrics;

        public DashboardFiltersController(IDashboardMetricsService metrics)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        [HttpGet("options")]
        public async Task<ActionResult<DashboardFilterOptionsDto>> Options(
            [FromQuery] DashboardFilterDto filter,
            CancellationToken ct)
        {
            _ = filter;
            return Ok(await _metrics.GetFilterOptionsAsync(ct));
        }
    }
}
