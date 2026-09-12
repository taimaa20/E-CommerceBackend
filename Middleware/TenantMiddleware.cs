using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Middleware
{
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITenantResolver tenantResolver)
        {
            // The TenantResolver will extract tenant from header or subdomain
            // We don't need to do anything here except ensure it's resolved early
            // The resolver is already injected into DbContext
            
            // Optional: You could validate tenant exists and is active here
            // For now, we'll let the resolver handle it
            
            await _next(context);
        }
    }
}
