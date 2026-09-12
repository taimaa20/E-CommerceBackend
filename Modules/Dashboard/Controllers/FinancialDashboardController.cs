using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Financial;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Modules.Dashboard.Services;
using RestaurantPos.Api.Modules.Dashboard.Services.Financial;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Controllers
{
    [ApiController]
    [Route("api/dashboards/financial")]
    [Authorize(Roles = AppRoleGroups.FinancialDashboardViewers)]
    [DashboardScope(DashboardScope.Financial, DefaultPreset = DashboardFilterPreset.ThisMonth)]
    public sealed class FinancialDashboardController : ControllerBase
    {
        private readonly IDashboardMetricsService _metrics;
        private readonly IFinancialDashboardExportService _exportService;

        public FinancialDashboardController(
            IDashboardMetricsService metrics,
            IFinancialDashboardExportService exportService)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        }

        [HttpGet]
        public async Task<ActionResult<FinancialSnapshotDto>> Get(
            [FromQuery] DashboardFilterDto filter,
            CancellationToken ct)
        {
            _ = filter;
            return Ok(await _metrics.GetFinancialAsync(ct));
        }

        [HttpGet("commission-analysis")]
        public async Task<ActionResult<CommissionAnalysisDto>> GetCommissionAnalysis(
            [FromQuery] DashboardFilterDto filter,
            CancellationToken ct)
        {
            _ = filter;
            return Ok(await _metrics.GetFinancialCommissionAnalysisAsync(ct));
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] DashboardFilterDto filter,
            [FromQuery] string? currency,
            CancellationToken ct)
        {
            _ = filter;
            var snapshot = await _metrics.GetFinancialAsync(ct);
            var commission = await _metrics.GetFinancialCommissionAnalysisAsync(ct);
            var export = _exportService.BuildWorkbook(snapshot, currency, commission);
            return File(export.Content, export.ContentType, export.FileName);
        }
    }
}
