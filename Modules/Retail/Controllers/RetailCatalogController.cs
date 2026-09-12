using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Retail.Security;
using RestaurantPos.Api.Modules.Retail.Services;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Retail.Controllers
{
    [ApiController]
    [Route("api/retail/catalog")]
    [Authorize(Roles = RetailRoleGroups.RetailCatalogViewers)]
    public class RetailCatalogController : ControllerBase
    {
        private const int DefaultPageSize = 50;

        private readonly IRetailCatalogService _catalogService;
        private readonly IBranchContext _branchContext;

        public RetailCatalogController(IRetailCatalogService catalogService, IBranchContext branchContext)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        }

        [HttpGet]
        public async Task<IActionResult> GetPage(
            CancellationToken ct,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] Guid? categoryId = null,
            [FromQuery] Guid? supplierId = null,
            [FromQuery] string? brand = null,
            [FromQuery] bool currentBranchOnly = false)
        {
            var page = await _catalogService.GetPageAsync(
                IsArabicRequested(Request),
                pageNumber,
                pageSize,
                search,
                categoryId,
                supplierId,
                brand,
                await ResolveBranchScopeAsync(currentBranchOnly, ct),
                ct);

            return Ok(page);
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(CancellationToken ct, [FromQuery] bool currentBranchOnly = false)
            => Ok(await _catalogService.GetSummaryAsync(await ResolveBranchScopeAsync(currentBranchOnly, ct), ct));

        /// <summary>
        /// Null means "every branch" — what the tenant-wide product catalogue wants. The
        /// Inventory screen asks for the active branch instead, because stock is held per branch.
        /// </summary>
        private async Task<Guid?> ResolveBranchScopeAsync(bool currentBranchOnly, CancellationToken ct)
            => currentBranchOnly ? (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id : null;

        [HttpGet("brands")]
        public async Task<IActionResult> GetBrands(CancellationToken ct)
            => Ok(await _catalogService.GetBrandsAsync(ct));

        private static bool IsArabicRequested(HttpRequest request)
            => request.Headers["Accept-Language"].ToString()
                .StartsWith("ar", StringComparison.OrdinalIgnoreCase);
    }
}
