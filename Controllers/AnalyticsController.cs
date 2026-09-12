using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    /// <summary>
    /// Legacy analytics endpoint preserved for backwards-compatibility with
    /// the existing /admin/analytics page. Tenant is now derived from the
    /// current user (previously trusted from query string — that was a
    /// cross-tenant read vulnerability). New code should use
    /// /api/dashboards/* instead.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    [Obsolete("Use /api/dashboards/financial or /api/dashboards/operations instead.")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IOrderMetricsService _orderMetricsService;
        private readonly ITenantResolver _tenantResolver;

        public AnalyticsController(IOrderMetricsService orderMetricsService, ITenantResolver tenantResolver)
        {
            _orderMetricsService = orderMetricsService ?? throw new ArgumentNullException(nameof(orderMetricsService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        [HttpGet]
        public async Task<ActionResult<AnalyticsResponseDto>> GetAnalytics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? branchId = null,
            CancellationToken ct = default)
        {
            // Tenant comes from the authenticated user, not the request. The
            // previous signature accepted [FromQuery] Guid tenantId, allowing
            // any caller to read any tenant's analytics.
            var tenantId = _tenantResolver.GetTenantId();
            return Ok(await _orderMetricsService.GetAnalyticsAsync(tenantId, startDate, endDate, branchId, ct));
        }
    }
}
