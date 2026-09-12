using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Retail.Domain
{
    /// <summary>
    /// Retail-only attributes of a <see cref="Product"/>, held in a 1:1 side table so the
    /// shared Product entity is not widened. A product WITHOUT a detail row is an ordinary
    /// restaurant product and is unaffected by anything in the Retail module.
    ///
    /// Deliberately absent: current stock, total cost, cost per item, calculated selling
    /// price and profit. Those are derived — see <see cref="Services.RetailCatalogService"/>.
    /// Cost per item lives on <see cref="Product.CostPrice"/> and the approved selling price
    /// lives on <see cref="Product.BasePrice"/>; both are reused rather than duplicated.
    /// </summary>
    public class RetailProductDetail : BaseEntity
    {
        public Guid ProductId { get; set; }
        // Nullable navigation: this entity is created by the workbook importer with the FK
        // set explicitly, so a required nav would force a load that is never needed.
        public Product? Product { get; set; }

        /// <summary>Merchant identifier. Text, not numeric — the workbook contains "RLL ROSC".</summary>
        [Required]
        [MaxLength(64)]
        public string Sku { get; set; } = string.Empty;

        /// <summary>Manufacturer EAN/UPC. Equal to <see cref="Sku"/> in the current workbook
        /// but a separate concept, so it is stored separately.</summary>
        [MaxLength(64)]
        public string? Barcode { get; set; }

        [MaxLength(120)]
        public string? Brand { get; set; }

        /// <summary>Free-form size label ("100 ML"), not a quantity.</summary>
        [MaxLength(40)]
        public string? SizeLabel { get; set; }

        [MaxLength(80)]
        public string? CountryOfOrigin { get; set; }

        public Guid? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        /// <summary>Target gross margin as a share of the SELLING price, 0–100.
        /// The workbook stores it as a fraction (0.55); the importer scales it.</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal? TargetMarginPercent { get; set; }

        /// <summary>
        /// Total units received to date — the workbook's "AVAILABLE SOH" column, which is
        /// maintained as cumulative receipts rather than as available stock.
        ///
        /// QUICK-MVP OPENING BALANCE: this is a one-off imported figure, not a movement
        /// history. Stock on hand is derived as ReceivedQuantity − (units sold), so POS
        /// sales reduce it live. Replaced by the stock-movement ledger in a later phase.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ReceivedQuantity { get; set; }

        /// <summary>Total supplier invoice cost for all units received (workbook column I).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal SupplierCostTotal { get; set; }

        /// <summary>Shipping/customs/clearance allocated per unit (workbook column J).
        /// Zero throughout the current workbook — landed cost is not yet in use.</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal ShippingCostPerUnit { get; set; }
    }
}
