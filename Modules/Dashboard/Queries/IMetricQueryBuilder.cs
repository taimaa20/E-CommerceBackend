using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Dashboard.Filters;

namespace RestaurantPos.Api.Modules.Dashboard.Queries
{
    /// <summary>
    /// Builds tenant- and filter-scoped <see cref="IQueryable{T}"/> sources for
    /// dashboard calculators. Every analytics calculator goes through this
    /// builder — it is the only place permitted to attach the active
    /// <see cref="Filters.IDashboardFilterContext"/> window and scope filters
    /// to a query, guaranteeing every metric on a dashboard shares one filter.
    /// </summary>
    public interface IMetricQueryBuilder
    {
        bool ExcludesWasteLogs { get; }

        IQueryable<Order>       Orders();
        IQueryable<Order>       AllOrders();
        IQueryable<OrderItem>   OrderItems();
        IQueryable<Payment>     Payments();
        IQueryable<WasteLog>    WasteLogs();
        IQueryable<RefundLog>   RefundLogs();
        IQueryable<CancelLog>   CancelLogs();
        IQueryable<CancelLog>   AllCancelLogs();
        IQueryable<RawMaterial> RawMaterials();

        /// <summary>
        /// Per-warehouse stock balances, scoped to the filter's WarehouseId when one
        /// is selected (the single place warehouse filtering is applied). Used by the
        /// warehouse-aware stock-health metrics; returns every warehouse's rows when
        /// no specific warehouse is selected.
        /// </summary>
        IQueryable<RawMaterialInventory> RawMaterialInventories();

        IQueryable<StockBatch>  StockBatches();
        IQueryable<TimeEntry>   TimeEntries();
        IMetricQueryBuilder     ForContext(IDashboardFilterContext context);
    }
}
