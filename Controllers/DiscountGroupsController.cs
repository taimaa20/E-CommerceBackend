using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    /// <summary>Affiliation-based discount groups (e.g. University Students 10%). Management is
    /// Admin/Manager-only; the active-options list is readable by any POS order editor so the
    /// cashier can pick a group. The applied percentage is always re-resolved server-side at
    /// order creation — this controller never lets the client dictate a discount amount.</summary>
    // Controller-level guard is authentication-only; each action sets its own role policy so the
    // cashier-facing /active list stays readable by POS order editors while management stays
    // Admin/Manager-only. (Stacked [Authorize] role attributes AND-combine, so a shared
    // controller-level role filter would wrongly intersect with the per-action ones.)
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [EnableRateLimiting("api")]
    public class DiscountGroupsController : ControllerBase
    {
        private readonly IDiscountGroupService _service;
        private readonly ITenantResolver _tenantResolver;
        private readonly ILogger<DiscountGroupsController> _logger;

        public DiscountGroupsController(
            IDiscountGroupService service,
            ITenantResolver tenantResolver,
            ILogger<DiscountGroupsController> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => Ok(await _service.GetAllAsync(ct));

        // Cashier/order screen: only active groups, readable by any POS order editor.
        [HttpGet("active")]
        [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
        public async Task<IActionResult> GetActive(CancellationToken ct)
            => Ok(await _service.GetActiveOptionsAsync(ct));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DiscountGroupCreateDto input, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var tenantId = _tenantResolver.GetTenantId();
            var created = await _service.CreateAsync(tenantId, input, ct);
            return Ok(created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] DiscountGroupUpdateDto input, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _service.UpdateAsync(id, input, ct);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpPatch("{id}/active")]
        public async Task<IActionResult> SetActive(Guid id, [FromBody] DiscountGroupActiveDto input, CancellationToken ct)
        {
            var updated = await _service.SetActiveAsync(id, input.IsActive, ct);
            if (updated == null) return NotFound();
            return Ok(updated);
        }
    }

    public class DiscountGroupActiveDto
    {
        public bool IsActive { get; set; }
    }
}
