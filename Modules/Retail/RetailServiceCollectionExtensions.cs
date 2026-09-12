using RestaurantPos.Api.Modules.Retail.Import;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Modules.Retail.Repositories;
using RestaurantPos.Api.Modules.Retail.Services;

namespace RestaurantPos.Api.Modules.Retail
{
    /// <summary>Single DI entry point for the Retail module.</summary>
    public static class RetailServiceCollectionExtensions
    {
        public static IServiceCollection AddRetailModule(this IServiceCollection services)
        {
            services.AddScoped<IRetailCatalogRepository, RetailCatalogRepository>();
            services.AddScoped<IRetailCatalogService, RetailCatalogService>();

            // Composes retail attributes onto the existing product contract.
            services.AddScoped<IRetailProductService, RetailProductService>();

            // Read projections behind the existing reports / procurement / supplier screens.
            services.AddScoped<IRetailReportingService, RetailReportingService>();

            // Developer-side workbook import, invoked by the --import-retail-workbook switch.
            services.AddScoped<IRetailWorkbookImporter, RetailWorkbookImporter>();

            // The append-only finished-goods ledger and its sole writer.
            services.AddScoped<IRetailStockLedger, RetailStockLedger>();

            // Order <-> ledger reconciliation. Registered once and exposed under two names: the
            // core knows it as IProductStockLedger (the seam the order lifecycle calls through),
            // retail code knows it as IRetailOrderStockService.
            services.AddScoped<RetailOrderStockService>();
            services.AddScoped<IProductStockLedger>(sp => sp.GetRequiredService<RetailOrderStockService>());
            services.AddScoped<IRetailOrderStockService>(sp => sp.GetRequiredService<RetailOrderStockService>());

            // Operational retail purchasing on the shared Supplier master.
            services.AddScoped<IRetailPurchasingService, RetailPurchasingService>();

            // Manual stock corrections — the only human-initiated writer of the ledger.
            services.AddScoped<IRetailStockAdjustmentService, RetailStockAdjustmentService>();

            // One-time, idempotent conversion of the imported opening position into ledger rows.
            services.AddScoped<IRetailStockLedgerInitializer, RetailStockLedgerInitializer>();

            return services;
        }
    }
}
