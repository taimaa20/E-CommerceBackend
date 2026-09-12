using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using System.Security.Claims;

namespace RestaurantPos.Api.Services
{
    public interface ITenantResolver
    {
        Guid GetTenantId();
    }

    public class HeaderTenantResolver : ITenantResolver
    {
        private const string TenantHeaderName = "X-Tenant-ID";
        private const string TenantClaimName = "tenant_id";
        private static readonly Guid FallbackTenantId = SeedData.DefaultTenantId;

        private readonly IHttpContextAccessor _httpContextAccessor;

        public HeaderTenantResolver(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public Guid GetTenantId()
        {
            var context = _httpContextAccessor.HttpContext;
            Guid headerTenantId = default;
            Guid claimTenantId = default;
            var hasHeaderTenant = context?.Request.Headers.TryGetValue(TenantHeaderName, out var tenantIdValue) == true
                && Guid.TryParse(tenantIdValue, out headerTenantId);
            var hasClaimTenant = Guid.TryParse(context?.User.FindFirstValue(TenantClaimName), out claimTenantId);

            if (context?.User.Identity?.IsAuthenticated == true && hasClaimTenant)
            {
                if (hasHeaderTenant && headerTenantId != claimTenantId)
                    throw new UnauthorizedException("Tenant header does not match the authenticated tenant.");

                return claimTenantId;
            }

            if (hasHeaderTenant)
                return headerTenantId;

            if (hasClaimTenant)
            {
                return claimTenantId;
            }

            if (CanUseFallbackTenant(context))
                return FallbackTenantId;

            throw new InvalidOperationException(
                $"Tenant resolution failed: {TenantHeaderName} header is missing or not a valid GUID.");
        }

        private static bool CanUseFallbackTenant(HttpContext? context)
        {
            if (context == null)
                return true;

            return context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() != null;
        }
    }
}
