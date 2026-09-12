using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/shift-rules-config")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.ShiftConfigManagers)]
    public class ShiftRulesConfigController : ControllerBase
    {
        private readonly IShiftRulesConfigService _service;
        private readonly ITenantResolver _tenantResolver;
        private readonly ILogger<ShiftRulesConfigController> _logger;

        public ShiftRulesConfigController(
            IShiftRulesConfigService service,
            ITenantResolver tenantResolver,
            ILogger<ShiftRulesConfigController> logger)
        {
            _service        = service        ?? throw new ArgumentNullException(nameof(service));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _logger         = logger         ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<ShiftRulesConfigDto>> Get(CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var config = await _service.GetForTenantAsync(tenantId, ct);
            return Ok(ToDto(config));
        }

        [HttpPut]
        public async Task<ActionResult<ShiftRulesConfigDto>> Update(
            [FromBody] ShiftRulesConfigDto dto,
            CancellationToken ct)
        {
            if (dto is null) return BadRequest("Body is required.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var tenantId = _tenantResolver.GetTenantId();
            var saved = await _service.UpsertAsync(tenantId, new ShiftRulesConfig
            {
                ActiveShiftRule                  = dto.ActiveShiftRule,
                RequireAllOrdersPaid             = dto.RequireAllOrdersPaid,
                RequireAllOrdersReady            = dto.RequireAllOrdersReady,
                RequireAllOrdersServed           = dto.RequireAllOrdersServed,
                RequireAllOrdersCompleted        = dto.RequireAllOrdersCompleted,
                AllowPendingOrders               = dto.AllowPendingOrders,
                AllowPreparingOrders             = dto.AllowPreparingOrders,
                AllowReadyOrders                 = dto.AllowReadyOrders,
                AllowPendingDeliveryOrders       = dto.AllowPendingDeliveryOrders,
                AllowPendingCancellationRequests = dto.AllowPendingCancellationRequests,
                AllowForcedShiftClose            = dto.AllowForcedShiftClose,
                AllowShiftCloseWithOpenOrders    = dto.AllowShiftCloseWithOpenOrders,
            }, ct);

            return Ok(ToDto(saved));
        }

        private static ShiftRulesConfigDto ToDto(ShiftRulesConfig? c) => new()
        {
            ActiveShiftRule                  = c?.ActiveShiftRule                  ?? ActiveShiftRule.Unlimited,
            RequireAllOrdersPaid             = c?.RequireAllOrdersPaid             ?? false,
            RequireAllOrdersReady            = c?.RequireAllOrdersReady            ?? false,
            RequireAllOrdersServed           = c?.RequireAllOrdersServed           ?? false,
            RequireAllOrdersCompleted        = c?.RequireAllOrdersCompleted        ?? false,
            AllowPendingOrders               = c?.AllowPendingOrders               ?? true,
            AllowPreparingOrders             = c?.AllowPreparingOrders             ?? true,
            AllowReadyOrders                 = c?.AllowReadyOrders                 ?? true,
            AllowPendingDeliveryOrders       = c?.AllowPendingDeliveryOrders       ?? true,
            AllowPendingCancellationRequests = c?.AllowPendingCancellationRequests ?? true,
            AllowForcedShiftClose            = c?.AllowForcedShiftClose            ?? false,
            AllowShiftCloseWithOpenOrders    = c?.AllowShiftCloseWithOpenOrders    ?? false,
        };
    }
}
