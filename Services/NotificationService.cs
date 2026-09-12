using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Hubs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public interface INotificationService
    {
        Task SendAsync(NotificationType type, string title, string message, string? referenceId = null, string? targetRole = null, Guid? tenantId = null, Guid? branchId = null);
    }

    public class NotificationService : INotificationService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<KitchenHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IServiceScopeFactory scopeFactory,
            IHubContext<KitchenHub> hubContext,
            ILogger<NotificationService> logger)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task SendAsync(NotificationType type, string title, string message, string? referenceId = null, string? targetRole = null, Guid? tenantId = null, Guid? branchId = null)
        {
            // 1. Persist to DB
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var tenantResolver = scope.ServiceProvider.GetRequiredService<ITenantResolver>();
            var resolvedTenantId = tenantId ?? tenantResolver.GetTenantId();
            var resolvedBranchId = branchId ?? await ResolveBranchIdAsync(scope.ServiceProvider, context, resolvedTenantId);

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                TenantId = resolvedTenantId,
                BranchId = resolvedBranchId,
                Type = type,
                Title = title,
                Message = message,
                ReferenceId = referenceId,
                TargetRole = targetRole,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            context.Notifications.Add(notification);
            await context.SaveChangesAsync();

            // 2. Broadcast via SignalR
            var payload = new
            {
                notification.Id,
                Type = type.ToString(),
                notification.Title,
                notification.Message,
                notification.ReferenceId,
                notification.TargetRole,
                notification.IsRead,
                notification.CreatedAt
            };

            await _hubContext.Clients
                .Group(KitchenHubGroups.Branch(resolvedBranchId))
                .SendAsync(KitchenHubEvents.ReceiveNotification, payload);
            _logger.LogInformation("[Notification] {Type}: {Title} → {Role}", type, title, targetRole ?? "All");
        }

        private async Task<Guid> ResolveBranchIdAsync(IServiceProvider services, PosDbContext context, Guid tenantId)
        {
            try
            {
                var branchContext = services.GetRequiredService<IBranchContext>();
                var current = await branchContext.GetCurrentAsync();
                if (current.TenantId == tenantId)
                    return current.CurrentBranch.Id;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Notification branch context unavailable; falling back to Main Branch.");
            }

            return await context.Branches
                .IgnoreQueryFilters()
                .Where(branch => branch.TenantId == tenantId && branch.IsMainBranch && branch.DeletedAt == null)
                .Select(branch => branch.Id)
                .FirstAsync();
        }
    }
}
