using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/branch-context")]
    [Authorize]
    public class BranchContextController : ControllerBase
    {
        private readonly IBranchContext _branchContext;

        public BranchContextController(IBranchContext branchContext)
        {
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrent(CancellationToken ct)
        {
            var context = await _branchContext.GetCurrentAsync(ct);
            return Ok(context);
        }

        [HttpPost("switch")]
        public async Task<IActionResult> Switch([FromBody] BranchSwitchDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var context = await _branchContext.SwitchAsync(dto.BranchId, ct);
            return Ok(context);
        }
    }
}
