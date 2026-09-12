using RestaurantPos.Api.Modules.Dashboard.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Realtime
{
    /// <summary>
    /// Pushes coalesced "invalidate" pings to subscribed dashboard clients.
    /// Implementations MUST debounce per (tenant, scope) so a burst of order
    /// events does not generate a burst of SignalR pushes.
    /// </summary>
    public interface IDashboardRealtimeService
    {
        Task NotifyAsync(Guid tenantId, DashboardScope scope, CancellationToken ct = default);
        Task RaiseAlertAsync(Guid tenantId, DashboardScope scope, object payload, CancellationToken ct = default);
    }
}
