using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Audit;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Modules.Dashboard.Services;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Controllers
{
    [ApiController]
    [Route("api/dashboards/audit")]
    [Authorize(Roles = AppRoleGroups.AdminStrict)]
    [DashboardScope(DashboardScope.Audit, DefaultPreset = DashboardFilterPreset.ThisWeek)]
    public sealed class AuditDashboardController : ControllerBase
    {
        private readonly IDashboardMetricsService _metrics;

        public AuditDashboardController(IDashboardMetricsService metrics)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        [HttpGet]
        public async Task<ActionResult<AuditSnapshotDto>> Get(
            [FromQuery] DashboardFilterDto filter,
            CancellationToken ct)
        {
            _ = filter;
            return Ok(await _metrics.GetAuditAsync(ct));
        }
    }
}
