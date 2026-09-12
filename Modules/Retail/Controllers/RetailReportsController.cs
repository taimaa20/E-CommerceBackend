using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Retail.Security;
using RestaurantPos.Api.Modules.Retail.Services;

namespace RestaurantPos.Api.Modules.Retail.Controllers
{
    /// <summary>
    /// Read-only retail projections consumed by the EXISTING reports, procurement and
    /// supplier screens. Nothing here duplicates a restaurant endpoint — each answers a
    /// question the restaurant model cannot express for finished goods.
    /// </summary>
    [ApiController]
    [Route("api/retail")]
    [Authorize(Roles = RetailRoleGroups.RetailCatalogViewers)]
    public class RetailReportsController : ControllerBase
    {
        private readonly IRetailReportingService _reporting;

        public RetailReportsController(IRetailReportingService reporting)
        {
            _reporting = reporting ?? throw new ArgumentNullException(nameof(reporting));
        }

        /// <summary>Retail trading result — the corrected SUMMARY PROFIT.</summary>
        [HttpGet("performance")]
        public async Task<IActionResult> GetPerformance(
            CancellationToken ct,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var fromUtc = Normalize(from);
            // Half-open window: `to` names an inclusive day, so the boundary is the next day.
            var toExclusiveUtc = to.HasValue ? Normalize(to)!.Value.AddDays(1) : (DateTime?)null;

            return Ok(await _reporting.GetPerformanceAsync(fromUtc, toExclusiveUtc, ct));
        }

        /// <summary>
        /// Retail sales history, line by line. Reads the existing Order/OrderItem/Payment
        /// records — imported workbook sales and POS sales appear in the same list.
        /// </summary>
        [HttpGet("sales")]
        public async Task<IActionResult> GetSales(
            CancellationToken ct,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? search = null,
            [FromQuery] Guid? productId = null,
            [FromQuery] string? sku = null,
            [FromQuery] string? brand = null,
            [FromQuery] string? paymentStatus = null,
            [FromQuery] Guid? orderId = null)
        {
            var query = new RetailSalesQuery(
                pageNumber,
                pageSize,
                Normalize(from),
                to.HasValue ? Normalize(to)!.Value.AddDays(1) : null,
                search,
                productId,
                sku,
                brand,
                paymentStatus,
                orderId);

            return Ok(await _reporting.GetSalesAsync(query, ct));
        }

        /// <summary>Historical purchase-order headers imported from the workbook.</summary>
        [HttpGet("purchases")]
        public async Task<IActionResult> GetPurchases(CancellationToken ct)
            => Ok(await _reporting.GetLegacyPurchasesAsync(ct));

        /// <summary>
        /// Superseded by the shared supplier endpoints under <c>/api/procurement/suppliers</c>,
        /// which now expose the retail sourcing attributes on the one supplier master. Kept so
        /// nothing pointing at it breaks; no screen calls it any more.
        /// </summary>
        [HttpGet("suppliers")]
        [Obsolete("Use /api/procurement/suppliers — one supplier master serves both sides of the business.")]
        public async Task<IActionResult> GetSuppliers(CancellationToken ct, [FromQuery] bool retailOnly = true)
            => Ok(await _reporting.GetSuppliersAsync(retailOnly, ct));

        /// <summary>Npgsql rejects DateTimeKind.Unspecified on timestamptz parameters, so
        /// every inbound boundary is pinned to UTC before it reaches EF.</summary>
        private static DateTime? Normalize(DateTime? value)
            => value.HasValue
                ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc)
                : null;
    }
}
