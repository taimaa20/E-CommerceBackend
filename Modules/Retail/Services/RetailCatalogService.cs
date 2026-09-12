using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.DTOs;
using RestaurantPos.Api.Modules.Retail.Repositories;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    public sealed class RetailCatalogService : IRetailCatalogService
    {
        private const int MaxPageSize = 200;

        private readonly IRetailCatalogRepository _repository;
        private readonly IRetailStockLedger _ledger;
        private readonly ITenantResolver _tenantResolver;

        public RetailCatalogService(
            IRetailCatalogRepository repository,
            IRetailStockLedger ledger,
            ITenantResolver tenantResolver)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        public async Task<PaginatedResponse<RetailProductDto>> GetPageAsync(
            bool isArabic,
            int pageNumber,
            int pageSize,
            string? search,
            Guid? categoryId,
            Guid? supplierId,
            string? brand,
            Guid? branchId,
            CancellationToken ct = default)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var page = await _repository.GetPageAsync(
                tenantId,
                Math.Max(pageNumber, 1),
                Math.Clamp(pageSize, 1, MaxPageSize),
                search,
                categoryId,
                supplierId,
                brand,
                branchId,
                ct);

            var items = await MapAsync(page.Items, isArabic, branchId, ct);

            return new PaginatedResponse<RetailProductDto>
            {
                Items = items,
                TotalCount = page.TotalCount,
                PageNumber = page.PageNumber,
                PageSize = page.PageSize
            };
        }

        public async Task<RetailCatalogSummaryDto> GetSummaryAsync(Guid? branchId, CancellationToken ct = default)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var rows = await _repository.GetAllAsync(tenantId, branchId, ct);
            var products = await MapAsync(rows, isArabic: false, branchId, ct);

            return new RetailCatalogSummaryDto
            {
                ProductCount = products.Count,
                SupplierCount = products.Where(p => p.SupplierId.HasValue).Select(p => p.SupplierId!.Value).Distinct().Count(),
                BrandCount = products.Where(p => !string.IsNullOrWhiteSpace(p.Brand)).Select(p => p.Brand!).Distinct().Count(),
                TotalStockUnits = products.Sum(p => p.StockOnHand),
                InventoryValueAtCost = decimal.Round(products.Sum(p => p.StockOnHand * (p.CostPerItem ?? 0m)), 2, MidpointRounding.AwayFromZero),
                InventoryValueAtRetail = decimal.Round(products.Sum(p => p.StockOnHand * p.ApprovedSellingPrice), 2, MidpointRounding.AwayFromZero),
                OutOfStockCount = products.Count(p => p.StockStatus == RetailStockStatuses.OutOfStock),
                ReorderCount = products.Count(p => p.StockStatus is RetailStockStatuses.Urgent or RetailStockStatuses.ReorderNow)
            };
        }

        public async Task<IReadOnlyList<string>> GetBrandsAsync(CancellationToken ct = default)
            => await _repository.GetBrandsAsync(_tenantResolver.GetTenantId(), ct);

        private async Task<List<RetailProductDto>> MapAsync(
            IReadOnlyList<RetailProductRow> rows,
            bool isArabic,
            Guid? branchId,
            CancellationToken ct)
        {
            if (rows.Count == 0)
                return new List<RetailProductDto>();

            // Every stock figure below is the ledger's, summed once for the whole page, and
            // scoped to the branch when the caller is looking at one branch's inventory.
            var positions = await _ledger.GetPositionsAsync(
                rows.Select(r => r.ProductId).Distinct().ToList(),
                branchId,
                ct);

            return rows.Select(row => Map(row, positions, isArabic)).ToList();
        }

        private static RetailProductDto Map(
            RetailProductRow row,
            IReadOnlyDictionary<Guid, RetailStockPosition> positions,
            bool isArabic)
        {
            var position = positions.GetValueOrDefault(row.ProductId);
            var received = position.TotalIn;
            var sold = position.TotalOut;
            var stockOnHand = position.OnHand;
            var reorderLevel = RetailCalculations.CalculateReorderLevel(received);

            return new RetailProductDto
            {
                Id = row.Id,
                ProductId = row.ProductId,
                Sku = row.Sku,
                Barcode = row.Barcode,
                Brand = row.Brand,
                Name = row.Name,
                NameAr = row.NameAr,
                DisplayName = Display(row.Name, row.NameAr, isArabic),
                SizeLabel = row.SizeLabel,
                CountryOfOrigin = row.CountryOfOrigin,
                CategoryId = row.CategoryId,
                CategoryName = row.CategoryName,
                CategoryNameAr = row.CategoryNameAr,
                CategoryDisplayName = row.CategoryName is null ? null : Display(row.CategoryName, row.CategoryNameAr, isArabic),
                SupplierId = row.SupplierId,
                SupplierName = row.SupplierName,
                SupplierNameAr = row.SupplierNameAr,
                SupplierDisplayName = row.SupplierName is null ? null : Display(row.SupplierName, row.SupplierNameAr, isArabic),
                ReceivedQuantity = received,
                SoldQuantity = sold,
                StockOnHand = stockOnHand,
                ReorderLevel = reorderLevel,
                StockStatus = RetailCalculations.ResolveStockStatus(stockOnHand, reorderLevel),
                SupplierCostTotal = row.SupplierCostTotal,
                ShippingCostPerUnit = row.ShippingCostPerUnit,
                TotalCost = RetailCalculations.CalculateTotalCost(row.SupplierCostTotal, row.ShippingCostPerUnit, received),
                CostPerItem = row.CostPrice,
                TargetMarginPercent = row.TargetMarginPercent,
                CalculatedSellingPrice = RetailCalculations.CalculateSellingPrice(row.CostPrice, row.TargetMarginPercent),
                ApprovedSellingPrice = row.BasePrice,
                ProfitPerUnit = RetailCalculations.CalculateProfitPerUnit(row.BasePrice, row.CostPrice),
                ActualMarginPercent = RetailCalculations.CalculateActualMarginPercent(row.BasePrice, row.CostPrice),
                IsActive = row.IsActive,
                ImageUrl = row.ImageUrl
            };
        }

        private static string Display(string name, string? nameAr, bool isArabic)
            => isArabic && !string.IsNullOrWhiteSpace(nameAr) ? nameAr : name;
    }
}
