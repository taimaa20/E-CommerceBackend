using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/users/{userId:guid}/branches")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public class UserBranchesController : ControllerBase
    {
        private readonly IUserBranchService _service;

        public UserBranchesController(IUserBranchService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet]
        public async Task<IActionResult> GetAssignments(Guid userId, CancellationToken ct)
        {
            var assignments = await _service.GetAssignmentsAsync(userId, ct);
            return Ok(assignments);
        }

        [HttpPost]
        public async Task<IActionResult> Assign(Guid userId, [FromBody] UserBranchAssignDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var assignments = await _service.AssignAsync(userId, dto, ct);
            return Ok(assignments);
        }

        [HttpPost("{branchId:guid}/default")]
        public async Task<IActionResult> SetDefault(Guid userId, Guid branchId, CancellationToken ct)
        {
            var assignments = await _service.SetDefaultAsync(userId, branchId, ct);
            return Ok(assignments);
        }

        [HttpDelete("{branchId:guid}")]
        public async Task<IActionResult> Remove(
            Guid userId,
            Guid branchId,
            [FromQuery] Guid? newDefaultBranchId,
            CancellationToken ct)
        {
            var assignments = await _service.RemoveAsync(
                userId,
                branchId,
                new UserBranchRemoveDto { NewDefaultBranchId = newDefaultBranchId },
                ct);
            return Ok(assignments);
        }
    }
}
