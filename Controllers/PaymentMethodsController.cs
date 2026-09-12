using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/payment-methods")]
    public class PaymentMethodsController : ControllerBase
    {
        private readonly IPaymentMethodService _service;

        public PaymentMethodsController(IPaymentMethodService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet("active")]
        [Authorize(Roles = AppRoleGroups.TakeawayCheckoutOperators)]
        public async Task<IActionResult> GetActive(CancellationToken ct)
        {
            var methods = await _service.GetActiveAsync(ct);
            return Ok(methods);
        }

        [HttpGet]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var methods = await _service.GetAllAsync(ct);
            return Ok(methods);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var method = await _service.GetByIdAsync(id, ct);
            return Ok(method);
        }

        [HttpPost]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> Create([FromBody] PaymentMethodCreateDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> Update(Guid id, [FromBody] PaymentMethodUpdateDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

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
