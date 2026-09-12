using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/config-audit")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.ConfigAuditViewers)]
    public class ConfigAuditController : ControllerBase
    {
        private readonly IConfigAuditService _service;
        private readonly ITenantResolver _tenantResolver;

        public ConfigAuditController(
            IConfigAuditService service,
            ITenantResolver tenantResolver)
        {
            _service        = service        ?? throw new ArgumentNullException(nameof(service));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        public class ConfigAuditEntryDto
        {
            public Guid     Id                { get; set; }
            public string   EventType         { get; set; } = string.Empty;
            public Guid?    ChangedByUserId   { get; set; }
            public string?  ChangedByUserName { get; set; }
            public Guid?    TargetId          { get; set; }
            public string?  BranchCode        { get; set; }
            public string?  PreviousValue     { get; set; }
            public string?  NewValue          { get; set; }
            public string?  Reason            { get; set; }
            public DateTime CreatedAtUtc      { get; set; }
        }

        [HttpGet]
        public async Task<ActionResult<List<ConfigAuditEntryDto>>> List(
            [FromQuery] string? eventType,
            [FromQuery] int limit = 100,
            CancellationToken ct = default)
        {
            ConfigAuditEventType? typed = null;
            if (!string.IsNullOrWhiteSpace(eventType)
                && Enum.TryParse<ConfigAuditEventType>(eventType, true, out var parsed))
            {
                typed = parsed;
            }

            var tenantId = _tenantResolver.GetTenantId();
            var rows = await _service.GetRecentAsync(tenantId, typed, limit, ct);

            return Ok(rows.Select(r => new ConfigAuditEntryDto
            {
                Id                = r.Id,
                EventType         = r.EventType.ToString(),
                ChangedByUserId   = r.ChangedByUserId,
                ChangedByUserName = r.ChangedByUserName,
                TargetId          = r.TargetId,
                BranchCode        = r.BranchCode,
                PreviousValue     = r.PreviousValue,
                NewValue          = r.NewValue,
                Reason            = r.Reason,
                CreatedAtUtc      = r.CreatedAt,
            }).ToList());
        }
    }
}
