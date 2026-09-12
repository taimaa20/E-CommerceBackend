using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Modules.Retail.DTOs
{
    /// <summary>
    /// Retail attributes carried alongside a product on the EXISTING product endpoints.
    /// Null on <c>ProductDto.Retail</c> means "ordinary restaurant product" — that is the
    /// only signal any consumer needs, so no caller has to know about branches or flags.
    /// </summary>
    public sealed class RetailProductDetailDto
    {
        public string Sku { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? Brand { get; set; }
        public string? SizeLabel { get; set; }
        public string? CountryOfOrigin { get; set; }

        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? SupplierNameAr { get; set; }

        public decimal ReceivedQuantity { get; set; }
        public decimal SoldQuantity { get; set; }
        public decimal StockOnHand { get; set; }
        public decimal ReorderLevel { get; set; }
        public string StockStatus { get; set; } = RetailStockStatuses.Ok;

        public decimal SupplierCostTotal { get; set; }
        public decimal ShippingCostPerUnit { get; set; }
        public decimal TotalCost { get; set; }

        public decimal? TargetMarginPercent { get; set; }
        public decimal? CalculatedSellingPrice { get; set; }
        public decimal? ProfitPerUnit { get; set; }
        public decimal? ActualMarginPercent { get; set; }
    }

    /// <summary>
    /// Write shape accepted by the existing product create/update endpoints. Present ⇒ the
    /// product is (or becomes) a retail product; absent ⇒ nothing about retail changes, so
    /// every existing restaurant caller keeps working untouched.
    /// </summary>
    public sealed class RetailProductDetailUpsertDto
    {
        [Required(ErrorMessage = "SKU is required for a retail product.")]
        [MaxLength(64)]
        public string Sku { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? Barcode { get; set; }

        [MaxLength(120)]
        public string? Brand { get; set; }

        [MaxLength(40)]
        public string? SizeLabel { get; set; }

        [MaxLength(80)]
        public string? CountryOfOrigin { get; set; }

        public Guid? SupplierId { get; set; }

        [Range(0, 9999999, ErrorMessage = "Received quantity cannot be negative.")]
        public decimal ReceivedQuantity { get; set; }

        [Range(0, 99999999, ErrorMessage = "Supplier cost cannot be negative.")]
        public decimal SupplierCostTotal { get; set; }

        [Range(0, 99999999, ErrorMessage = "Shipping cost cannot be negative.")]
        public decimal ShippingCostPerUnit { get; set; }

        [Range(0, 99.99, ErrorMessage = "Target margin must be between 0 and less than 100.")]
        public decimal? TargetMarginPercent { get; set; }
    }
}
