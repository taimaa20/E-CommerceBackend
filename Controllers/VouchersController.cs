using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VouchersController : ControllerBase
    {
        private readonly IVoucherService _voucherService;
        private readonly ITenantResolver _tenantResolver;

        public VouchersController(
            IVoucherService voucherService,
            ITenantResolver tenantResolver)
        {
            _voucherService = voucherService ?? throw new ArgumentNullException(nameof(voucherService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        [HttpGet("availability")]
        [Authorize(Roles = AppRoleGroups.TakeawayCheckoutOperators)]
        public async Task<ActionResult<VoucherAvailabilityDto>> GetAvailability(CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            return Ok(await _voucherService.GetAvailabilityAsync(tenantId, ct));
        }

        [HttpGet("audit/today")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<VoucherAuditSummaryDto>> GetTodayAudit(CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            return Ok(await _voucherService.GetTodayAuditAsync(tenantId, ct));
        }
    }
}
