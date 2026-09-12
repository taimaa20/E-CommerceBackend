using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Retail.Repositories
{
    /// <summary>Flat row read from the database before derived values are applied.</summary>
    public sealed record RetailProductRow(
        Guid Id,
        Guid ProductId,
        string Sku,
        string? Barcode,
        string? Brand,
        string Name,
        string? NameAr,
        string? SizeLabel,
        string? CountryOfOrigin,
        Guid? CategoryId,
        string? CategoryName,
        string? CategoryNameAr,
        Guid? SupplierId,
        string? SupplierName,
        string? SupplierNameAr,
        decimal ReceivedQuantity,
        decimal SupplierCostTotal,
        decimal ShippingCostPerUnit,
        decimal? CostPrice,
        decimal? TargetMarginPercent,
        decimal BasePrice,
        bool IsActive,
        string? ImageUrl);

    public interface IRetailCatalogRepository
    {
        Task<PaginatedResponse<RetailProductRow>> GetPageAsync(
            Guid tenantId,
            int pageNumber,
            int pageSize,
            string? search,
            Guid? categoryId,
            Guid? supplierId,
            string? brand,
            Guid? branchId,
            CancellationToken ct = default);

        /// <summary>Every retail product, or only those assigned to a branch when one is given.</summary>
        Task<IReadOnlyList<RetailProductRow>> GetAllAsync(
            Guid tenantId,
            Guid? branchId,
            CancellationToken ct = default);

        Task<IReadOnlyList<string>> GetBrandsAsync(Guid tenantId, CancellationToken ct = default);
    }
}
