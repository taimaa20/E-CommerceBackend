using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Controllers
{
    /// <summary>Staff/admin view of a customer's loyalty wallet, profile and history.</summary>
    [ApiController]
    [Route("api/v1/marketing/wallets")]
    [Authorize(Roles = AppRoleGroups.CashierOperators)]
    public sealed class MarketingWalletController : ControllerBase
    {
        private readonly IWalletQueryService _query;
        private readonly IWalletService _wallet;

        public MarketingWalletController(IWalletQueryService query, IWalletService wallet)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        [HttpGet("search")]
        public async Task<ActionResult<PaginatedResponse<WalletSearchItemDto>>> Search(
            [FromQuery] string? term, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
            => Ok(await _query.SearchAsync(term, page, pageSize, GeneralHelper.IsArabicRequested(Request), ct));

        [HttpGet("{customerId:guid}")]
        public async Task<ActionResult<WalletDto>> GetWallet(Guid customerId, CancellationToken ct)
        {
            var wallet = await _query.GetWalletAsync(customerId, ct);
            return wallet is null ? NotFound() : Ok(wallet);
        }

        [HttpGet("{customerId:guid}/profile")]
        public async Task<ActionResult<LoyaltyProfileDto>> GetProfile(Guid customerId, CancellationToken ct)
        {
            var profile = await _query.GetProfileAsync(customerId, GeneralHelper.IsArabicRequested(Request), ct);
            return profile is null ? NotFound() : Ok(profile);
        }

        [HttpGet("{customerId:guid}/history")]
        public async Task<ActionResult<PaginatedResponse<WalletTransactionDto>>> GetHistory(
            Guid customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
            => Ok(await _query.GetHistoryAsync(customerId, page, pageSize, ct));

        [HttpPost("{customerId:guid}/freeze")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> Freeze(Guid customerId, [FromBody] WalletFreezeRequest request, CancellationToken ct)
        {
            await _query.EnsureCustomerInCurrentTenantAsync(customerId, ct);
            var wallet = await _wallet.GetByCustomerIdAsync(customerId, ct);
            if (wallet is null) return NotFound();
            await _wallet.FreezeAsync(wallet.Id, request?.Reason ?? "Frozen by admin", ct);
            return Ok(new { message = "Wallet frozen." });
        }

        [HttpPost("{customerId:guid}/unfreeze")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> Unfreeze(Guid customerId, CancellationToken ct)
        {
            await _query.EnsureCustomerInCurrentTenantAsync(customerId, ct);
            var wallet = await _wallet.GetByCustomerIdAsync(customerId, ct);
            if (wallet is null) return NotFound();
            await _wallet.UnfreezeAsync(wallet.Id, ct);
            return Ok(new { message = "Wallet unfrozen." });
        }

        /// <summary>Manual points adjustment (add/deduct). Admin-only; mandatory reason; fully audited.</summary>
        [HttpPost("{customerId:guid}/adjust")]
        [Authorize(Roles = AppRoleGroups.AdminStrict)]
        public async Task<ActionResult<WalletDto>> Adjust(
            Guid customerId, [FromBody] WalletAdjustRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            await _query.EnsureCustomerInCurrentTenantAsync(customerId, ct);
            var signed = request.Direction == WalletAdjustmentDirection.Deduct ? -request.Points : request.Points;
            await _wallet.AdjustAsync(customerId, signed, request.Reason, request.ReasonAr, ct);

            var wallet = await _query.GetWalletAsync(customerId, ct);
            return wallet is null ? NotFound() : Ok(wallet);
        }

        public sealed class WalletFreezeRequest
        {
            public string? Reason { get; set; }
        }
    }
}
