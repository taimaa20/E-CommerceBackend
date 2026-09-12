namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <summary>
    /// Module-facing name for the order reconciler. The core talks to it through
    /// <see cref="RestaurantPos.Api.Services.IProductStockLedger"/>; retail code (the workbook
    /// importer, the ledger initialiser) uses this alias so it does not have to reach for a
    /// generic abstraction to do a retail job.
    /// </summary>
    public interface IRetailOrderStockService
    {
        /// <summary>Re-derives and posts the ledger effect of one order in its current state.</summary>
        Task SyncOrderAsync(Guid orderId, CancellationToken ct = default);
    }
}
