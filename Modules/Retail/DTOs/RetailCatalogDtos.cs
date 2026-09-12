namespace RestaurantPos.Api.Modules.Retail.DTOs
{
    /// <summary>
    /// One retail product as the catalogue screen renders it. Stored facts come from the
    /// product and its retail detail row; every cost/price/stock aggregate below is derived
    /// at read time and never persisted.
    /// </summary>
    public sealed class RetailProductDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }

        public string Sku { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? Brand { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? SizeLabel { get; set; }
        public string? CountryOfOrigin { get; set; }

        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryNameAr { get; set; }
        public string? CategoryDisplayName { get; set; }

        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? SupplierNameAr { get; set; }
        public string? SupplierDisplayName { get; set; }

        // ── Stock ────────────────────────────────────────────────────────────
        /// <summary>Opening/received balance imported from the workbook.</summary>
        public decimal ReceivedQuantity { get; set; }
        /// <summary>Units sold, summed from order lines (never a row count).</summary>
        public decimal SoldQuantity { get; set; }
        /// <summary>Derived: ReceivedQuantity − SoldQuantity.</summary>
        public decimal StockOnHand { get; set; }
        /// <summary>OutOfStock | Urgent | ReorderNow | Ok</summary>
        public string StockStatus { get; set; } = RetailStockStatuses.Ok;
        public decimal ReorderLevel { get; set; }

        // ── Cost ─────────────────────────────────────────────────────────────
        public decimal SupplierCostTotal { get; set; }
        public decimal ShippingCostPerUnit { get; set; }
        /// <summary>Derived: SupplierCostTotal + (ShippingCostPerUnit × ReceivedQuantity).</summary>
        public decimal TotalCost { get; set; }
        /// <summary>Product.CostPrice — cost per item.</summary>
        public decimal? CostPerItem { get; set; }

        // ── Price ────────────────────────────────────────────────────────────
        public decimal? TargetMarginPercent { get; set; }
        /// <summary>Derived: CostPerItem ÷ (1 − margin), rounded to the nearest unit.</summary>
        public decimal? CalculatedSellingPrice { get; set; }
        /// <summary>Product.BasePrice — the price of record, set by a person.</summary>
        public decimal ApprovedSellingPrice { get; set; }
        /// <summary>Derived: ApprovedSellingPrice − CostPerItem.</summary>
        public decimal? ProfitPerUnit { get; set; }
        /// <summary>Derived: ProfitPerUnit ÷ ApprovedSellingPrice × 100. 0 when the price is 0.</summary>
        public decimal? ActualMarginPercent { get; set; }

        public bool IsActive { get; set; }
        public string? ImageUrl { get; set; }
    }

    public static class RetailStockStatuses
    {
        public const string OutOfStock = "OutOfStock";
        public const string Urgent = "Urgent";
        public const string ReorderNow = "ReorderNow";
        public const string Ok = "Ok";
    }

    /// <summary>Headline figures for the retail catalogue screen.</summary>
    public sealed class RetailCatalogSummaryDto
    {
        public int ProductCount { get; set; }
        public int SupplierCount { get; set; }
        public int BrandCount { get; set; }
        public decimal TotalStockUnits { get; set; }
        public decimal InventoryValueAtCost { get; set; }
        public decimal InventoryValueAtRetail { get; set; }
        public int OutOfStockCount { get; set; }
        public int ReorderCount { get; set; }
    }
}
