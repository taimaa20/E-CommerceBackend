using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerNotificationService : ICustomerNotificationService
    {
        private const int MaxPageSize = 100;

        private readonly PosDbContext _context;
        private readonly ICustomerMobileContext _mobileContext;

        public CustomerNotificationService(PosDbContext context, ICustomerMobileContext mobileContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
        }

        public async Task<PaginatedResponse<CustomerNotificationDto>> GetNotificationsAsync(
            int page,
            int pageSize,
            CancellationToken ct)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            var customerId = _mobileContext.GetCustomerId();
            var query = _context.Notifications
                .AsNoTracking()
                .Where(n => n.CustomerId == customerId);

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new CustomerNotificationDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    TitleAr = n.TitleAr,
                    Message = n.Message,
                    MessageAr = n.MessageAr,
                    Type = MapNotificationType(n.Type),
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync(ct);

            return new PaginatedResponse<CustomerNotificationDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<CustomerMobileActionResponse> MarkAsReadAsync(Guid id, CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.CustomerId == customerId, ct)
                ?? throw new NotFoundException("Customer notification was not found.");

            notification.IsRead = true;
            AddAuditLog(notification, customerId);
            await _context.SaveChangesAsync(ct);
            return new CustomerMobileActionResponse { Success = true, Message = "Notification marked as read." };
        }

        private void AddAuditLog(Notification notification, Guid customerId)
            => _context.CustomerMobileAuditLogs.Add(new CustomerMobileAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = notification.TenantId,
                CustomerId = customerId,
                NotificationId = notification.Id,
                Action = CustomerMobileAuditAction.NotificationRead,
                ActionAt = DateTime.UtcNow
            });

        private static string MapNotificationType(NotificationType type)
            => type switch
            {
                NotificationType.Loyalty => "Loyalty",
                NotificationType.Promotion => "Promotion",
                NotificationType.General => "General",
                _ => "Order"
            };
    }
}
