using RestaurantPos.Api.Modules.Purchasing.Services;

namespace RestaurantPos.Api.Modules.Purchasing
{
    /// <summary>Single DI entry point for the Purchasing read seam.</summary>
    public static class PurchasingServiceCollectionExtensions
    {
        public static IServiceCollection AddPurchasingModule(this IServiceCollection services)
        {
            // Read-only. Owns no table and writes nothing: the two purchase documents keep
            // their own services, and this only presents them as one list.
            services.AddScoped<IPurchaseOrderDirectory, PurchaseOrderDirectory>();

            return services;
        }
    }
}
