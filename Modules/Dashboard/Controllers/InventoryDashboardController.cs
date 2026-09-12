using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Inventory;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Modules.Dashboard.Services;
using RestaurantPos.Api.Modules.Dashboard.Services.Inventory;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Controllers
{
    [ApiController]
    [Route("api/dashboards/inventory")]
    [Authorize(Roles = AppRoleGroups.WasteLogViewers)]
    [DashboardScope(DashboardScope.Inventory, DefaultPreset = DashboardFilterPreset.ThisWeek)]
    public sealed class InventoryDashboardController : ControllerBase
    {
        private readonly IDashboardMetricsService _metrics;
        private readonly IInventoryDashboardExportService _export;

        public InventoryDashboardController(
            IDashboardMetricsService metrics,
            IInventoryDashboardExportService export)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _export  = export  ?? throw new ArgumentNullException(nameof(export));
        }

        [HttpGet]
        public async Task<ActionResult<InventorySnapshotDto>> Get(
            [FromQuery] DashboardFilterDto filter,
            CancellationToken ct)
        {
            _ = filter;
            return Ok(await _metrics.GetInventoryAsync(ct));
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] DashboardFilterDto filter,
            [FromQuery] string? currency,
            CancellationToken ct)
        {
            _ = filter;
            var snapshot = await _metrics.GetInventoryAsync(ct);
            var file = _export.BuildWorkbook(snapshot, currency);
            return File(file.Content, file.ContentType, file.FileName);
        }
    }
}
