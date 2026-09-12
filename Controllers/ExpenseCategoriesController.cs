using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/expense-categories")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public sealed class ExpenseCategoriesController : ControllerBase
    {
        private readonly IExpenseCategoryService _service;

        public ExpenseCategoriesController(IExpenseCategoryService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        // GET: api/expense-categories?activeOnly=true
        [HttpGet]
        public async Task<ActionResult<List<ExpenseCategoryDto>>> List(
            [FromQuery] bool activeOnly = false,
            CancellationToken ct = default)
        {
            var list = await _service.ListAsync(GeneralHelper.IsArabicRequested(Request), activeOnly, ct);
            return Ok(list);
        }

        [HttpPost]
        public async Task<ActionResult<ExpenseCategoryDto>> Create([FromBody] CreateExpenseCategoryDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, GeneralHelper.IsArabicRequested(Request), ct);
            return Ok(created);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ExpenseCategoryDto>> Update(Guid id, [FromBody] UpdateExpenseCategoryDto dto, CancellationToken ct)
        {
            var updated = await _service.UpdateAsync(id, dto, GeneralHelper.IsArabicRequested(Request), ct);
            return Ok(updated);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _service.SoftDeleteAsync(id, ct);
            return NoContent();
        }
    }
}
