using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.PosOrderEditors)]
    [EnableRateLimiting("api")]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly ITenantResolver _tenantResolver;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(
            ICustomerService customerService,
            ITenantResolver tenantResolver,
            ILogger<CustomersController> logger)
        {
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _tenantResolver  = tenantResolver  ?? throw new ArgumentNullException(nameof(tenantResolver));
            _logger          = logger          ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string phone, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest(new { message = "Phone is required" });
            }

            var isArabic = GeneralHelper.IsArabicRequested(Request);
            var customer = await _customerService.GetCustomerByPhoneAsync(phone);
            if (customer == null) return NotFound("Müşteri bulunamadı");

            return Ok(BuildResponse(customer, isArabic));
        }

        [HttpGet("{id}/insights")]
        public async Task<IActionResult> GetInsights(Guid id, CancellationToken cancellationToken)
        {
            var insights = await _customerService.GetCustomerInsightsAsync(id);
            if (insights == null) return NotFound();
            return Ok(insights);
        }

        [HttpPost]
        [Authorize(Roles = AppRoleGroups.CashierOperators)]
        public async Task<IActionResult> Create([FromBody] CustomerCreateDto input, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var tenantId = _tenantResolver.GetTenantId();
            var isArabic = GeneralHelper.IsArabicRequested(Request);

            var customer = await _customerService.CreateAsync(tenantId, input, cancellationToken);
            _logger.LogInformation("Customer {CustomerId} created in tenant {TenantId}", customer.Id, tenantId);

            return Ok(BuildResponse(customer, isArabic));
        }

        [HttpPost("{id}/redeem")]
        [Authorize(Roles = AppRoleGroups.CashierOperators)]
        public async Task<IActionResult> RedeemPoints(Guid id, [FromQuery] Guid orderId, CancellationToken cancellationToken)
        {
            var success = await _customerService.RedeemPointsAsync(id, orderId);
            if (!success)
            {
                _logger.LogWarning("Redeem points failed for customer {CustomerId} on order {OrderId}", id, orderId);
                return BadRequest("Point usage failed (Insufficient points or order not found)");
            }

            _logger.LogInformation("Customer {CustomerId} redeemed points on order {OrderId}", id, orderId);
            return Ok(new
            {
                success = true,
                message = "Points converted to discount successfully"
            });
        }

        // Preserves the previous anonymous-object shape (Id, Name, NameAr, DisplayName,
        // PhoneNumber, Email, BirthDate, Tier, LoyaltyPoints, LastVisit) for the frontend.
        private static CustomerDto BuildResponse(Customer customer, bool isArabic) => new()
        {
            Id = customer.Id,
            Name = customer.Name,
            NameAr = customer.NameAr,
            DisplayName = GeneralHelper.ResolveDisplayName(customer.Name, customer.NameAr, isArabic),
            PhoneNumber = customer.PhoneNumber,
            Email = customer.Email,
            BirthDate = customer.BirthDate,
            Tier = customer.Tier,
            LoyaltyPoints = customer.LoyaltyPoints,
            LastVisit = customer.LastVisit
        };
    }
}
