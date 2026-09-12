using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Modules.Purchasing.DTOs;
using RestaurantPos.Api.Modules.Purchasing.Security;
using RestaurantPos.Api.Modules.Purchasing.Services;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Purchasing.Controllers
{
    /// <summary>
    /// The one Purchasing screen reads its list here.
    ///
    /// Writing stays where it already works: raw-material purchases go to
    /// <c>api/procurement/purchases</c>, finished-goods orders to
    /// <c>api/retail/purchase-orders</c>, and the item chosen on the line decides which. This
    /// controller only unifies what the operator reads, which is where the two systems showed.
    /// </summary>
    [ApiController]
    [Route("api/purchasing")]
    [Authorize(Roles = PurchasingRoleGroups.PurchaseOrderViewers)]
    public class PurchasingController : ControllerBase
    {
        private readonly IPurchaseOrderDirectory _directory;
        private readonly IBranchContext _branchContext;

        public PurchasingController(IPurchaseOrderDirectory directory, IBranchContext branchContext)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders(
            CancellationToken ct,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? itemKind = null,
            [FromQuery] string? stage = null,
            [FromQuery] string? paymentStatus = null)
        {
            var context = await _branchContext.GetCurrentAsync(ct);
            var query = new PurchaseOrderDirectoryQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                Search = search,
                ItemKind = itemKind,
                Stage = stage,
                PaymentStatus = paymentStatus
            };

            var page = await _directory.GetPageAsync(
                context.CurrentBranch.Id,
                query,
                PurchasingVisibility.For(User),
                GeneralHelper.IsArabicRequested(Request),
                ct);

            return Ok(page);
        }
    }
}
