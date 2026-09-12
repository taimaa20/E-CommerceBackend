using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeliveryZonesController : ControllerBase
    {
        private readonly IDeliveryZoneService _service;
        private readonly ILogger<DeliveryZonesController> _logger;

        public DeliveryZonesController(IDeliveryZoneService service, ILogger<DeliveryZonesController> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("active")]
        [Authorize(Roles = AppRoleGroups.TakeawayCheckoutOperators)]
        public async Task<IActionResult> GetActive(CancellationToken ct)
        {
            var zones = await _service.GetActiveForSelectAsync(ct);
            return Ok(zones);
        }

        [HttpGet]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetPaged(
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetPagedAsync(search, isActive, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            return Ok(dto);
        }

        [HttpPost]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> Create([FromBody] DeliveryZoneCreateDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> Update(Guid id, [FromBody] DeliveryZoneUpdateDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updated = await _service.UpdateAsync(id, dto, ct);
            return Ok(updated);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }
    }
}
