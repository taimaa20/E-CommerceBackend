using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Filters;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ServiceFilter(typeof(DeliveryPartnersFeatureFilter))]
    [Authorize(Roles = AppRoleGroups.CashierOperators)]
    public class DeliveryPartnersController : ControllerBase
    {
        private readonly IDeliveryPartnerService _service;

        public DeliveryPartnersController(IDeliveryPartnerService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActive(CancellationToken ct)
        {
            var partners = await _service.GetActiveAsync(ct);
            return Ok(partners);
        }

        [HttpGet]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetPaged(
            [FromQuery] string? search,
            [FromQuery] DeliveryPartnerStatus? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetPagedAsync(search, status, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var partner = await _service.GetByIdAsync(id, ct);
            return Ok(partner);
        }

        [HttpPost]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> Create([FromBody] DeliveryPartnerUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> Update(Guid id, [FromBody] DeliveryPartnerUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

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

        [HttpGet("{id:guid}/products")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetPartnerProducts(
            Guid id,
            [FromQuery] string? search,
            [FromQuery] bool? enabledOnly,
            [FromQuery] Guid? categoryId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var result = await _service.GetPartnerProductsAsync(id, search, enabledOnly, categoryId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("products/{productId:guid}/mappings")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetProductMappings(
            Guid productId,
            [FromQuery] bool activePartnersOnly = true,
            CancellationToken ct = default)
        {
            var mappings = await _service.GetProductMappingsAsync(productId, activePartnersOnly, ct);
            return Ok(mappings);
        }

        [HttpPut("products/{productId:guid}/mappings/{partnerId:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> UpsertProductMapping(
            Guid productId,
            Guid partnerId,
            [FromBody] DeliveryPartnerProductMappingUpsertDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var updated = await _service.UpsertProductMappingAsync(productId, partnerId, dto, ct);
            return Ok(updated);
        }
    }
}
