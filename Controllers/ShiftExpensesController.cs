using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    /// <summary>
    /// Expenses / invoices a cashier pays out of an open shift.
    /// Separate from api/expense-invoices (Admin+Manager back-office bills) so
    /// cashier access never widens the back-office surface.
    /// </summary>
    [ApiController]
    [Route("api/shift-expenses")]
    [Authorize(Roles = AppRoleGroups.CashierOperators)]
    [EnableRateLimiting("api")]
    public sealed class ShiftExpensesController : ControllerBase
    {
        private readonly IShiftExpenseService _service;
        private readonly ICurrentUserAccessor _currentUser;

        public ShiftExpensesController(IShiftExpenseService service, ICurrentUserAccessor currentUser)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        // GET: api/shift-expenses/categories
        [HttpGet("categories")]
        public async Task<ActionResult<List<ExpenseCategoryDto>>> GetCategories(CancellationToken ct)
        {
            var categories = await _service.ListCategoriesAsync(GeneralHelper.IsArabicRequested(Request), ct);
            return Ok(categories);
        }

        // GET: api/shift-expenses/current
        [HttpGet("current")]
        public async Task<ActionResult<ShiftExpenseListDto>> GetCurrent(CancellationToken ct)
        {
            var result = await _service.GetCurrentShiftExpensesAsync(
                _currentUser.UserId,
                GeneralHelper.IsArabicRequested(Request),
                ct);
            return Ok(result);
        }

        // GET: api/shift-expenses/shift/{shiftId}
        [HttpGet("shift/{shiftId:guid}")]
        public async Task<ActionResult<ShiftExpenseListDto>> GetForShift(Guid shiftId, CancellationToken ct)
        {
            var result = await _service.GetShiftExpensesAsync(
                shiftId,
                _currentUser.UserId,
                _currentUser.IsAdminOrManager,
                GeneralHelper.IsArabicRequested(Request),
                ct);
            return Ok(result);
        }

        // POST: api/shift-expenses
        [HttpPost]
        public async Task<ActionResult<ShiftExpenseDto>> Create(
            [FromBody] CreateShiftExpenseDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var created = await _service.CreateForCurrentShiftAsync(
                dto,
                _currentUser.UserId,
                GetCurrentUserName(),
                GeneralHelper.IsArabicRequested(Request),
                ct);
            return Ok(created);
        }

        // POST: api/shift-expenses/{id}/cancel
        [HttpPost("{id:guid}/cancel")]
        public async Task<ActionResult<ShiftExpenseDto>> Cancel(
            Guid id,
            [FromBody] CancelShiftExpenseDto? dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var cancelled = await _service.CancelAsync(
                id,
                dto ?? new CancelShiftExpenseDto(),
                _currentUser.UserId,
                GetCurrentUserName(),
                _currentUser.IsAdminOrManager,
                GeneralHelper.IsArabicRequested(Request),
                ct);
            return Ok(cancelled);
        }

        private string? GetCurrentUserName()
            => User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("name")
                ?? User.Identity?.Name;
    }
}
