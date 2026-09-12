using Microsoft.AspNetCore.SignalR;
using RestaurantPos.Api.Modules.Dashboard.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Hubs
{
    /// <summary>
    /// Dashboard SignalR hub. Clients join a per-tenant per-scope group so
    /// "OrderPaid" only invalidates the financial+operations dashboards of
    /// the tenant it belongs to.
    /// </summary>
    /// <remarks>
    /// Intentionally NOT <c>[Authorize]</c> — matches the existing
    /// <see cref="Hubs.KitchenHub"/> pattern. The hub itself only carries
    /// fire-and-forget "your data is stale, refetch" pings; the real data
    /// is always served by JWT-protected GET endpoints. Adding [Authorize]
    /// here without extending the JWT bearer config to read the token from
    /// the SignalR query string would break the EventSource fallback.
    ///
    /// Tenant is supplied by the client via <see cref="JoinScope"/> rather
    /// than derived from <see cref="Services.ITenantResolver"/>, because the
    /// SignalR connection's HttpContext may have already been disposed by
    /// the time invocation methods are called (transports like long-polling
    /// re-establish the connection without the original headers).
    /// </remarks>
    public sealed class DashboardHub : Hub
    {
        /// <summary>
        /// Subscribe to a tenant+scope channel. The tenantId comes from the
        /// authenticated client's stored tenant — the server treats it as an
        /// opaque routing key (no permission decision is made on its value).
        /// </summary>
        public Task JoinScope(string tenantId, DashboardScope scope) =>
            Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tenantId, scope));

        public Task LeaveScope(string tenantId, DashboardScope scope) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tenantId, scope));

        public static string GroupName(Guid tenantId, DashboardScope scope) =>
            $"dash:{tenantId:N}:{scope}";

        public static string GroupName(string tenantId, DashboardScope scope)
        {
            // Normalise: accept both hyphenated and no-hyphen GUIDs from the
            // client, route to the canonical no-hyphen form used server-side.
            if (Guid.TryParse(tenantId, out var g))
                return GroupName(g, scope);
            return $"dash:{tenantId}:{scope}";
        }
    }
}
