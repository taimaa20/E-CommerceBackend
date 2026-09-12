using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Modules.Retail.DTOs;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <summary>
    /// Composes retail attributes onto the EXISTING product contract, and persists edits made
    /// through the existing product create/update flow. Kept out of <c>ProductService</c> so
    /// the restaurant product path is untouched: a product with no retail row is returned
    /// exactly as before, with <c>ProductDto.Retail</c> left null.
    /// </summary>
    public interface IRetailProductService
    {
        /// <summary>Fills <c>ProductDto.Retail</c> for any product that has a retail row.
        /// One query for the whole page — never per product.</summary>
        /// <paramref name="branchId"/> scopes the stock position: null (the default, and what
        /// the tenant-wide product screens want) sums every branch; a branch id gives that one
        /// branch's shelf, which is what a till needs.
        Task AttachRetailDetailsAsync(
            IReadOnlyList<ProductDto> products,
            bool isArabic,
            CancellationToken ct = default,
            Guid? branchId = null);

        /// <summary>Creates or updates the retail row for a product. A null payload is a
        /// no-op, so every existing restaurant caller is unaffected.</summary>
        Task UpsertAsync(
            Guid productId,
            Guid tenantId,
            RetailProductDetailUpsertDto? payload,
            CancellationToken ct = default);
    }
}
