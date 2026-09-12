using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Home;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Modules.Dashboard.Services.Caching;
using RestaurantPos.Api.Modules.Dashboard.Services.Home;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Controllers
{
    [ApiController]
    [Route("api/dashboards/home")]
    [Authorize(Roles = AppRoleGroups.HomeDashboardViewers)]
    [DashboardScope(DashboardScope.Operations, DefaultPreset = DashboardFilterPreset.Today)]
    public sealed class HomeDashboardController : ControllerBase
    {
        private readonly IHomeAnalyticsService  _home;
        private readonly IHomeDashboardExportService _export;
        private readonly IDashboardCacheService _cache;
        private readonly IDashboardFilterContext _ctx;

        public HomeDashboardController(
            IHomeAnalyticsService home,
            IHomeDashboardExportService export,
            IDashboardCacheService cache,
            IDashboardFilterContext ctx)
        {
            _home   = home   ?? throw new ArgumentNullException(nameof(home));
            _export = export ?? throw new ArgumentNullException(nameof(export));
            _cache  = cache  ?? throw new ArgumentNullException(nameof(cache));
            _ctx    = ctx    ?? throw new ArgumentNullException(nameof(ctx));
        }

        [HttpGet]
        public async Task<ActionResult<HomeSnapshotDto>> Get(
            [FromQuery] DashboardFilterDto filter,
            CancellationToken ct)
        {
            _ = filter;
            return Ok(await GetSnapshotAsync(ct));
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] DashboardFilterDto filter,
            [FromQuery] string? currency,
            CancellationToken ct)
        {
            _ = filter;
            var snapshot = await GetSnapshotAsync(ct);
            var file = _export.BuildWorkbook(snapshot, currency);
            return File(file.Content, file.ContentType, file.FileName);
        }

        private Task<HomeSnapshotDto> GetSnapshotAsync(CancellationToken ct)
            => _cache.GetOrCreateAsync(
                DashboardScope.Operations,
                "home-snapshot",
                _ctx.Filter,
                () => _home.BuildSnapshotAsync(ct),
                DashboardCacheTtl.Short,
                ct);
    }
}
