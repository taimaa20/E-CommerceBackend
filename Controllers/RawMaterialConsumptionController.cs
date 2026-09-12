using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/inventory/raw-material-consumption")]
    [Authorize(Roles = AppRoleGroups.WasteLogViewers)]
    public sealed class RawMaterialConsumptionController : ControllerBase
    {
        private readonly IRawMaterialConsumptionService _service;
        private readonly IRawMaterialConsumptionExportService _export;

        public RawMaterialConsumptionController(IRawMaterialConsumptionService service, IRawMaterialConsumptionExportService export)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _export = export ?? throw new ArgumentNullException(nameof(export));
        }

        [HttpGet]
        public async Task<ActionResult<RawMaterialConsumptionReportDto>> Get(
            [FromQuery] RawMaterialConsumptionQueryDto query,
            CancellationToken ct)
            => Ok(await _service.GetAsync(query, ct));

        [HttpGet("export/{format}")]
        public async Task<IActionResult> Export(string format, [FromQuery] RawMaterialConsumptionQueryDto query, CancellationToken ct)
        {
            var (report, details, companyName, logoUrl) = await _service.GetExportAsync(query, ct);
            var generatedBy = User.Identity?.Name ?? "POS User";
            var file = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
                ? _export.BuildCsv(details) : _export.BuildExcel(report, details, query, companyName, logoUrl, generatedBy);
            return File(file.Content, file.ContentType, file.FileName);
        }
    }
}
