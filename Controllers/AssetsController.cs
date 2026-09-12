using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Services.Assets;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/assets")]
    [Authorize(Roles = AppRoleGroups.AssetViewers)]
    public sealed class AssetsController : ControllerBase
    {
        private const long UploadMaxBytes = AssetFileStorageService.DefaultMaxFileSizeBytes;
        private readonly IAssetService _service;

        public AssetsController(IAssetService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<AssetListItemDto>>> GetAssets([FromQuery] AssetQueryDto query, CancellationToken ct)
            => Ok(await _service.GetPagedAsync(query, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct));

        [HttpGet("export")]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        public async Task<IActionResult> Export([FromQuery] AssetQueryDto query, CancellationToken ct)
        {
            var rows = await _service.GetExportRowsAsync(query, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct);
            var bytes = System.Text.Encoding.UTF8.GetBytes(_service.BuildCsv(rows));
            return File(bytes, "text/csv", $"assets-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<AssetDashboardDto>> GetDashboard(CancellationToken ct)
            => Ok(await _service.GetDashboardAsync(GeneralHelper.IsArabicRequested(Request), BuildActor(), ct));

        [HttpGet("maintenance")]
        public async Task<ActionResult<PaginatedResponse<AssetMaintenanceRecordDto>>> GetMaintenance([FromQuery] AssetMaintenanceQueryDto query, CancellationToken ct)
            => Ok(await _service.GetMaintenanceAsync(query, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct));

        [HttpGet("logs")]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        public async Task<ActionResult<PaginatedResponse<AssetActivityLogDto>>> GetLogs([FromQuery] AssetActivityLogQueryDto query, CancellationToken ct)
            => Ok(await _service.GetLogsAsync(query, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct));

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AssetDetailsDto>> GetAssetDetails(Guid id, CancellationToken ct)
            => Ok(await _service.GetDetailsAsync(id, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct));

        [HttpPost]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        public async Task<ActionResult<AssetDetailsDto>> CreateAsset([FromBody] AssetCreateDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct);
            return CreatedAtAction(nameof(GetAssetDetails), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        public async Task<ActionResult<AssetDetailsDto>> UpdateAsset(Guid id, [FromBody] AssetUpdateDto dto, CancellationToken ct)
            => Ok(await _service.UpdateAsync(id, dto, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct));

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        public async Task<IActionResult> DeleteAsset(Guid id, CancellationToken ct)
        {
            await _service.SoftDeleteAsync(id, BuildActor(), ct);
            return NoContent();
        }

        [HttpPost("{id:guid}/restore")]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        public async Task<IActionResult> RestoreAsset(Guid id, CancellationToken ct)
        {
            await _service.RestoreAsync(id, BuildActor(), ct);
            return NoContent();
        }

        [HttpPost("{id:guid}/attachments")]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(UploadMaxBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = UploadMaxBytes)]
        public async Task<ActionResult<AssetAttachmentDto>> UploadAssetAttachment(
            Guid id,
            IFormFile file,
            [FromForm] string? attachmentType,
            [FromForm] Guid? maintenanceRecordId,
            CancellationToken ct)
            => Ok(await _service.AddAttachmentAsync(id, file, attachmentType, maintenanceRecordId, BuildActor(), ct));

        [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct)
        {
            await _service.DeleteAttachmentAsync(id, attachmentId, BuildActor(), ct);
            return NoContent();
        }

        [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
        public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
        {
            var (stream, fileName, mimeType) = await _service.OpenAttachmentForDownloadAsync(id, attachmentId, BuildActor(), ct);
            return File(stream, mimeType, fileName, enableRangeProcessing: true);
        }

        [HttpGet("{id:guid}/maintenance")]
        public async Task<ActionResult<PaginatedResponse<AssetMaintenanceRecordDto>>> GetAssetMaintenance(Guid id, CancellationToken ct)
        {
            var query = new AssetMaintenanceQueryDto { AssetId = id, Page = 1, PageSize = 100 };
            return Ok(await _service.GetMaintenanceAsync(query, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct));
        }

        [HttpPost("{id:guid}/maintenance")]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        public async Task<ActionResult<AssetMaintenanceRecordDto>> AddMaintenanceRecord(Guid id, [FromBody] AssetMaintenanceCreateDto dto, CancellationToken ct)
            => Ok(await _service.AddMaintenanceAsync(id, dto, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct));

        [HttpGet("{id:guid}/logs")]
        public async Task<ActionResult<PaginatedResponse<AssetActivityLogDto>>> GetAssetLogs(Guid id, [FromQuery] AssetActivityLogQueryDto query, CancellationToken ct)
        {
            query.AssetId = id;
            return Ok(await _service.GetLogsAsync(query, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct));
        }

        private AssetActor BuildActor()
            => new()
            {
                UserId = GetCurrentUserId(),
                Name = GetActorName(),
                CanManageAssets = User.IsInRole(AppRoleNames.Admin) || User.IsInRole(AppRoleNames.Manager),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                DeviceInfo = Request.Headers.UserAgent.ToString()
            };

        private Guid? GetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("UserId")
                ?? User.FindFirstValue("userId");
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        private string? GetActorName()
            => User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("name")
                ?? User.Identity?.Name;
    }
}
