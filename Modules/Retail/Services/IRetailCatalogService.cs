using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.DTOs;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    public interface IRetailCatalogService
    {
        Task<PaginatedResponse<RetailProductDto>> GetPageAsync(
            bool isArabic,
            int pageNumber,
            int pageSize,
            string? search,
            Guid? categoryId,
            Guid? supplierId,
            string? brand,
            Guid? branchId,
            CancellationToken ct = default);

        /// <summary>Headline stock figures. Pass a branch to scope them to that branch's
        /// catalogue — which is what the branch-aware Inventory screen needs.</summary>
        Task<RetailCatalogSummaryDto> GetSummaryAsync(Guid? branchId, CancellationToken ct = default);

        Task<IReadOnlyList<string>> GetBrandsAsync(CancellationToken ct = default);
    }
}
