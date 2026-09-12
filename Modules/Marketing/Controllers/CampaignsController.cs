using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Controllers
{
    [ApiController]
    [Route("api/v1/marketing/campaigns")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public sealed class CampaignsController : ControllerBase
    {
        private readonly ICampaignService _service;

        public CampaignsController(ICampaignService service)
            => _service = service ?? throw new ArgumentNullException(nameof(service));

        [HttpGet]
        public async Task<ActionResult<List<CampaignDto>>> GetAll([FromQuery] bool includeArchived, CancellationToken ct)
            => Ok(await _service.GetAllAsync(includeArchived, ct));

        [HttpGet("dashboard")]
        public async Task<ActionResult<CampaignDashboardDto>> Dashboard(CancellationToken ct)
            => Ok(await _service.GetDashboardAsync(ct));

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CampaignDto>> GetById(Guid id, CancellationToken ct)
            => Ok(await _service.GetByIdAsync(id, ct));

        [HttpPost]
        public async Task<ActionResult<CampaignDto>> Create([FromBody] CampaignUpsertDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<CampaignDto>> Update(Guid id, [FromBody] CampaignUpsertDto dto, CancellationToken ct)
            => Ok(await _service.UpdateAsync(id, dto, ct));

        [HttpPost("{id:guid}/status")]
        public async Task<ActionResult<CampaignDto>> ChangeStatus(Guid id, [FromBody] CampaignStatusChangeDto dto, CancellationToken ct)
            => Ok(await _service.ChangeStatusAsync(id, dto.Status, ct));

        // Semantic convenience routes (all validated by the same transition policy).
        [HttpPost("{id:guid}/activate")]
        public Task<ActionResult<CampaignDto>> Activate(Guid id, CancellationToken ct) => ChangeStatus(id, new CampaignStatusChangeDto { Status = CampaignStatus.Active }, ct);

        [HttpPost("{id:guid}/schedule")]
        public Task<ActionResult<CampaignDto>> Schedule(Guid id, CancellationToken ct) => ChangeStatus(id, new CampaignStatusChangeDto { Status = CampaignStatus.Scheduled }, ct);

        [HttpPost("{id:guid}/pause")]
        public Task<ActionResult<CampaignDto>> Pause(Guid id, CancellationToken ct) => ChangeStatus(id, new CampaignStatusChangeDto { Status = CampaignStatus.Paused }, ct);

        [HttpPost("{id:guid}/resume")]
        public Task<ActionResult<CampaignDto>> Resume(Guid id, CancellationToken ct) => ChangeStatus(id, new CampaignStatusChangeDto { Status = CampaignStatus.Active }, ct);

        [HttpPost("{id:guid}/archive")]
        public Task<ActionResult<CampaignDto>> Archive(Guid id, CancellationToken ct) => ChangeStatus(id, new CampaignStatusChangeDto { Status = CampaignStatus.Archived }, ct);
    }
}
