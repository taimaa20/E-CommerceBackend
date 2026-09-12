using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.Domain;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <inheritdoc cref="IRetailStockLedgerInitializer"/>
    public sealed class RetailStockLedgerInitializer : IRetailStockLedgerInitializer
    {
        private const string PerformedBySystem = "workbook-import";
        private const string OpeningNote = "Imported opening position (workbook AVAILABLE SOH)";

        /// <summary>The opening position predates every recorded sale, so it is stamped a day
        /// before the first one. Ordering only matters for reading the ledger — the balance is
        /// a sum and does not depend on it.</summary>
        private static readonly TimeSpan OpeningBackdate = TimeSpan.FromDays(1);

        private readonly PosDbContext _context;
        private readonly IRetailStockLedger _ledger;
        private readonly IRetailOrderStockService _orderStock;
        private readonly ILogger<RetailStockLedgerInitializer> _logger;

        public RetailStockLedgerInitializer(
            PosDbContext context,
            IRetailStockLedger ledger,
            IRetailOrderStockService orderStock,
            ILogger<RetailStockLedgerInitializer> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _orderStock = orderStock ?? throw new ArgumentNullException(nameof(orderStock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<RetailStockLedgerInitResult> InitializeAsync(CancellationToken ct = default)
        {
            var result = new RetailStockLedgerInitResult();

            var products = await LoadOpeningPositionsAsync(ct);
            var openingAt = await ResolveOpeningTimestampAsync(ct);
            var postings = new List<RetailStockPosting>();

            foreach (var product in products)
            {
                if (product.BranchId is null)
                {
                    result.Skipped.Add($"{product.Sku}: not assigned to any branch");
                    continue;
                }

                if (product.ReceivedQuantity <= 0m)
                    continue;

                result.ProductsWithOpeningBalance++;
                result.OpeningUnits += product.ReceivedQuantity;
                postings.Add(new RetailStockPosting(
                    ProductId: product.ProductId,
                    BranchId: product.BranchId.Value,
                    MovementType: RetailStockMovementType.OpeningBalance,
                    Quantity: product.ReceivedQuantity,
                    UnitCost: product.CostPrice,
                    SourceDocumentType: RetailStockSourceDocument.WorkbookImport,
                    SourceDocumentId: product.DetailId,
                    SourceLineId: product.DetailId,
                    SourceReference: product.Sku,
                    OccurredAtUtc: openingAt,
                    PerformedBySystem: PerformedBySystem,
                    Notes: OpeningNote));
            }

            var before = await CountMovementsAsync(RetailStockMovementType.OpeningBalance, ct);
            await _ledger.PostManyAsync(postings, ct);
            result.OpeningBalancesCreated = await CountMovementsAsync(RetailStockMovementType.OpeningBalance, ct) - before;

            // Every historical sale becomes its own Sale movement through the ordinary
            // reconciler, so imported history and live trading are recorded identically.
            var orderIds = await LoadRetailOrderIdsAsync(ct);
            foreach (var orderId in orderIds)
            {
                await _orderStock.SyncOrderAsync(orderId, ct);
                result.OrdersReconciled++;
            }

            result.SoldUnits = await SumAsync(m => m.MovementType == RetailStockMovementType.Sale, ct) * -1m;
            result.StockOnHand = await SumAsync(_ => true, ct);

            _logger.LogInformation(
                "Retail stock ledger initialised: {Opening} opening units over {Products} products, {Orders} orders reconciled, {Sold} units sold, {OnHand} on hand",
                result.OpeningUnits, result.ProductsWithOpeningBalance, result.OrdersReconciled, result.SoldUnits, result.StockOnHand);

            return result;
        }

        private async Task<List<OpeningPosition>> LoadOpeningPositionsAsync(CancellationToken ct)
            => await _context.RetailProductDetails
                .AsNoTracking()
                .Select(d => new OpeningPosition(
                    d.Id,
                    d.ProductId,
                    d.Sku,
                    d.ReceivedQuantity,
                    d.Product!.CostPrice,
                    _context.Set<BranchProduct>()
                        .Where(bp => bp.ProductId == d.ProductId)
                        .Select(bp => (Guid?)bp.BranchId)
                        .FirstOrDefault()))
                .ToListAsync(ct);

        private async Task<List<Guid>> LoadRetailOrderIdsAsync(CancellationToken ct)
            => await _context.Orders
                .AsNoTracking()
                .Where(o => o.OrderItems.Any(oi =>
                    _context.RetailProductDetails.Any(d => d.ProductId == oi.ProductId)))
                .OrderBy(o => o.CreatedAt)
                .Select(o => o.Id)
                .ToListAsync(ct);

        private async Task<DateTime> ResolveOpeningTimestampAsync(CancellationToken ct)
        {
            var firstSale = await _context.Orders
                .AsNoTracking()
                .Where(o => o.OrderItems.Any(oi =>
                    _context.RetailProductDetails.Any(d => d.ProductId == oi.ProductId)))
                .MinAsync(o => (DateTime?)o.CreatedAt, ct);

            var stamp = (firstSale ?? DateTime.UtcNow) - OpeningBackdate;
            return DateTime.SpecifyKind(stamp, DateTimeKind.Utc);
        }

        private Task<int> CountMovementsAsync(RetailStockMovementType type, CancellationToken ct)
            => _context.RetailStockMovements.AsNoTracking().CountAsync(m => m.MovementType == type, ct);

        private async Task<decimal> SumAsync(
            System.Linq.Expressions.Expression<Func<RetailStockMovement, bool>> predicate,
            CancellationToken ct)
            => await _context.RetailStockMovements
                .AsNoTracking()
                .Where(predicate)
                .SumAsync(m => (decimal?)m.Quantity, ct) ?? 0m;

        private sealed record OpeningPosition(
            Guid DetailId,
            Guid ProductId,
            string Sku,
            decimal ReceivedQuantity,
            decimal? CostPrice,
            Guid? BranchId);
    }
}
