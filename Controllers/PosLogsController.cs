using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/pos/logs")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
    public class PosLogsController : ControllerBase
    {
        private readonly IPosLogService _posLogService;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserResolver _currentUserResolver;

        public PosLogsController(
            IPosLogService posLogService,
            ITenantResolver tenantResolver,
            ICurrentUserResolver currentUserResolver)
        {
            _posLogService = posLogService ?? throw new ArgumentNullException(nameof(posLogService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _currentUserResolver = currentUserResolver ?? throw new ArgumentNullException(nameof(currentUserResolver));
        }

        [HttpGet]
        public async Task<IActionResult> GetLogs([FromQuery] PosLogQueryDto query, CancellationToken ct)
        {
            var userId = await _currentUserResolver.ResolveUserIdAsync(User);
            if (userId == null) return Unauthorized();

            var logs = await _posLogService.GetLogsAsync(
                _tenantResolver.GetTenantId(),
                query,
                userId.Value,
                IsAdmin(),
                ct);

            return Ok(logs);
        }

        private bool IsAdmin()
            => User.IsInRole(AppRoleNames.Admin) || User.IsInRole(AppRoleNames.Manager);
    }
}
