using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    /// <summary>
    /// Append-only audit trail for every marketing mutation (who/when/what, before→after).
    /// Actor is <see cref="MarketingBaseEntity.CreatedByUserId"/> (null = system).
    /// </summary>
    public class MarketingAuditLog : MarketingBaseEntity
    {
        public MarketingAuditAction Action { get; set; }

        [Required, MaxLength(60)] public string EntityType { get; set; } = string.Empty;
        public Guid? EntityId { get; set; }

        public Guid? CustomerId { get; set; }

        /// <summary>State before the change (jsonb). Null for creates.</summary>
        public string? BeforeJson { get; set; }

        /// <summary>State after the change (jsonb). Null for deletes.</summary>
        public string? AfterJson { get; set; }

        [MaxLength(500)] public string? Metadata { get; set; }

        /// <summary>Trace/Activity id for cross-request correlation.</summary>
        public Guid? CorrelationId { get; set; }
    }
}
