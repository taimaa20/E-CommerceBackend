namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <summary>What the initialiser did, so the run can be reconciled against the workbook.</summary>
    public sealed class RetailStockLedgerInitResult
    {
        public int ProductsWithOpeningBalance { get; set; }
        public int OpeningBalancesCreated { get; set; }
        public decimal OpeningUnits { get; set; }
        public int OrdersReconciled { get; set; }
        public decimal SoldUnits { get; set; }
        public decimal StockOnHand { get; set; }
        public List<string> Skipped { get; } = new();
    }

    /// <summary>
    /// Converts the imported opening position into ledger movements, once.
    ///
    /// The opening balance is the GROSS quantity received, not the net position: the sales that
    /// have already happened are then posted as their own Sale movements by reconciling each
    /// historical order. Seeding the net figure instead would subtract those sales twice.
    ///
    /// Re-running is safe by construction — the opening balance is unique per product and
    /// branch, and each sale is unique per order line.
    /// </summary>
    public interface IRetailStockLedgerInitializer
    {
        Task<RetailStockLedgerInitResult> InitializeAsync(CancellationToken ct = default);
    }
}
