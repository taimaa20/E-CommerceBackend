using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/payment-adjustments")]
    public sealed class PaymentAdjustmentsController : ControllerBase
    {
        private readonly IPaymentAdjustmentService _service;
        private readonly ICurrentUserResolver _currentUserResolver;

        public PaymentAdjustmentsController(
            IPaymentAdjustmentService service,
            ICurrentUserResolver currentUserResolver)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _currentUserResolver = currentUserResolver ?? throw new ArgumentNullException(nameof(currentUserResolver));
        }

        [HttpGet("approvers")]
        [Authorize(Roles = AppRoleGroups.PaymentChangeRequesters)]
        public async Task<ActionResult<IReadOnlyList<PaymentAdjustmentApproverDto>>> GetApprovers(
            CancellationToken ct)
        {
            var requesterId = await ResolveUserIdAsync();
            return Ok(await _service.GetApproversAsync(requesterId, ct));
        }

        [HttpPost("orders/{orderId:guid}")]
        [Authorize(Roles = AppRoleGroups.PaymentChangeRequesters)]
        public async Task<ActionResult<PaymentAdjustmentResultDto>> RequestAdjustment(
            Guid orderId,
            [FromBody] PaymentAdjustmentCreateDto request,
            CancellationToken ct)
        {
            var result = await _service.RequestAsync(
                orderId,
                request,
                await ResolveUserIdAsync(),
                ResolveRole(),
                ct);
            return Ok(result);
        }

        [HttpGet("orders/{orderId:guid}/requests/{adjustmentId:guid}")]
        [Authorize(Roles = AppRoleGroups.PaymentChangeApprovers)]
        public async Task<ActionResult<PaymentAdjustmentReviewDto>> GetPending(
            Guid orderId,
            Guid adjustmentId,
            CancellationToken ct)
        {
            return Ok(await _service.GetPendingAsync(orderId, adjustmentId, ct));
        }

        [HttpPost("orders/{orderId:guid}/requests/{adjustmentId:guid}/approve")]
        [Authorize(Roles = AppRoleGroups.PaymentChangeApprovers)]
        public async Task<ActionResult<PaymentAdjustmentResultDto>> Approve(
            Guid orderId,
            Guid adjustmentId,
            [FromBody] PaymentAdjustmentDecisionDto request,
            CancellationToken ct)
        {
            return Ok(await _service.ApproveAsync(
                orderId,
                adjustmentId,
                request,
                await ResolveUserIdAsync(),
                ResolveRole(),
                ct));
        }

        [HttpPost("orders/{orderId:guid}/requests/{adjustmentId:guid}/reject")]
        [Authorize(Roles = AppRoleGroups.PaymentChangeApprovers)]
        public async Task<IActionResult> Reject(
            Guid orderId,
            Guid adjustmentId,
            [FromBody] PaymentAdjustmentDecisionDto request,
            CancellationToken ct)
        {
            await _service.RejectAsync(
                orderId,
                adjustmentId,
                request,
                await ResolveUserIdAsync(),
                ResolveRole(),
                ct);
            return NoContent();
        }

        [HttpGet("history")]
        [Authorize(Roles = AppRoleGroups.PaymentAdjustmentViewers)]
        public async Task<ActionResult<PaginatedResponse<PaymentAdjustmentHistoryDto>>> GetHistory(
            [FromQuery] PaymentAdjustmentReportFilterDto filter,
            CancellationToken ct)
        {
            return Ok(await _service.GetHistoryAsync(filter, ct));
        }

        private async Task<Guid> ResolveUserIdAsync()
        {
            var userId = await _currentUserResolver.ResolveUserIdAsync(User);
            return userId ?? Guid.Empty;
        }

        private UserRole ResolveRole()
        {
            var value = User.FindFirstValue(ClaimTypes.Role)
                ?? User.FindFirstValue("role");
            return Enum.TryParse<UserRole>(value, true, out var role)
                ? role
                : UserRole.Waiter;
        }
    }
}
