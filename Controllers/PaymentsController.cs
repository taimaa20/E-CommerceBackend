using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ICurrentUserResolver _currentUserResolver;

        public PaymentsController(
            IPaymentService paymentService,
            ICurrentUserResolver currentUserResolver)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _currentUserResolver = currentUserResolver ?? throw new ArgumentNullException(nameof(currentUserResolver));
        }

        [HttpPost]
        [Authorize(Roles = AppRoleGroups.TakeawayCheckoutOperators)]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = await _currentUserResolver.ResolveUserIdAsync(User);
            var userRole = ResolveUserRole();

            try
            {
                var result = await _paymentService.AddPaymentAsync(request, userId, userRole, cancellationToken);
                return Ok(MapResult(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("The order was modified by another request. Please retry.");
            }
        }

        private OrderPaymentResultDto MapResult(PaymentProcessResult result)
        {
            var snapshot = OrderPaymentHelper.BuildSnapshot(result.Order);

            return new OrderPaymentResultDto
            {
                OrderId = result.Order.Id,
                TotalAmount = result.Order.TotalAmount,
                PaidAmount = snapshot.PaidAmount,
                RemainingAmount = snapshot.RemainingAmount,
                PaymentStatus = snapshot.PaymentStatus,
                IsPaid = snapshot.IsPaid,
                Status = result.Order.Status.ToString(),
                PaymentMethod = snapshot.PaymentMethod,
                AmountTendered = snapshot.AmountTendered,
                ChangeAmount = snapshot.ChangeAmount,
                IsVoucherApplied = result.Order.IsVoucherApplied,
                VoucherDiscountAmount = result.Order.VoucherDiscountAmount,
                VoucherAppliedAt = result.Order.VoucherAppliedAt,
                VoucherRemainingToday = result.VoucherRemainingToday,
                CostSharingTotalCommission = result.Order.CostSharingTotalCommission,
                CostSharingRestaurantShare = result.Order.CostSharingRestaurantShare,
                CostSharingCounterpartyShare = result.Order.CostSharingCounterpartyShare,
                CostSharingNetSettlement = result.Order.CostSharingNetSettlement,
                Payment = OrderPaymentHelper.MapPayment(result.Payment),
                Payments = snapshot.Payments
            };
        }

        private UserRole ResolveUserRole()
        {
            var roleClaim = User.Claims.FirstOrDefault(c =>
                c.Type == "role" ||
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

            return Enum.TryParse<UserRole>(roleClaim?.Value, out var role)
                ? role
                : UserRole.Waiter;
        }
    }
}
