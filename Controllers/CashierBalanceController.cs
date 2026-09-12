using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/cashierbalance")]
    [Authorize]
    [EnableRateLimiting("api")]
    public class CashierBalanceController : ControllerBase
    {
        private readonly ICashierShiftService _cashierShiftService;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<CashierBalanceController> _logger;

        public CashierBalanceController(
            ICashierShiftService cashierShiftService,
            ITenantResolver tenantResolver,
            ICurrentUserAccessor currentUser,
            ILogger<CashierBalanceController> logger)
        {
            _cashierShiftService = cashierShiftService ?? throw new ArgumentNullException(nameof(cashierShiftService));
            _tenantResolver      = tenantResolver      ?? throw new ArgumentNullException(nameof(tenantResolver));
            _currentUser         = currentUser         ?? throw new ArgumentNullException(nameof(currentUser));
            _logger              = logger              ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<List<CashierBalanceShiftDto>>> GetShifts(
            [FromQuery] bool? isClosed = null,
            [FromQuery] Guid? cashierId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] DateTime? toExclusive = null,
            CancellationToken cancellationToken = default)
        {
            var shifts = await _cashierShiftService.GetShiftsAsync(
                _tenantResolver.GetTenantId(),
                _currentUser.UserId,
                _currentUser.IsAdminOrManager,
                new CashierShiftQueryDto
                {
                    IsClosed = isClosed,
                    CashierId = cashierId,
                    From = from,
                    To = to,
                    ToExclusive = toExclusive
                },
                cancellationToken);

            return Ok(shifts);
        }

        [HttpGet("active/{cashierId:guid}")]
        public async Task<ActionResult<CashierBalanceShiftDto>> GetActiveShift(Guid cashierId, CancellationToken cancellationToken)
        {
            var shift = await _cashierShiftService.GetActiveShiftAsync(
                _tenantResolver.GetTenantId(),
                cashierId,
                _currentUser.UserId,
                _currentUser.IsAdminOrManager,
                cancellationToken);

            return shift == null ? NotFound() : Ok(shift);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CashierBalanceShiftDto>> GetShift(Guid id, CancellationToken cancellationToken)
        {
            var shift = await _cashierShiftService.GetShiftByIdAsync(
                _tenantResolver.GetTenantId(),
                id,
                _currentUser.UserId,
                _currentUser.IsAdminOrManager,
                cancellationToken);

            return shift == null ? NotFound() : Ok(shift);
        }

        [HttpPost("open")]
        [Authorize(Roles = AppRoleNames.Admin)]
        public async Task<ActionResult<CashierBalanceShiftDto>> OpenShift(
            [FromBody] OpenShiftRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var tenantId = _tenantResolver.GetTenantId();
            var shift = await _cashierShiftService.OpenShiftAsync(
                tenantId, _currentUser.UserId, request, cancellationToken);

            _logger.LogInformation(
                "Admin {AdminUserId} opened shift {ShiftId} for cashier {CashierId} (tenant={TenantId})",
                _currentUser.UserId, shift.Id, request.CashierId, tenantId);

            return CreatedAtAction(nameof(GetShift), new { id = shift.Id }, shift);
        }

        [HttpPost("open-self")]
        [Authorize(Roles = AppRoleGroups.AdminOrCashier)]
        public async Task<ActionResult<CashierBalanceShiftDto>> OpenOwnShift(
            [FromBody] OpenOwnShiftRequest? request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var tenantId = _tenantResolver.GetTenantId();
            var shift = await _cashierShiftService.OpenOwnShiftAsync(
                tenantId, _currentUser.UserId, request, cancellationToken);

            _logger.LogInformation(
                "User {UserId} opened own shift {ShiftId} (tenant={TenantId})",
                _currentUser.UserId, shift.Id, tenantId);

            return CreatedAtAction(nameof(GetShift), new { id = shift.Id }, shift);
        }

        [HttpPut("{id:guid}/close")]
        [Authorize(Roles = AppRoleGroups.AdminOrCashier)]
        public async Task<ActionResult<CashierBalanceShiftDto>> CloseShift(
            Guid id, [FromBody] CloseShiftRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var tenantId = _tenantResolver.GetTenantId();
            var shift = await _cashierShiftService.CloseShiftAsync(
                tenantId, id, _currentUser.UserId, _currentUser.IsAdminOrManager, request, cancellationToken);

            _logger.LogInformation(
                "User {UserId} closed shift {ShiftId} (tenant={TenantId})",
                _currentUser.UserId, id, tenantId);

            return Ok(shift);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> DeleteShift(Guid id, CancellationToken cancellationToken)
        {
            var tenantId = _tenantResolver.GetTenantId();
            await _cashierShiftService.DeleteShiftAsync(tenantId, id, cancellationToken);

            _logger.LogWarning(
                "Admin {AdminUserId} deleted shift {ShiftId} (tenant={TenantId})",
                _currentUser.UserId, id, tenantId);

            return NoContent();
        }
    }
}
