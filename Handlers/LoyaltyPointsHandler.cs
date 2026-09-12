using MediatR;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Handlers
{
    public class LoyaltyPointsHandler : INotificationHandler<OrderPaidEvent>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LoyaltyPointsHandler> _logger;

        public LoyaltyPointsHandler(IServiceScopeFactory scopeFactory, ILogger<LoyaltyPointsHandler> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task Handle(OrderPaidEvent notification, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PosDbContext>();

            var order = await context.Orders
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == notification.OrderId, cancellationToken);

            if (order == null)
                return;

            // Per-tenant kill-switch. When delegation is on, the configurable rules engine owns
            // earning (and can resolve/auto-create the customer by phone). When off, the legacy
            // flat tier-rate path runs unchanged — so the cutover is reversible with no regression.
            var delegationEnabled = await context.MarketingSettings
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == order.TenantId)
                .Select(s => (bool?)s.EarnDelegationEnabled)
                .FirstOrDefaultAsync(cancellationToken) ?? false;

            if (delegationEnabled)
            {
                var engine = scope.ServiceProvider.GetRequiredService<IEarningEngine>();
                await engine.ApplyEarningForOrderAsync(order.Id, cancellationToken);
                return;
            }

            // Legacy path (unchanged behavior).
            if (order.CustomerId.HasValue)
            {
                _logger.LogInformation("[Loyalty] Processing points for Order {OrderNumber}, Customer {CustomerId}",
                    order.OrderNumber, order.CustomerId);
                var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
                await customerService.AddLoyaltyPointsAsync(order.CustomerId.Value, notification.TotalAmount);
            }
        }
    }
}
