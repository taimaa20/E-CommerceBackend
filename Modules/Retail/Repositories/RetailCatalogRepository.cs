using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.Domain;

namespace RestaurantPos.Api.Modules.Retail.Repositories
{
    public sealed class RetailCatalogRepository : IRetailCatalogRepository
    {
        private readonly PosDbContext _context;

        public RetailCatalogRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<PaginatedResponse<RetailProductRow>> GetPageAsync(
            Guid tenantId,
            int pageNumber,
            int pageSize,
            string? search,
            Guid? categoryId,
            Guid? supplierId,
            string? brand,
            Guid? branchId,
            CancellationToken ct = default)
        {
            var query = BuildQuery(tenantId, search, categoryId, supplierId, brand, branchId);
            var totalCount = await query.CountAsync(ct);

            // Order on the entity BEFORE projecting. Ordering a positional record after
            // Select() makes EF try to translate the whole constructor into ORDER BY, which
            // it cannot do — the query then fails to translate at runtime.
            var items = await Project(query.OrderBy(d => d.Product!.Name).ThenBy(d => d.Sku))
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PaginatedResponse<RetailProductRow>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<IReadOnlyList<RetailProductRow>> GetAllAsync(
            Guid tenantId,
            Guid? branchId,
            CancellationToken ct = default)
            => await Project(BuildQuery(tenantId, null, null, null, null, branchId)).ToListAsync(ct);

        public async Task<IReadOnlyList<string>> GetBrandsAsync(Guid tenantId, CancellationToken ct = default)
            => await _context.RetailProductDetails
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId && d.Brand != null && d.Brand != string.Empty)
                .Select(d => d.Brand!)
                .Distinct()
                .OrderBy(b => b)
                .ToListAsync(ct);

        private IQueryable<RetailProductDetail> BuildQuery(
            Guid tenantId,
            string? search,
            Guid? categoryId,
            Guid? supplierId,
            string? brand,
            Guid? branchId)
        {
            var query = _context.RetailProductDetails
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(d =>
                    d.Sku.ToLower().Contains(term)
                    || (d.Barcode != null && d.Barcode.ToLower().Contains(term))
                    || (d.Brand != null && d.Brand.ToLower().Contains(term))
                    || (d.Product != null && d.Product.Name.ToLower().Contains(term)));
            }

            if (categoryId.HasValue)
                query = query.Where(d => d.Product != null && d.Product.CategoryId == categoryId.Value);

            if (supplierId.HasValue)
                query = query.Where(d => d.SupplierId == supplierId.Value);

            if (!string.IsNullOrWhiteSpace(brand))
                query = query.Where(d => d.Brand == brand);

            // Branch catalogue membership stays on the frozen BranchProduct platform — the
            // retail module never keeps its own idea of which branch sells what.
            if (branchId.HasValue)
            {
                query = query.Where(d => _context.Set<BranchProduct>()
                    .Any(bp => bp.ProductId == d.ProductId && bp.BranchId == branchId.Value));
            }

            return query;
        }

        private static IQueryable<RetailProductRow> Project(IQueryable<RetailProductDetail> query)
            => query.Select(d => new RetailProductRow(
                d.Id,
                d.ProductId,
                d.Sku,
                d.Barcode,
                d.Brand,
                d.Product!.Name,
                d.Product.NameAr,
                d.SizeLabel,
                d.CountryOfOrigin,
                d.Product.CategoryId,
                d.Product.Category != null ? d.Product.Category.Name : null,
                d.Product.Category != null ? d.Product.Category.NameAr : null,
                d.SupplierId,
                d.Supplier != null ? d.Supplier.Name : null,
                d.Supplier != null ? d.Supplier.NameAr : null,
                d.ReceivedQuantity,
                d.SupplierCostTotal,
                d.ShippingCostPerUnit,
                d.Product.CostPrice,
                d.TargetMarginPercent,
                d.Product.BasePrice,
                d.Product.IsActive,
                d.Product.ImageUrl));
    }
}
