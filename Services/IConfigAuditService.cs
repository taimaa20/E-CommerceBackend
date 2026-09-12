using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Append-only audit trail for configuration and shift lifecycle events.
    /// Failures here MUST NOT block the caller — implementations swallow + log
    /// any persistence exception, since dropping an audit row is preferable to
    /// dropping a shift open/close or settings save.
    /// </summary>
    public interface IConfigAuditService
    {
        Task LogAsync(
            Guid tenantId,
            ConfigAuditEventType eventType,
            object? previousValue,
            object? newValue,
            Guid? targetId = null,
            string? branchCode = null,
            string? reason = null,
            CancellationToken ct = default);

        Task<List<ConfigAuditLog>> GetRecentAsync(
            Guid tenantId,
            ConfigAuditEventType? eventType,
            int limit,
            CancellationToken ct);
    }
}
