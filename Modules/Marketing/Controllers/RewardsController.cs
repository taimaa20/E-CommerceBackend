using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Controllers
{
    /// <summary>
    /// Rewards catalog management (admin) + redemption (cashier). Class allows CashierOperators;
    /// management actions are narrowed to AdminOnly (the AND of both groups = Admin/Manager).
    /// </summary>
    [ApiController]
    [Route("api/v1/marketing/rewards")]
    [Authorize(Roles = AppRoleGroups.CashierOperators)]
    public sealed class RewardsController : ControllerBase
    {
        private readonly IRewardService _service;

        public RewardsController(IRewardService service)
            => _service = service ?? throw new ArgumentNullException(nameof(service));

        // ── Admin catalog management ──
        [HttpGet]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<List<RewardDto>>> GetAll([FromQuery] bool includeInactive, CancellationToken ct)
            => Ok(await _service.GetAllAsync(includeInactive, GeneralHelper.IsArabicRequested(Request), ct));

        [HttpGet("dashboard")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<RewardDashboardDto>> Dashboard(CancellationToken ct)
            => Ok(await _service.GetDashboardAsync(ct));

        [HttpGet("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<RewardDto>> GetById(Guid id, CancellationToken ct)
            => Ok(await _service.GetByIdAsync(id, GeneralHelper.IsArabicRequested(Request), ct));

        [HttpPost]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<RewardDto>> Create([FromBody] RewardUpsertDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<RewardDto>> Update(Guid id, [FromBody] RewardUpsertDto dto, CancellationToken ct)
            => Ok(await _service.UpdateAsync(id, dto, ct));

        [HttpPost("{id:guid}/activate")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        {
            await _service.SetActiveAsync(id, true, ct);
            return Ok(new { message = "Reward activated." });
        }

        [HttpPost("{id:guid}/deactivate")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        {
            await _service.SetActiveAsync(id, false, ct);
            return Ok(new { message = "Reward deactivated." });
        }

        // ── Cashier catalog + redemption ──
        [HttpGet("catalog")]
        public async Task<ActionResult<List<RewardDto>>> Catalog(CancellationToken ct)
            => Ok(await _service.GetActiveCatalogAsync(GeneralHelper.IsArabicRequested(Request), ct));

        [HttpPost("{id:guid}/redeem")]
        public async Task<ActionResult<RewardRedemptionResultDto>> Redeem(Guid id, [FromBody] RewardRedeemRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _service.RedeemAsync(id, request, GeneralHelper.IsArabicRequested(Request), ct));
        }

        [HttpGet("redemptions")]
        public async Task<ActionResult<PaginatedResponse<RewardRedemptionDto>>> Redemptions(
            [FromQuery] Guid customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
            => Ok(await _service.GetRedemptionsAsync(customerId, page, pageSize, GeneralHelper.IsArabicRequested(Request), ct));
    }
}
