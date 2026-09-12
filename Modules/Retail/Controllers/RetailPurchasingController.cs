using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Retail.Domain;
using RestaurantPos.Api.Modules.Retail.DTOs;
using RestaurantPos.Api.Modules.Retail.Security;
using RestaurantPos.Api.Modules.Retail.Services;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using System.Security.Claims;

namespace RestaurantPos.Api.Modules.Retail.Controllers
{
    /// <summary>
    /// Operational retail purchasing: SKU-level purchase orders against the SHARED supplier
    /// master, and the receipt that raises finished-goods stock. The unified Purchasing screen
    /// calls this for retail branches and the restaurant procurement endpoints for the rest —
    /// the two never overlap, because a line here must be a product with retail details.
    /// </summary>
    [ApiController]
    [Route("api/retail/purchase-orders")]
    [Authorize(Roles = RetailRoleGroups.RetailPurchasingOperators)]
    public class RetailPurchasingController : ControllerBase
    {
        private readonly IRetailPurchasingService _purchasing;
        private readonly IBranchContext _branchContext;

        public RetailPurchasingController(
            IRetailPurchasingService purchasing,
            IBranchContext branchContext)
        {
            _purchasing = purchasing ?? throw new ArgumentNullException(nameof(purchasing));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        }

        [HttpGet]
        public async Task<IActionResult> GetPage(
            CancellationToken ct,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null,
            [FromQuery] Guid? supplierId = null,
            [FromQuery] RetailPurchaseOrderStatus? status = null)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var query = new RetailPurchaseOrderQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                Search = search,
                SupplierId = supplierId,
                Status = status
            };

            return Ok(await _purchasing.GetPageAsync(branchId, query, IsArabicRequested(Request), ct));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
            => Ok(await _purchasing.GetAsync(id, IsArabicRequested(Request), ct));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RetailPurchaseOrderCreateDto input, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var branchId = await GetCurrentBranchIdAsync(ct);
            var created = await _purchasing.CreateAsync(branchId, input, GetCurrentUserId(), IsArabicRequested(Request), ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] RetailPurchaseOrderCreateDto input, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            return Ok(await _purchasing.UpdateAsync(id, input, IsArabicRequested(Request), ct));
        }

        [HttpPost("{id:guid}/submit")]
        public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
            => Ok(await _purchasing.SubmitAsync(id, IsArabicRequested(Request), ct));

        /// <summary>
        /// Confirms the goods arrived. Posts one stock movement per received line and rolls the
        /// landed cost into the product. Replaying this call returns the same order untouched.
        /// </summary>
        [HttpPost("{id:guid}/receive")]
        public async Task<IActionResult> Receive(
            Guid id,
            [FromBody] RetailPurchaseReceiptDto? receipt,
            CancellationToken ct)
        {
            var received = await _purchasing.ReceiveAsync(
                id,
                receipt ?? new RetailPurchaseReceiptDto(),
                GetCurrentUserId(),
                IsArabicRequested(Request),
                ct);

            return Ok(received);
        }

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
            => Ok(await _purchasing.CancelAsync(id, IsArabicRequested(Request), ct));

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
        {
            var context = await _branchContext.GetCurrentAsync(ct);
            return context.CurrentBranch.Id;
        }

        private Guid? GetCurrentUserId()
            => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private static bool IsArabicRequested(HttpRequest request)
            => request.Headers["Accept-Language"].ToString()
                .StartsWith("ar", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Finished-goods stock behind the one Inventory area: the movement history, and the
    /// manual adjustment that is the only way a human can change a position directly.
    /// </summary>
    [ApiController]
    [Route("api/retail/stock")]
    [Authorize(Roles = RetailRoleGroups.RetailStockViewers)]
    public class RetailStockController : ControllerBase
    {
        private const int DefaultMovementLimit = 100;

        private readonly IRetailPurchasingService _purchasing;
        private readonly IRetailStockAdjustmentService _adjustments;
        private readonly IBranchContext _branchContext;

        public RetailStockController(
            IRetailPurchasingService purchasing,
            IRetailStockAdjustmentService adjustments,
            IBranchContext branchContext)
        {
            _purchasing = purchasing ?? throw new ArgumentNullException(nameof(purchasing));
            _adjustments = adjustments ?? throw new ArgumentNullException(nameof(adjustments));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        }

        /// <summary>
        /// Movement history for one product, newest first, with a running balance.
        /// Defaults to the ACTIVE branch, because stock is held per branch and the running
        /// balance is meaningless summed across branches. Pass an explicit branch to override.
        /// </summary>
        [HttpGet("movements/{productId:guid}")]
        public async Task<IActionResult> GetMovements(
            Guid productId,
            CancellationToken ct,
            [FromQuery] Guid? branchId = null,
            [FromQuery] int limit = DefaultMovementLimit)
        {
            var scope = branchId ?? (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
            return Ok(await _purchasing.GetMovementsAsync(productId, scope, limit, ct));
        }

        /// <summary>
        /// Corrects the product's stock on the active branch by posting one adjustment
        /// movement. Same grant as the raw-material equivalent, so "adjust stock" means the
        /// same thing for both inventory types.
        /// </summary>
        [HttpPost("{productId:guid}/adjustments")]
        [Authorize(Roles = RetailRoleGroups.RetailStockAdjusters)]
        public async Task<IActionResult> Adjust(
            Guid productId,
            [FromBody] RetailStockAdjustmentCreateDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            return Ok(await _adjustments.AdjustAsync(productId, dto, ct));
        }

        [HttpGet("{productId:guid}/adjustments")]
        [Authorize(Roles = RetailRoleGroups.RetailStockAdjusters)]
        public async Task<IActionResult> GetAdjustments(Guid productId, CancellationToken ct)
            => Ok(await _adjustments.GetHistoryAsync(productId, ct));
    }
}
