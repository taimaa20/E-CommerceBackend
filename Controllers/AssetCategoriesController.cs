using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/asset-categories")]
    [Authorize(Roles = AppRoleGroups.AssetViewers)]
    public sealed class AssetCategoriesController : ControllerBase
    {
        private readonly IAssetService _service;

        public AssetCategoriesController(IAssetService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet]
        public async Task<ActionResult<List<AssetCategoryDto>>> GetCategories([FromQuery] bool activeOnly = false, CancellationToken ct = default)
            => Ok(await _service.GetCategoriesAsync(activeOnly, GeneralHelper.IsArabicRequested(Request), ct));

        [HttpPost]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        public async Task<ActionResult<AssetCategoryDto>> CreateCategory([FromBody] AssetCategoryCreateDto dto, CancellationToken ct)
        {
            var created = await _service.CreateCategoryAsync(dto, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct);
            return CreatedAtAction(nameof(GetCategories), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        public async Task<ActionResult<AssetCategoryDto>> UpdateCategory(Guid id, [FromBody] AssetCategoryUpdateDto dto, CancellationToken ct)
            => Ok(await _service.UpdateCategoryAsync(id, dto, GeneralHelper.IsArabicRequested(Request), BuildActor(), ct));

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = AppRoleGroups.AssetManagers)]
        public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
        {
            await _service.DeleteCategoryAsync(id, BuildActor(), ct);
            return NoContent();
        }

        private AssetActor BuildActor()
            => new()
            {
                UserId = GetCurrentUserId(),
                Name = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name,
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
    }
}
