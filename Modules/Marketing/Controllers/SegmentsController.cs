using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Controllers
{
    [ApiController]
    [Route("api/v1/marketing/segments")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public sealed class SegmentsController : ControllerBase
    {
        private readonly ISegmentService _service;

        public SegmentsController(ISegmentService service)
            => _service = service ?? throw new ArgumentNullException(nameof(service));

        [HttpGet]
        public async Task<ActionResult<List<SegmentDto>>> GetAll([FromQuery] bool includeInactive, CancellationToken ct)
            => Ok(await _service.GetAllAsync(includeInactive, ct));

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SegmentDto>> GetById(Guid id, CancellationToken ct)
            => Ok(await _service.GetByIdAsync(id, ct));

        [HttpPost]
        public async Task<ActionResult<SegmentDto>> Create([FromBody] SegmentUpsertDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<SegmentDto>> Update(Guid id, [FromBody] SegmentUpsertDto dto, CancellationToken ct)
            => Ok(await _service.UpdateAsync(id, dto, ct));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        {
            await _service.DeactivateAsync(id, ct);
            return Ok(new { message = "Segment deactivated." });
        }

        [HttpGet("{id:guid}/members")]
        public async Task<ActionResult<PaginatedResponse<SegmentMemberDto>>> GetMembers(
            Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
            => Ok(await _service.GetMembersAsync(id, page, pageSize, GeneralHelper.IsArabicRequested(Request), ct));

        [HttpPost("{id:guid}/members")]
        public async Task<IActionResult> AddMember(Guid id, [FromBody] AddSegmentMemberRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            await _service.AddMemberAsync(id, request.CustomerId, ct);
            return Ok(new { message = "Member added." });
        }

        [HttpDelete("{id:guid}/members/{customerId:guid}")]
        public async Task<IActionResult> RemoveMember(Guid id, Guid customerId, CancellationToken ct)
        {
            await _service.RemoveMemberAsync(id, customerId, ct);
            return Ok(new { message = "Member removed." });
        }

        [HttpPost("{id:guid}/compute")]
        public async Task<ActionResult<SegmentComputeResultDto>> Compute(Guid id, CancellationToken ct)
            => Ok(await _service.ComputeAsync(id, ct));
    }
}
