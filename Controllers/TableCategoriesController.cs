using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TableCategoriesController : ControllerBase
    {
        private readonly ITableCategoryService _service;

        public TableCategoriesController(ITableCategoryService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        // GET: api/TableCategories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TableCategoryDto>>> GetTableCategories(CancellationToken cancellationToken)
        {
            var categories = await _service.GetAllAsync(IsArabicRequested(Request), cancellationToken);
            return Ok(categories);
        }

        // POST: api/TableCategories
        [HttpPost]
        [Authorize(Roles = AppRoleNames.Admin)]
        public async Task<ActionResult<TableCategoryDto>> CreateTableCategory(
            [FromBody] CreateTableCategoryRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Category name is required.");

            var outcome = await _service.CreateAsync(request, cancellationToken);
            if (outcome.DuplicateName)
                return Conflict(new { message = "A category with this name already exists." });

            return CreatedAtAction(nameof(GetTableCategories), new { id = outcome.Dto!.Id }, outcome.Dto);
        }

        // DELETE: api/TableCategories/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoleNames.Admin)]
        public async Task<IActionResult> DeleteTableCategory(Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _service.DeleteAsync(id, cancellationToken);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        // PUT: api/TableCategories/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = AppRoleNames.Admin)]
        public async Task<ActionResult<TableCategoryDto>> UpdateTableCategory(
            Guid id,
            [FromBody] CreateTableCategoryRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Category name is required.");

            var outcome = await _service.UpdateAsync(id, request, IsArabicRequested(Request), cancellationToken);

            if (outcome.NotFound)
                return NotFound();

            if (outcome.DuplicateName)
                return Conflict(new { message = "A category with this name already exists." });

            return Ok(outcome.Dto!);
        }

        private static bool IsArabicRequested(HttpRequest request)
        {
            var language = request.Headers["Accept-Language"].ToString();
            return language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        }
    }
}
