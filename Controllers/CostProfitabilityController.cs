using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/cost-profitability")]
    public class CostProfitabilityController : ControllerBase
    {
        private readonly ICostProfitabilityService _service;

        public CostProfitabilityController(ICostProfitabilityService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = AppRoleGroups.ProfitabilityViewers)]
        public async Task<IActionResult> GetDashboard(
            [FromQuery] CostProfitabilityFilterDto filter,
            CancellationToken ct)
        {
            return Ok(await _service.GetDashboardAsync(filter, ct));
        }

        [HttpGet("orders/{orderId:guid}")]
        [Authorize(Roles = AppRoleGroups.CostBreakdownViewers)]
        public async Task<IActionResult> GetOrderSnapshot(Guid orderId, CancellationToken ct)
        {
            return Ok(await _service.GetOrderSnapshotAsync(orderId, ct));
        }

        [HttpPost("orders/{orderId:guid}/snapshot")]
        [Authorize(Roles = AppRoleGroups.CostSharingManagers)]
        public async Task<IActionResult> CaptureOrderSnapshot(Guid orderId, CancellationToken ct)
        {
            var snapshot = await _service.CaptureSnapshotAsync(
                orderId,
                ProfitabilitySnapshotSource.ManualRefresh,
                ct);

            return snapshot == null
                ? NotFound()
                : Ok(snapshot);
        }

        [HttpGet("providers")]
        [Authorize(Roles = AppRoleGroups.CostSharingViewers)]
        public async Task<IActionResult> GetProviders(
            [FromQuery] CostProviderType? type,
            [FromQuery] bool includeInactive = false,
            CancellationToken ct = default)
        {
            return Ok(await _service.GetProvidersAsync(type, includeInactive, ct));
        }

        [HttpGet("providers/{id:guid}")]
        [Authorize(Roles = AppRoleGroups.CostSharingViewers)]
        public async Task<IActionResult> GetProvider(Guid id, CancellationToken ct)
        {
            return Ok(await _service.GetProviderByIdAsync(id, ct));
        }

        [HttpPost("providers")]
        [Authorize(Roles = AppRoleGroups.CostSharingManagers)]
        public async Task<IActionResult> CreateProvider(
            [FromBody] CostSharingProviderUpsertDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateProviderAsync(dto, ct);
            return CreatedAtAction(nameof(GetProvider), new { id = created.Id }, created);
        }

        [HttpPut("providers/{id:guid}")]
        [Authorize(Roles = AppRoleGroups.CostSharingManagers)]
        public async Task<IActionResult> UpdateProvider(
            Guid id,
            [FromBody] CostSharingProviderUpsertDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _service.UpdateProviderAsync(id, dto, ct));
        }

        [HttpDelete("providers/{id:guid}")]
        [Authorize(Roles = AppRoleGroups.CostSharingManagers)]
        public async Task<IActionResult> DeleteProvider(Guid id, CancellationToken ct)
        {
            await _service.DeleteProviderAsync(id, ct);
            return NoContent();
        }

        [HttpGet("rules")]
        [Authorize(Roles = AppRoleGroups.CostSharingViewers)]
        public async Task<IActionResult> GetRules(
            [FromQuery] CostProviderType? providerType,
            [FromQuery] bool includeInactive = false,
            CancellationToken ct = default)
        {
            return Ok(await _service.GetRulesAsync(providerType, includeInactive, ct));
        }

        [HttpGet("rules/{id:guid}")]
        [Authorize(Roles = AppRoleGroups.CostSharingViewers)]
        public async Task<IActionResult> GetRule(Guid id, CancellationToken ct)
        {
            return Ok(await _service.GetRuleByIdAsync(id, ct));
        }

        [HttpPost("rules")]
        [Authorize(Roles = AppRoleGroups.CostSharingManagers)]
        public async Task<IActionResult> CreateRule(
            [FromBody] CostSharingRuleUpsertDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateRuleAsync(dto, ct);
            return CreatedAtAction(nameof(GetRule), new { id = created.Id }, created);
        }

        [HttpPut("rules/{id:guid}")]
        [Authorize(Roles = AppRoleGroups.CostSharingManagers)]
        public async Task<IActionResult> UpdateRule(
            Guid id,
            [FromBody] CostSharingRuleUpsertDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _service.UpdateRuleAsync(id, dto, ct));
        }

        [HttpDelete("rules/{id:guid}")]
        [Authorize(Roles = AppRoleGroups.CostSharingManagers)]
        public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
        {
            await _service.DeleteRuleAsync(id, ct);
            return NoContent();
        }
    }
}
