using System.Diagnostics;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <inheritdoc cref="IMarketingAuditLogger"/>
    public class MarketingAuditLogger : IMarketingAuditLogger
    {
        private readonly PosDbContext _context;
        private readonly ICurrentUserAccessor _currentUser;

        public MarketingAuditLogger(PosDbContext context, ICurrentUserAccessor currentUser)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public void Track(
            Guid tenantId,
            Guid? branchId,
            MarketingAuditAction action,
            string entityType,
            Guid? entityId,
            Guid? customerId,
            string? metadata = null,
            string? beforeJson = null,
            string? afterJson = null)
        {
            _context.MarketingAuditLogs.Add(new MarketingAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = branchId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                CustomerId = customerId,
                Metadata = metadata,
                BeforeJson = beforeJson,
                AfterJson = afterJson,
                CreatedByUserId = _currentUser.UserIdOrNull,
                CorrelationId = ResolveCorrelationId()
            });
        }

        private static Guid? ResolveCorrelationId()
        {
            var traceId = Activity.Current?.TraceId.ToString();
            return traceId is { Length: 32 } ? new Guid(Convert.FromHexString(traceId)) : null;
        }
    }
}
