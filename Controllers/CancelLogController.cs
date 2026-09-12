using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/cancel-log")]
    [ApiController]
    [EnableRateLimiting("api")]
    public class CancelLogController : ControllerBase
    {
        // Built-in fallback set, returned only when the DB is unreachable so the cancel
        // modal continues to function during incident windows.
        private static readonly CancelReasonDto[] DefaultCancelReasons =
        {
            new() { Id = 1, Code = "CLIENT_CHANGED_MIND", Name = "Client changed mind", NameAr = "العميل غير رأيه",  RequiresNote = false },
            new() { Id = 2, Code = "WRONG_ORDER_ENTERED", Name = "Wrong order entered", NameAr = "تم إدخال طلب خاطئ", RequiresNote = false },
            new() { Id = 3, Code = "CLIENT_LEFT",         Name = "Client left",          NameAr = "العميل غادر",      RequiresNote = false },
            new() { Id = 4, Code = "ALLERGY_CONCERN",     Name = "Allergy concern",      NameAr = "مشكلة حساسية",      RequiresNote = false },
            new() { Id = 5, Code = "ITEM_UNAVAILABLE",    Name = "Item unavailable",     NameAr = "المنتج غير متوفر",  RequiresNote = false },
            new() { Id = 6, Code = "OTHER",               Name = "Other",                NameAr = "سبب آخر",           RequiresNote = true  },
        };

        private readonly ICancelLogService _cancelLogService;
        private readonly ICancelReasonService _cancelReasonService;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<CancelLogController> _logger;

        public CancelLogController(
            ICancelLogService cancelLogService,
            ICancelReasonService cancelReasonService,
            ITenantResolver tenantResolver,
            ICurrentUserAccessor currentUser,
            ILogger<CancelLogController> logger)
        {
            _cancelLogService    = cancelLogService    ?? throw new ArgumentNullException(nameof(cancelLogService));
            _cancelReasonService = cancelReasonService ?? throw new ArgumentNullException(nameof(cancelReasonService));
            _tenantResolver      = tenantResolver      ?? throw new ArgumentNullException(nameof(tenantResolver));
            _currentUser         = currentUser         ?? throw new ArgumentNullException(nameof(currentUser));
            _logger              = logger              ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET api/cancel-log?dateFrom=&dateTo=&page=1&limit=50
        // Admin/Manager → all entries; Cashier/Waiter → own entries only (server-enforced).
        [HttpGet]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<ActionResult<CancelLogPageDto>> GetCancelLog(
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            [FromQuery] Guid? orderId,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default)
        {
            var role = _currentUser.Role;
            if (role == null) return Unauthorized();

            // Whitelist the roles permitted by this endpoint. [Authorize(PosOrderEditors)]
            // already filters most of the matrix; we restate it here so a future role
            // addition does not silently inherit admin-level visibility.
            if (role != UserRole.Admin
                && role != UserRole.Manager
                && role != UserRole.Waiter
                && role != UserRole.Cashier)
            {
                return Forbid();
            }

            var query = new CancelLogQueryDto
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                OrderId = orderId,
                Page = page,
                Limit = limit
            };

            var result = await _cancelLogService.GetAsync(
                _tenantResolver.GetTenantId(),
                _currentUser.UserId,
                role.Value,
                query,
                cancellationToken);

            return Ok(result);
        }

        // ── Cancel reasons (lookup + admin CRUD) ───────────────────────────────────
        // Kept in this controller (rather than split into a CancelReasonsController) to
        // preserve the legacy file ownership. Absolute routes match the original mapping.

        [HttpGet("/api/cancel-reasons")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<ActionResult<IReadOnlyList<CancelReasonDto>>> GetCancelReasons(CancellationToken cancellationToken)
        {
            var tenantId = _tenantResolver.GetTenantId();
            try
            {
                var reasons = await _cancelReasonService.GetActiveAsync(tenantId, cancellationToken);
                if (reasons.Count == 0)
                {
                    _logger.LogWarning("No cancel reasons found for tenant {TenantId}. Returning built-in defaults.", tenantId);
                    return Ok(DefaultCancelReasons);
                }
                return Ok(reasons);
            }
            catch (Exception ex)
            {
                // DB-down fallback: the cancel flow must remain functional even if the DB
                // is unreachable for a brief window.
                _logger.LogWarning(ex, "Failed to load cancel reasons from the database for tenant {TenantId}. Returning built-in defaults.", tenantId);
                return Ok(DefaultCancelReasons);
            }
        }

        // POST — admin-only. Previously open to PosOrderEditors, which let Cashier/Waiter
        // mutate tenant-shared reference data.
        [HttpPost("/api/cancel-reasons")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<CancelReasonDetailDto>> CreateCancelReason(
            [FromBody] CancelReasonUpsertRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var tenantId = _tenantResolver.GetTenantId();
            var dto = await _cancelReasonService.CreateAsync(tenantId, request, cancellationToken);

            _logger.LogInformation(
                "Cancel reason {ReasonCode} (id={ReasonId}) created by {UserId} for tenant {TenantId}",
                dto.Code, dto.Id, _currentUser.UserId, tenantId);

            return Ok(dto);
        }

        [HttpPut("/api/cancel-reasons/{id:int}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<CancelReasonDetailDto>> UpdateCancelReason(
            int id,
            [FromBody] CancelReasonUpsertRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var tenantId = _tenantResolver.GetTenantId();
            var dto = await _cancelReasonService.UpdateAsync(tenantId, id, request, cancellationToken);

            _logger.LogInformation(
                "Cancel reason {ReasonCode} (id={ReasonId}) updated by {UserId} for tenant {TenantId}",
                dto.Code, dto.Id, _currentUser.UserId, tenantId);

            return Ok(dto);
        }
    }
}
