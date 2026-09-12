using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Printing
{
    /// <summary>
    /// Groups OrderItems by (Kitchen, Printer) so that a single product can fan
    /// out to multiple kitchen printers when configured. Resolution order:
    ///   1. If product has AllKitchenPrinters = true → every active kitchen
    ///      printer in the tenant.
    ///   2. Else if product has explicit KitchenPrinter mappings → those printers.
    ///   3. Else fall back to the legacy single-printer rule based on
    ///      product.KitchenId / tenant default kitchen.
    /// Items whose product yields no printer at all land in a single
    /// "unassigned" bucket (Kitchen + Printer both null) which the dispatcher
    /// logs and skips.
    /// </summary>
    public interface IKitchenRoutingService
    {
        Task<IReadOnlyList<KitchenRouteGroup>> RouteAsync(
            Guid orderId,
            IReadOnlyCollection<Guid>? itemIdFilter,
            CancellationToken ct);
    }

    public sealed class KitchenRouteGroup
    {
        public Kitchen? Kitchen { get; init; } // null = unassigned bucket
        public Printer? Printer { get; init; } // null = no resolvable printer
        public List<OrderItem> Items { get; init; } = new();
    }
}
