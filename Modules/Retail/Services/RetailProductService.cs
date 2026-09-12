using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.DTOs;
using RestaurantPos.Api.Modules.Retail.Domain;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    public sealed class RetailProductService : IRetailProductService
    {
        private const string PerformedBySystem = "product-setup";

        private readonly PosDbContext _context;
        private readonly IRetailStockLedger _ledger;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchContext _branchContext;
        private readonly ILogger<RetailProductService> _logger;

        public RetailProductService(
            PosDbContext context,
            IRetailStockLedger ledger,
            ITenantResolver tenantResolver,
            IBranchContext branchContext,
            ILogger<RetailProductService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task AttachRetailDetailsAsync(
            IReadOnlyList<ProductDto> products,
            bool isArabic,
            CancellationToken ct = default,
            Guid? branchId = null)
        {
            if (products.Count == 0)
                return;

            var productIds = products.Select(p => p.Id).ToList();

            var details = await _context.RetailProductDetails
                .AsNoTracking()
                .Where(d => productIds.Contains(d.ProductId))
                .Select(d => new
                {
                    d.ProductId,
                    d.Sku,
                    d.Barcode,
                    d.Brand,
                    d.SizeLabel,
                    d.CountryOfOrigin,
                    d.SupplierId,
                    SupplierName = d.Supplier != null ? d.Supplier.Name : null,
                    SupplierNameAr = d.Supplier != null ? d.Supplier.NameAr : null,
                    d.ReceivedQuantity,
                    d.SupplierCostTotal,
                    d.ShippingCostPerUnit,
                    d.TargetMarginPercent
                })
                .ToListAsync(ct);

            if (details.Count == 0)
                return;

            var positions = await _ledger.GetPositionsAsync(
                details.Select(d => d.ProductId).ToList(),
                branchId,
                ct);

            var byProduct = details.ToDictionary(d => d.ProductId, d => d);

            foreach (var product in products)
            {
                if (!byProduct.TryGetValue(product.Id, out var detail))
                    continue;

                var position = positions.GetValueOrDefault(product.Id);
                var receivedQuantity = position.TotalIn;
                var soldQuantity = position.TotalOut;
                var stockOnHand = position.OnHand;
                var reorderLevel = RetailCalculations.CalculateReorderLevel(receivedQuantity);

                product.Retail = new RetailProductDetailDto
                {
                    Sku = detail.Sku,
                    Barcode = detail.Barcode,
                    Brand = detail.Brand,
                    SizeLabel = detail.SizeLabel,
                    CountryOfOrigin = detail.CountryOfOrigin,
                    SupplierId = detail.SupplierId,
                    SupplierName = detail.SupplierName,
                    SupplierNameAr = detail.SupplierNameAr,
                    ReceivedQuantity = receivedQuantity,
                    SoldQuantity = soldQuantity,
                    StockOnHand = stockOnHand,
                    ReorderLevel = reorderLevel,
                    StockStatus = RetailCalculations.ResolveStockStatus(stockOnHand, reorderLevel),
                    SupplierCostTotal = detail.SupplierCostTotal,
                    ShippingCostPerUnit = detail.ShippingCostPerUnit,
                    TotalCost = RetailCalculations.CalculateTotalCost(
                        detail.SupplierCostTotal, detail.ShippingCostPerUnit, receivedQuantity),
                    TargetMarginPercent = detail.TargetMarginPercent,
                    CalculatedSellingPrice = RetailCalculations.CalculateSellingPrice(
                        product.CostPrice, detail.TargetMarginPercent),
                    ProfitPerUnit = RetailCalculations.CalculateProfitPerUnit(product.BasePrice, product.CostPrice),
                    ActualMarginPercent = RetailCalculations.CalculateActualMarginPercent(product.BasePrice, product.CostPrice)
                };
            }
        }

        public async Task UpsertAsync(
            Guid productId,
            Guid tenantId,
            RetailProductDetailUpsertDto? payload,
            CancellationToken ct = default)
        {
            if (payload is null)
                return;

            var sku = payload.Sku.Trim();
            if (sku.Length == 0)
                throw new ValidationException("SKU is required for a retail product.");

            await EnsureSkuIsFreeAsync(sku, productId, ct);

            var detail = await _context.RetailProductDetails
                .FirstOrDefaultAsync(d => d.ProductId == productId, ct);
            var isNew = detail is null;

            if (detail is null)
            {
                detail = new RetailProductDetail
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductId = productId
                };
                _context.RetailProductDetails.Add(detail);
            }
            else if (detail.ReceivedQuantity != payload.ReceivedQuantity)
            {
                // Stock is the ledger's, not this form's. Editing the figure here would change a
                // balance with no movement behind it, so the edit is recorded and ignored — the
                // operator adjusts stock through a purchase receipt or a stock adjustment.
                _logger.LogWarning(
                    "Retail product {ProductId} opening quantity edit ignored ({Old} -> {New}); stock changes only through the ledger",
                    productId, detail.ReceivedQuantity, payload.ReceivedQuantity);
            }

            detail.Sku = sku;
            detail.Barcode = Trimmed(payload.Barcode);
            detail.Brand = Trimmed(payload.Brand);
            detail.SizeLabel = Trimmed(payload.SizeLabel);
            detail.CountryOfOrigin = Trimmed(payload.CountryOfOrigin);
            detail.SupplierId = payload.SupplierId;
            detail.SupplierCostTotal = payload.SupplierCostTotal;
            detail.ShippingCostPerUnit = payload.ShippingCostPerUnit;
            detail.TargetMarginPercent = payload.TargetMarginPercent;

            if (isNew)
                detail.ReceivedQuantity = payload.ReceivedQuantity;

            await _context.SaveChangesAsync(ct);

            if (isNew && payload.ReceivedQuantity > 0m)
                await PostOpeningBalanceAsync(detail, ct);
        }

        /// <summary>
        /// A brand-new retail product may be created with stock already on the shelf. That
        /// quantity becomes the product's opening movement — the one and only time a figure
        /// typed into a form is allowed to establish stock.
        /// </summary>
        private async Task PostOpeningBalanceAsync(RetailProductDetail detail, CancellationToken ct)
        {
            // The ACTIVE branch, not the product's first assignment: creating a product
            // auto-assigns it to Main, so keying off the assignment would put a retail branch's
            // opening stock on the restaurant branch.
            var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

            var costPrice = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == detail.ProductId)
                .Select(p => p.CostPrice)
                .FirstOrDefaultAsync(ct);

            await _ledger.PostAsync(new RetailStockPosting(
                ProductId: detail.ProductId,
                BranchId: branchId,
                MovementType: RetailStockMovementType.OpeningBalance,
                Quantity: detail.ReceivedQuantity,
                UnitCost: costPrice,
                SourceDocumentType: RetailStockSourceDocument.ManualAdjustment,
                SourceDocumentId: detail.Id,
                SourceLineId: detail.Id,
                SourceReference: detail.Sku,
                OccurredAtUtc: DateTime.UtcNow,
                PerformedBySystem: PerformedBySystem,
                Notes: "Opening quantity entered when the retail product was created"), ct);
        }

        private async Task EnsureSkuIsFreeAsync(string sku, Guid productId, CancellationToken ct)
        {
            var taken = await _context.RetailProductDetails
                .AnyAsync(d => d.Sku == sku && d.ProductId != productId, ct);

            if (taken)
                throw new ValidationException($"SKU '{sku}' is already used by another product.");
        }

        private static string? Trimmed(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
