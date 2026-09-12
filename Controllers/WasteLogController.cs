using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using System.Security.Claims;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/waste-log")]
    [ApiController]
    [Authorize]
    public class WasteLogController : ControllerBase
    {
        private readonly IWasteLogService _wasteLogService;

        public WasteLogController(IWasteLogService wasteLogService)
        {
            _wasteLogService = wasteLogService ?? throw new ArgumentNullException(nameof(wasteLogService));
        }

        [HttpGet]
        [Authorize(Roles = AppRoleGroups.WasteLogViewers)]
        public async Task<ActionResult<WasteLogPageDto>> GetWasteLog([FromQuery] WasteLogQueryDto query, CancellationToken ct)
        {
            var result = await _wasteLogService.GetPagedAsync(query, ct);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = AppRoleGroups.WasteLogViewers)]
        public async Task<ActionResult<WasteLogDto>> GetById(Guid id, CancellationToken ct)
        {
            var result = await _wasteLogService.GetByIdAsync(id, ct);
            return Ok(result);
        }

        [HttpGet("summary")]
        [Authorize(Roles = AppRoleGroups.WasteLogViewers)]
        public async Task<ActionResult<WasteLogSummaryDto>> GetSummary([FromQuery] WasteLogQueryDto query, CancellationToken ct)
        {
            var result = await _wasteLogService.GetSummaryAsync(query, ct);
            return Ok(result);
        }

        [HttpGet("analytics")]
        [Authorize(Roles = AppRoleGroups.WasteLogViewers)]
        public async Task<ActionResult<WasteLogAnalyticsDto>> GetAnalytics([FromQuery] WasteLogQueryDto query, CancellationToken ct)
        {
            var result = await _wasteLogService.GetAnalyticsAsync(query, ct);
            return Ok(result);
        }

        [HttpGet("export")]
        [Authorize(Roles = AppRoleGroups.WasteLogViewers)]
        public async Task<ActionResult> Export([FromQuery] WasteLogQueryDto query, CancellationToken ct)
        {
            var bytes = await _wasteLogService.ExportCsvAsync(query, ct);
            return File(bytes, "text/csv", $"waste-log-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        [HttpGet("employees")]
        [Authorize(Roles = AppRoleGroups.WasteCreators)]
        public async Task<ActionResult<IReadOnlyList<WasteEmployeeOptionDto>>> SearchEmployees(
            [FromQuery] string? search,
            [FromQuery] int limit,
            CancellationToken ct)
        {
            var result = await _wasteLogService.SearchEmployeesAsync(search, limit <= 0 ? 20 : limit, ct);
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = AppRoleGroups.WasteCreators)]
        public async Task<ActionResult<WasteLogDto>> Create([FromBody] WasteLogCreateDto request, CancellationToken ct)
        {
            var result = await _wasteLogService.CreateAsync(request, GetCurrentUserId(), GetCurrentUserName(), ct);
            return Ok(result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = AppRoleGroups.WasteCreators)]
        public async Task<ActionResult<WasteLogDto>> Update(Guid id, [FromBody] WasteLogUpdateDto request, CancellationToken ct)
        {
            var result = await _wasteLogService.UpdateAsync(id, request, GetCurrentUserId(), GetCurrentUserName(), ct);
            return Ok(result);
        }

        [HttpPost("{id}/approve")]
        [Authorize(Roles = AppRoleGroups.WasteApprovers)]
        public async Task<ActionResult<WasteLogDto>> Approve(Guid id, [FromBody] WasteLogDecisionDto request, CancellationToken ct)
        {
            var result = await _wasteLogService.ApproveAsync(id, request, GetCurrentUserId(), GetCurrentUserName(), ct);
            return Ok(result);
        }

        [HttpPost("{id}/reject")]
        [Authorize(Roles = AppRoleGroups.WasteApprovers)]
        public async Task<ActionResult<WasteLogDto>> Reject(Guid id, [FromBody] WasteLogDecisionDto request, CancellationToken ct)
        {
            var result = await _wasteLogService.RejectAsync(id, request, GetCurrentUserId(), GetCurrentUserName(), ct);
            return Ok(result);
        }

        [HttpPost("/api/inventory/{materialId}/waste")]
        [Authorize(Roles = AppRoleGroups.WasteCreators)]
        public async Task<ActionResult<WasteLogDto>> LogInventoryWaste(
            Guid materialId,
            [FromBody] LogWasteRequest request,
            CancellationToken ct)
        {
            var result = await _wasteLogService.CreateAsync(new WasteLogCreateDto
            {
                WasteType = "OTHER",
                MaterialId = materialId,
                Quantity = request.Quantity,
                Reason = request.Reason,
                Notes = request.Notes,
                IsAffectingInventory = true
            }, GetCurrentUserId(), GetCurrentUserName(), ct);

            return Ok(result);
        }

        private Guid? GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("UserId")
                ?? User.FindFirstValue("userId");

            return Guid.TryParse(value, out var id) ? id : null;
        }

        private string? GetCurrentUserName()
        {
            return User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("name")
                ?? User.Identity?.Name;
        }
    }

    public class LogWasteRequest
    {
        public decimal Quantity { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
