using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    /// <summary>
    /// Who absorbs the delivery fee for an order placed in a given zone.
    /// Append-only enum — never reorder or repurpose existing values (order snapshots store the int).
    /// </summary>
    public enum DeliveryPaymentMode
    {
        CustomerPays = 0,
        RestaurantPays = 1,
        Shared = 2
    }

    /// <summary>
    /// A delivery area/zone with its own fee + internal cost. Tenant-scoped and soft-deletable
    /// via <see cref="BaseEntity"/>. Fee/cost are charged at O(1) from a cached active-zone list;
    /// orders persist a snapshot of these values so historical orders are immune to later edits.
    ///
    /// Future-proofing (drivers, distance/time pricing, 3rd-party providers) attaches here as new
    /// nullable columns + new services without touching existing order or receipt logic.
    /// </summary>
    public class DeliveryZone : BaseEntity
    {
        /// <summary>Owning branch — every branch serves its own geographical zones.</summary>
        public Guid BranchId { get; set; }
        public Branch? Branch { get; set; }

        /// <summary>English / default display name (paired with <see cref="NameAr"/> per the bilingual convention).</summary>
        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? NameAr { get; set; }

        /// <summary>Short operator-facing code, unique per tenant (filtered, case-insensitive in the service).</summary>
        [Required]
        [MaxLength(40)]
        public string Code { get; set; } = string.Empty;

        /// <summary>Fee charged to the order for this zone.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryFee { get; set; }

        /// <summary>Internal cost the restaurant pays to fulfil delivery in this zone (for profit reporting).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryCost { get; set; }

        public DeliveryPaymentMode PaymentMode { get; set; } = DeliveryPaymentMode.CustomerPays;

        [Column(TypeName = "decimal(18,6)")]
        public decimal? CenterLatitude { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? CenterLongitude { get; set; }

        public int? RadiusMeters { get; set; }

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Audit trail (who/when is added at the service boundary; timestamps live here).
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
