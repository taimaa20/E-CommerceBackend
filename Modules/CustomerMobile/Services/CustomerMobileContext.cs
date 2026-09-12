using System.Security.Claims;
using RestaurantPos.Api.Exceptions;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerMobileContext : ICustomerMobileContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomerMobileContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public Guid GetCustomerId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var customerId)
                ? customerId
                : throw new UnauthorizedException("Invalid customer session.");
        }

        public Guid GetTenantId()
        {
            var value = User.FindFirstValue("tenant_id");
            return Guid.TryParse(value, out var tenantId)
                ? tenantId
                : throw new UnauthorizedException("Invalid customer tenant.");
        }

        private ClaimsPrincipal User =>
            _httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedException("Customer session is required.");
    }
}
