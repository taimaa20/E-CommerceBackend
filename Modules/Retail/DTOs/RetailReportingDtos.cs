namespace RestaurantPos.Api.Modules.Retail.DTOs
{
    /// <summary>
    /// Per-product trading result. Replaces the workbook's SUMMARY PROFIT sheet using the
    /// corrected definitions from the workbook audit:
    ///   • units sold = Σ line quantity   (the sheet counted rows)
    ///   • revenue    = Σ (sold unit price × quantity)   (the sheet summed unit price)
    /// COGS is the sum of the SALE-TIME cost snapshots on the order lines, so a later change
    /// to a product's cost cannot rewrite a period that is already closed.
    /// </summary>
    public sealed class RetailProductPerformanceDto
    {
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? CategoryName { get; set; }
        public string? SupplierName { get; set; }

        public decimal QuantitySold { get; set; }
        public decimal TotalSales { get; set; }
        /// <summary>Today's cost per item. Shown for reference; it is NOT what TotalCost uses.</summary>
        public decimal? CostPerItem { get; set; }
        /// <summary>Σ of the sale-time cost snapshots on the lines sold in the period.</summary>
        public decimal TotalCost { get; set; }
        /// <summary>Number of sold lines with no cost snapshot, whose cost fell back to the
        /// product's current cost. Zero once every line has been through the ledger.</summary>
        public int LinesWithoutCostSnapshot { get; set; }
        public decimal GrossProfit { get; set; }
        /// <summary>Gross profit ÷ revenue × 100. Zero — not an error — when revenue is 0,
        /// because giveaways are a normal retail case.</summary>
        public decimal ProfitPercent { get; set; }
    }

    public sealed class RetailPerformanceReportDto
    {
        public decimal TotalSales { get; set; }
        public decimal TotalUnitsSold { get; set; }
        public decimal TotalCost { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossMarginPercent { get; set; }
        public int ProductCount { get; set; }
        public int OrderCount { get; set; }
        public decimal OutstandingUnpaid { get; set; }
        /// <summary>Total sold lines whose cost had to fall back to the product's current cost.
        /// Anything above zero means part of this report is not yet transactionally stable.</summary>
        public int LinesWithoutCostSnapshot { get; set; }
        public List<RetailProductPerformanceDto> Products { get; set; } = new();
    }

    /// <summary>
    /// One retail sale LINE. A read-only projection over the existing
    /// Order / OrderItem / Payment records — no sale is duplicated to render it. Imported
    /// workbook history and new POS sales appear here identically, which is what makes this
    /// the ongoing retail sales history rather than an Excel archive.
    /// </summary>
    public sealed class RetailSaleLineDto
    {
        public Guid OrderId { get; set; }
        public Guid OrderItemId { get; set; }
        public DateTime SoldAtUtc { get; set; }
        public string OrderNumber { get; set; } = string.Empty;

        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }

        public Guid ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? SupplierName { get; set; }

        public int Quantity { get; set; }
        /// <summary>List price at the time of sale (OrderItem.UnitPriceSnapshot).</summary>
        public decimal? ApprovedUnitPrice { get; set; }
        /// <summary>What the customer actually paid per unit.</summary>
        public decimal SoldUnitPrice { get; set; }
        public decimal LineTotal { get; set; }

        /// <summary>Cost of goods snapshotted when the line was relieved from stock.</summary>
        public decimal LineCost { get; set; }
        /// <summary>Cost per unit at the moment of sale, derived from the snapshot.</summary>
        public decimal? UnitCost { get; set; }
        public decimal LineProfit { get; set; }
        /// <summary>False when the line has no cost snapshot yet, so its profit is indicative.</summary>
        public bool HasCostSnapshot { get; set; }

        public string OrderStatus { get; set; } = string.Empty;
        public string? PaymentMethod { get; set; }
        public bool IsPaid { get; set; }
        /// <summary>True when the line transacted at zero — the workbook's GIFT rows.</summary>
        public bool IsGift { get; set; }
        /// <summary>True when the sold price differs from the approved price.</summary>
        public bool IsPriceOverride { get; set; }
        /// <summary>True for rows imported from the workbook rather than rung up in the POS.</summary>
        public bool IsImportedHistory { get; set; }
    }

    public sealed class RetailSalesPageDto
    {
        public List<RetailSaleLineDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalUnits { get; set; }
    }

    /// <summary>Historical purchase-order header imported from the workbook. Header-only by
    /// design — the source has no SKU-level lines.</summary>
    public sealed class RetailLegacyPurchaseDto
    {
        public Guid Id { get; set; }
        public DateTime OrderDateUtc { get; set; }
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        public string? InvoiceNumber { get; set; }
        public Guid? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal QuantityPieces { get; set; }
        public decimal SupplierCost { get; set; }
        public decimal ShippingCustomsClearance { get; set; }
        public decimal TotalCost { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>Supplier as the retail workbook describes it. Reads the shared
    /// <c>Supplier</c> master — no duplicate supplier records exist.</summary>
    public sealed class RetailSupplierDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Brands { get; set; }
        public string? BestSellers { get; set; }
        public string? Country { get; set; }
        public string? Website { get; set; }
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
        public int RetailProductCount { get; set; }
        public int PurchaseCount { get; set; }
        public decimal PurchaseValue { get; set; }
    }
}
