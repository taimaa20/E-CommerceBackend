using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    /// <summary>
    /// Base for every Marketing-module entity. Extends the global <see cref="BaseEntity"/>
    /// (Id/TenantId/CreatedAt/UpdatedAt/DeletedAt) without modifying it, adding the
    /// branch-scoping, audit-user and optimistic-concurrency columns the module requires.
    /// </summary>
    public abstract class MarketingBaseEntity : BaseEntity
    {
        /// <summary>Null = applies to all branches; a value = single branch (franchise scoping).</summary>
        public Guid? BranchId { get; set; }

        /// <summary>User that created the row. Null = system actor (event-driven earn, background job).</summary>
        public Guid? CreatedByUserId { get; set; }

        /// <summary>User that last updated the row. Null = system actor.</summary>
        public Guid? UpdatedByUserId { get; set; }

        /// <summary>
        /// Optimistic-concurrency token. Mapped to the PostgreSQL <c>xmin</c> system column,
        /// so it consumes no stored column. Configured in <c>MarketingModelBuilderExtensions</c>.
        /// </summary>
        public uint RowVersion { get; set; }
    }
}
