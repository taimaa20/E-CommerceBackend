using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/shift-summary")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.ShiftDashboardViewers)]
    public class ShiftSummaryController : ControllerBase
    {
        private readonly IShiftSummaryService _service;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserAccessor _user;

        public ShiftSummaryController(
            IShiftSummaryService service,
            ITenantResolver tenantResolver,
            ICurrentUserAccessor user)
        {
            _service        = service        ?? throw new ArgumentNullException(nameof(service));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _user           = user           ?? throw new ArgumentNullException(nameof(user));
        }

        /// <summary>Live summary for the current cashier's active shift.</summary>
        [HttpGet]
        public async Task<ActionResult<ShiftSummaryDto>> GetCurrent(CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var dto = await _service.GetSummaryAsync(
                tenantId, shiftId: null, _user.UserId, _user.IsAdminOrManager,
                GeneralHelper.IsArabicRequested(Request), ct);
            return Ok(dto);
        }

        /// <summary>Live summary for a specific shift (admin/manager — any; cashier — own only).</summary>
        [HttpGet("{shiftId:guid}")]
        public async Task<ActionResult<ShiftSummaryDto>> GetById(Guid shiftId, CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var dto = await _service.GetSummaryAsync(
                tenantId, shiftId, _user.UserId, _user.IsAdminOrManager,
                GeneralHelper.IsArabicRequested(Request), ct);
            return Ok(dto);
        }
    }
}
