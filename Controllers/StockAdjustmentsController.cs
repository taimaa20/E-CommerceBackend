using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    /// <summary>
    /// Manual raw-material stock adjustments. SuperAdmin only — Admin, Manager and
    /// Cashier are intentionally excluded. The stock change is applied through the
    /// batch/FIFO engine and every adjustment is written to an audit trail.
    /// </summary>
    [ApiController]
    [Route("api/rawmaterials/{materialId:guid}/stock-adjustments")]
    public sealed class StockAdjustmentsController : ControllerBase
    {
        private readonly IStockAdjustmentService _service;

        public StockAdjustmentsController(IStockAdjustmentService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpPost]
        [Authorize(Roles = AppRoleNames.SuperAdmin)]
        public async Task<IActionResult> Adjust(
            Guid materialId,
            [FromBody] StockAdjustmentCreateDto dto,
            CancellationToken ct)
        {
            var result = await _service.AdjustAsync(materialId, dto, ct);
            return Ok(result);
        }

        [HttpPost("~/api/rawmaterials/{materialId:guid}/unit-cost-adjustments")]
        [Authorize(Roles = AppRoleNames.SuperAdmin)]
        public async Task<IActionResult> AdjustUnitCost(
            Guid materialId,
            [FromBody] UnitCostAdjustmentCreateDto dto,
            CancellationToken ct)
        {
            var result = await _service.AdjustUnitCostAsync(materialId, dto, ct);
            return Ok(result);
        }

        [HttpGet]
        [Authorize(Roles = AppRoleNames.SuperAdmin)]
        public async Task<IActionResult> GetHistory(Guid materialId, CancellationToken ct)
        {
            var result = await _service.GetHistoryAsync(materialId, ct);
            return Ok(result);
        }

        [HttpGet("~/api/stock-adjustments")]
        [Authorize(Roles = AppRoleNames.SuperAdmin)]
        public async Task<IActionResult> GetBranchHistory(CancellationToken ct)
        {
            var result = await _service.GetBranchHistoryAsync(ct);
            return Ok(result);
        }
    }
}
