using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Controllers
{
    [ApiController]
    [Route("api/v1/marketing/tiers")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public sealed class TiersController : ControllerBase
    {
        private readonly ITierAdminService _service;

        public TiersController(ITierAdminService service)
            => _service = service ?? throw new ArgumentNullException(nameof(service));

        [HttpGet]
        public async Task<ActionResult<List<LoyaltyTierDto>>> GetAll([FromQuery] bool includeInactive, CancellationToken ct)
            => Ok(await _service.GetAllAsync(includeInactive, ct));

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<LoyaltyTierDto>> GetById(Guid id, CancellationToken ct)
            => Ok(await _service.GetByIdAsync(id, ct));

        [HttpPost]
        public async Task<ActionResult<LoyaltyTierDto>> Create([FromBody] LoyaltyTierUpsertDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<LoyaltyTierDto>> Update(Guid id, [FromBody] LoyaltyTierUpsertDto dto, CancellationToken ct)
            => Ok(await _service.UpdateAsync(id, dto, ct));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        {
            await _service.DeactivateAsync(id, ct);
            return Ok(new { message = "Loyalty tier deactivated." });
        }
    }
}
