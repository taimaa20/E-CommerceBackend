using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/admin/cashier-balances")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    [EnableRateLimiting("api")]
    public class AdminCashierBalancesController : ControllerBase
    {
        private readonly ICashierShiftService _cashierShiftService;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<AdminCashierBalancesController> _logger;

        public AdminCashierBalancesController(
            ICashierShiftService cashierShiftService,
            ITenantResolver tenantResolver,
            ICurrentUserAccessor currentUser,
            ILogger<AdminCashierBalancesController> logger)
        {
            _cashierShiftService = cashierShiftService ?? throw new ArgumentNullException(nameof(cashierShiftService));
            _tenantResolver      = tenantResolver      ?? throw new ArgumentNullException(nameof(tenantResolver));
            _currentUser         = currentUser         ?? throw new ArgumentNullException(nameof(currentUser));
            _logger              = logger              ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("cashiers")]
        public async Task<IActionResult> GetCashiers(CancellationToken cancellationToken)
        {
            var cashiers = await _cashierShiftService.GetShiftOwnerOptionsAsync(
                _tenantResolver.GetTenantId(),
                cancellationToken);
            return Ok(cashiers);
        }

        [HttpGet]
        public async Task<IActionResult> GetShifts(
            [FromQuery] bool? isClosed = null,
            [FromQuery] Guid? cashierId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] DateTime? toExclusive = null,
            CancellationToken cancellationToken = default)
        {
            var tenantId = _tenantResolver.GetTenantId();
            _logger.LogInformation(
                "Admin {AdminUserId} listed cashier balances (tenant={TenantId}, cashierId={CashierId}, isClosed={IsClosed})",
                _currentUser.UserId, tenantId, cashierId, isClosed);

            var shifts = await _cashierShiftService.GetShiftsAsync(
                tenantId,
                _currentUser.UserId,
                isAdmin: true,
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
    }
}
