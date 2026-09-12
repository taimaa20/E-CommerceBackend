using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    /// <inheritdoc cref="IConfigAuditService"/>
    public class ConfigAuditService : IConfigAuditService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        private readonly PosDbContext _context;
        private readonly ICurrentUserAccessor _user;
        private readonly ILogger<ConfigAuditService> _logger;

        public ConfigAuditService(
            PosDbContext context,
            ICurrentUserAccessor user,
            ILogger<ConfigAuditService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _user    = user    ?? throw new ArgumentNullException(nameof(user));
            _logger  = logger  ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task LogAsync(
            Guid tenantId,
            ConfigAuditEventType eventType,
            object? previousValue,
            object? newValue,
            Guid? targetId = null,
            string? branchCode = null,
            string? reason = null,
            CancellationToken ct = default)
        {
            if (tenantId == Guid.Empty) return;

            try
            {
                var entry = new ConfigAuditLog
                {
                    Id                = Guid.NewGuid(),
                    TenantId          = tenantId,
                    EventType         = eventType,
                    ChangedByUserId   = _user.UserIdOrNull,
                    ChangedByUserName = await ResolveUserNameAsync(_user.UserIdOrNull, ct),
                    TargetId          = targetId,
                    BranchCode        = branchCode,
                    PreviousValue     = Serialize(previousValue),
                    NewValue          = Serialize(newValue),
                    Reason            = reason,
                };

                _context.ConfigAuditLogs.Add(entry);
                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Never break the caller's transaction for an audit-write failure.
                _logger.LogWarning(ex,
                    "Failed to write ConfigAuditLog entry (event={EventType}, tenant={TenantId}).",
                    eventType, tenantId);
            }
        }

        public async Task<List<ConfigAuditLog>> GetRecentAsync(
            Guid tenantId,
            ConfigAuditEventType? eventType,
            int limit,
            CancellationToken ct)
        {
            if (tenantId == Guid.Empty) return new List<ConfigAuditLog>();

            limit = Math.Clamp(limit, 1, 500);

            var q = _context.ConfigAuditLogs
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId);

            if (eventType.HasValue)
                q = q.Where(c => c.EventType == eventType.Value);

            return await q
                .OrderByDescending(c => c.CreatedAt)
                .Take(limit)
                .ToListAsync(ct);
        }

        private async Task<string?> ResolveUserNameAsync(Guid? userId, CancellationToken ct)
        {
            if (!userId.HasValue || userId.Value == Guid.Empty) return null;
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId.Value)
                .Select(u => u.FullName ?? u.Username)
                .FirstOrDefaultAsync(ct);
        }

        private static string? Serialize(object? value)
        {
            if (value is null) return null;
            try { return JsonSerializer.Serialize(value, JsonOpts); }
            catch { return null; }
        }
    }
}
