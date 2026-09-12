using MediatR;
using Microsoft.Extensions.Logging;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Modules.Dashboard.Services.Caching;
using RestaurantPos.Api.Modules.Dashboard.Services.Realtime;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Dashboard.Events
{
    /// <summary>
    /// MediatR notification handlers that invalidate the dashboard cache and
    /// push SignalR pings when domain events occur. Domain handlers stay
    /// untouched; this class is the dashboard's listening surface.
    /// </summary>
    /// <remarks>
    /// All work here is wrapped in a top-level try/catch. A failure to
    /// invalidate a dashboard cache or push a SignalR ping must never break
    /// the order-paid flow that originated the event — dashboards are
    /// observability, not business-critical to the transaction.
    /// </remarks>
    public sealed class DashboardInvalidationHandlers
        : INotificationHandler<OrderPaidEvent>,
          INotificationHandler<PaymentMethodAdjustedEvent>
    {
        private readonly IDashboardCacheService    _cache;
        private readonly IDashboardRealtimeService _realtime;
        private readonly ITenantResolver           _tenant;
        private readonly ILogger<DashboardInvalidationHandlers> _logger;

        public DashboardInvalidationHandlers(
            IDashboardCacheService cache,
            IDashboardRealtimeService realtime,
            ITenantResolver tenant,
            ILogger<DashboardInvalidationHandlers> logger)
        {
            _cache    = cache    ?? throw new ArgumentNullException(nameof(cache));
            _realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
            _tenant   = tenant   ?? throw new ArgumentNullException(nameof(tenant));
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Handle(OrderPaidEvent notification, CancellationToken ct)
        {
            try
            {
                var tenantId = SafeTenant();
                if (tenantId == Guid.Empty) return;

                await _cache.InvalidateScopeAsync(tenantId, DashboardScope.Operations, ct);
                await _cache.InvalidateScopeAsync(tenantId, DashboardScope.Financial,  ct);
                await _realtime.NotifyAsync(tenantId, DashboardScope.Operations, ct);
                await _realtime.NotifyAsync(tenantId, DashboardScope.Financial,  ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // shutdown — fine to swallow
            }
            catch (Exception ex)
            {
                // Observability concern only — must NOT bubble back into PaymentService.
                _logger.LogWarning(ex, "Dashboard cache/realtime invalidation skipped for OrderPaidEvent {OrderId}", notification.OrderId);
            }
        }

        public async Task Handle(PaymentMethodAdjustedEvent notification, CancellationToken ct)
        {
            try
            {
                await _cache.InvalidateScopeAsync(notification.TenantId, DashboardScope.Operations, ct);
                await _cache.InvalidateScopeAsync(notification.TenantId, DashboardScope.Financial, ct);
                await _realtime.NotifyAsync(notification.TenantId, DashboardScope.Operations, ct);
                await _realtime.NotifyAsync(notification.TenantId, DashboardScope.Financial, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Dashboard cache/realtime invalidation skipped for PaymentMethodAdjustedEvent {OrderId}",
                    notification.OrderId);
            }
        }

        private Guid SafeTenant()
        {
            try { return _tenant.GetTenantId(); }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Dashboard invalidation skipped — tenant unavailable.");
                return Guid.Empty;
            }
        }
    }
}
