using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Operations;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Modules.Dashboard.Services;
using RestaurantPos.Api.Modules.Dashboard.Services.Operational;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Controllers
{
    [ApiController]
    [Route("api/dashboards/operations")]
    [Authorize(Roles = AppRoleGroups.OperationsDashboardViewers)]
    [DashboardScope(DashboardScope.Operations, DefaultPreset = DashboardFilterPreset.Today)]
    public sealed class OperationsDashboardController : ControllerBase
    {
        private readonly IDashboardMetricsService _metrics;
        private readonly IOperationsDashboardExportService _export;

        public OperationsDashboardController(
            IDashboardMetricsService metrics,
            IOperationsDashboardExportService export)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _export  = export  ?? throw new ArgumentNullException(nameof(export));
        }

        [HttpGet]
        public async Task<ActionResult<OperationsSnapshotDto>> Get(
            [FromQuery] DashboardFilterDto filter,
            CancellationToken ct)
        {
            _ = filter; // bound for the action filter; service reads from IDashboardFilterContext
            return Ok(await _metrics.GetOperationsAsync(ct));
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] DashboardFilterDto filter,
            [FromQuery] string? currency,
            CancellationToken ct)
        {
            _ = filter;
            var snapshot = await _metrics.GetOperationsAsync(ct);
            var file = _export.BuildWorkbook(snapshot, currency);
            return File(file.Content, file.ContentType, file.FileName);
        }
    }
}
