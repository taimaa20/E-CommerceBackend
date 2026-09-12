using RestaurantPos.Api.Modules.Retail.DTOs;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <summary>Filters for the retail sales history. All optional.</summary>
    public sealed record RetailSalesQuery(
        int PageNumber = 1,
        int PageSize = 50,
        DateTime? FromUtc = null,
        DateTime? ToExclusiveUtc = null,
        string? Search = null,
        Guid? ProductId = null,
        string? Sku = null,
        string? Brand = null,
        string? PaymentStatus = null,
        Guid? OrderId = null);

    public interface IRetailReportingService
    {
        /// <summary>Retail trading result over a date window (half-open UTC; null = all time).</summary>
        Task<RetailPerformanceReportDto> GetPerformanceAsync(
            DateTime? fromUtc,
            DateTime? toExclusiveUtc,
            CancellationToken ct = default);

        /// <summary>Line-level retail sales history, read from the existing order records.
        /// Covers both imported workbook sales and sales rung up in the POS.</summary>
        Task<RetailSalesPageDto> GetSalesAsync(RetailSalesQuery query, CancellationToken ct = default);

        Task<IReadOnlyList<RetailLegacyPurchaseDto>> GetLegacyPurchasesAsync(CancellationToken ct = default);

        Task<IReadOnlyList<RetailSupplierDto>> GetSuppliersAsync(bool retailOnly, CancellationToken ct = default);
    }
}
