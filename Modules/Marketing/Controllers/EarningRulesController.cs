using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Controllers
{
    [ApiController]
    [Route("api/v1/marketing/earning-rules")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public sealed class EarningRulesController : ControllerBase
    {
        private readonly IEarningRuleService _service;

        public EarningRulesController(IEarningRuleService service)
            => _service = service ?? throw new ArgumentNullException(nameof(service));

        [HttpGet]
        public async Task<ActionResult<List<EarningRuleDto>>> GetCurrent([FromQuery] bool includeInactive, CancellationToken ct)
            => Ok(await _service.GetCurrentAsync(includeInactive, ct));

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<EarningRuleDto>> GetById(Guid id, CancellationToken ct)
            => Ok(await _service.GetByIdAsync(id, ct));

        [HttpGet("groups/{ruleGroupId:guid}/versions")]
        public async Task<ActionResult<List<EarningRuleDto>>> GetVersions(Guid ruleGroupId, CancellationToken ct)
            => Ok(await _service.GetVersionsAsync(ruleGroupId, ct));

        [HttpPost]
        public async Task<ActionResult<EarningRuleDto>> Create([FromBody] EarningRuleCreateDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{ruleGroupId:guid}")]
        public async Task<ActionResult<EarningRuleDto>> Update(Guid ruleGroupId, [FromBody] EarningRuleCreateDto dto, CancellationToken ct)
            => Ok(await _service.UpdateAsync(ruleGroupId, dto, ct));

        [HttpDelete("{ruleGroupId:guid}")]
        public async Task<IActionResult> Deactivate(Guid ruleGroupId, CancellationToken ct)
        {
            await _service.DeactivateAsync(ruleGroupId, ct);
            return Ok(new { message = "Earning rule deactivated." });
        }
    }
}
