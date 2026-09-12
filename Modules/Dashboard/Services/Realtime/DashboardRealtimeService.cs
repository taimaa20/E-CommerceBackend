using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RestaurantPos.Api.Modules.Dashboard.Hubs;
using RestaurantPos.Api.Modules.Dashboard.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Realtime
{
    /// <inheritdoc />
    /// <remarks>
    /// Coalesces invalidate pings using a per-(tenant, scope) lock-free
    /// trailing-edge debouncer. The first event schedules a delayed flush;
    /// subsequent events inside the debounce window are absorbed for free.
    /// This keeps the hub responsive under high-throughput POS activity
    /// (50 order updates / second) without blowing up the client.
    /// </remarks>
    public sealed class DashboardRealtimeService : IDashboardRealtimeService
    {
        private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(800);

        private readonly IHubContext<DashboardHub> _hub;
        private readonly ILogger<DashboardRealtimeService> _logger;
        private readonly ConcurrentDictionary<(Guid, DashboardScope), DateTime> _pending = new();

        public DashboardRealtimeService(IHubContext<DashboardHub> hub, ILogger<DashboardRealtimeService> logger)
        {
            _hub    = hub    ?? throw new ArgumentNullException(nameof(hub));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task NotifyAsync(Guid tenantId, DashboardScope scope, CancellationToken ct = default)
        {
            var key   = (tenantId, scope);
            var until = DateTime.UtcNow.Add(DebounceWindow);

            // If a flush is already pending, refresh its deadline and bail.
            if (!_pending.TryAdd(key, until))
            {
                _pending[key] = until;
                return Task.CompletedTask;
            }

            _ = ScheduleFlushAsync(key, ct);
            return Task.CompletedTask;
        }

        public Task RaiseAlertAsync(Guid tenantId, DashboardScope scope, object payload, CancellationToken ct = default)
        {
            return _hub.Clients
                .Group(DashboardHub.GroupName(tenantId, scope))
                .SendAsync(DashboardEventNames.AlertRaised, payload, ct);
        }

        private async Task ScheduleFlushAsync((Guid tenantId, DashboardScope scope) key, CancellationToken ct)
        {
            try
            {
                while (_pending.TryGetValue(key, out var until))
                {
                    var wait = until - DateTime.UtcNow;
                    if (wait <= TimeSpan.Zero) break;
                    await Task.Delay(wait, ct);
                }

                _pending.TryRemove(key, out _);

                var evt = key.scope switch
                {
                    DashboardScope.Operations => DashboardEventNames.OperationsInvalidated,
                    DashboardScope.Financial  => DashboardEventNames.FinancialInvalidated,
                    DashboardScope.Inventory  => DashboardEventNames.InventoryInvalidated,
                    DashboardScope.Audit      => DashboardEventNames.AuditInvalidated,
                    _ => DashboardEventNames.OperationsInvalidated
                };

                await _hub.Clients
                    .Group(DashboardHub.GroupName(key.tenantId, key.scope))
                    .SendAsync(evt, cancellationToken: ct);
            }
            catch (OperationCanceledException) { /* shutdown */ }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DashboardRealtimeService flush failed for {Tenant}/{Scope}", key.tenantId, key.scope);
            }
        }
    }
}
