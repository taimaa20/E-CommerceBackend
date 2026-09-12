using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Modules.Retail.Domain;
using RestaurantPos.Api.Modules.Retail.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <inheritdoc cref="IRetailStockAdjustmentService"/>
    public sealed class RetailStockAdjustmentService : IRetailStockAdjustmentService
    {
        private const string PerformedBySystem = "stock-adjustment";
        private const int HistoryLimit = 200;

        private readonly PosDbContext _context;
        private readonly IRetailStockLedger _ledger;
        private readonly IBranchContext _branchContext;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<RetailStockAdjustmentService> _logger;

        public RetailStockAdjustmentService(
            PosDbContext context,
            IRetailStockLedger ledger,
            IBranchContext branchContext,
            ICurrentUserAccessor currentUser,
            ILogger<RetailStockAdjustmentService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<RetailStockAdjustmentResultDto> AdjustAsync(
            Guid productId,
            RetailStockAdjustmentCreateDto dto,
            CancellationToken ct = default)
        {
            var reason = (dto.Reason ?? string.Empty).Trim();
            if (reason.Length == 0)
                throw new ValidationException("Adjustment reason is required.");

            if (dto.NewStock < 0m)
                throw new ValidationException("New stock cannot be negative.");

            var product = await LoadTrackedProductAsync(productId, ct);
            var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

            await EnsureProductBelongsToBranchAsync(productId, branchId, product.Sku, ct);

            var onHand = (await _ledger.GetOnHandAsync(branchId, new[] { productId }, ct))
                .GetValueOrDefault(productId);
            var delta = dto.NewStock - onHand;
            var adjustedAt = DateTime.UtcNow;

            if (delta == 0m)
            {
                // The shelf already agrees. Posting a zero movement would add noise to the
                // audit trail and make a resubmitted form look like a second correction.
                _logger.LogInformation(
                    "Retail stock adjustment for product {ProductId} on branch {BranchId} matched the current position of {OnHand}; nothing posted",
                    productId, branchId, onHand);

                return BuildResult(productId, product, branchId, onHand, onHand, 0m, reason, adjustedAt, null);
            }

            var movementType = delta > 0m
                ? RetailStockMovementType.AdjustmentIncrease
                : RetailStockMovementType.AdjustmentDecrease;

            var movement = await _ledger.PostAsync(new RetailStockPosting(
                ProductId: productId,
                BranchId: branchId,
                MovementType: movementType,
                Quantity: Math.Abs(delta),
                UnitCost: product.CostPrice,
                SourceDocumentType: RetailStockSourceDocument.ManualAdjustment,
                // Each adjustment is its own event, so it gets its own id rather than sharing
                // a document's. That also keeps the exactly-once index from ever colliding.
                SourceDocumentId: Guid.NewGuid(),
                SourceLineId: null,
                SourceReference: product.Sku,
                OccurredAtUtc: adjustedAt,
                PerformedByUserId: _currentUser.UserIdOrNull,
                PerformedBySystem: PerformedBySystem,
                Notes: reason), ct);

            _logger.LogInformation(
                "Retail stock adjustment: product {ProductId} on branch {BranchId} {Previous} -> {New} (delta {Delta}) by user {UserId}. Reason: {Reason}",
                productId, branchId, onHand, dto.NewStock, delta, _currentUser.UserIdOrNull, reason);

            return BuildResult(productId, product, branchId, onHand, dto.NewStock, delta, reason, adjustedAt, movement?.Id);
        }

        public async Task<IReadOnlyList<RetailStockAdjustmentResultDto>> GetHistoryAsync(
            Guid productId,
            CancellationToken ct = default)
        {
            var product = await LoadTrackedProductAsync(productId, ct);
            var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

            var adjustments = await _context.RetailStockMovements
                .AsNoTracking()
                .Where(m => m.ProductId == productId
                    && m.BranchId == branchId
                    && (m.MovementType == RetailStockMovementType.AdjustmentIncrease
                        || m.MovementType == RetailStockMovementType.AdjustmentDecrease))
                .OrderByDescending(m => m.OccurredAtUtc)
                .Take(HistoryLimit)
                .Select(m => new { m.Id, m.Quantity, m.OccurredAtUtc, m.Notes })
                .ToListAsync(ct);

            // "Stock after" per adjustment is not stored; the movement's own delta is the fact
            // that matters here, and the full running balance lives in the movement history.
            return adjustments
                .Select(m => new RetailStockAdjustmentResultDto
                {
                    ProductId = productId,
                    Sku = product.Sku,
                    ProductName = product.Name,
                    BranchId = branchId,
                    Delta = m.Quantity,
                    Reason = m.Notes ?? string.Empty,
                    AdjustedAtUtc = m.OccurredAtUtc,
                    MovementId = m.Id
                })
                .ToList();
        }

        private async Task<TrackedProduct> LoadTrackedProductAsync(Guid productId, CancellationToken ct)
            => await _context.RetailProductDetails
                   .AsNoTracking()
                   .Where(d => d.ProductId == productId)
                   .Select(d => new TrackedProduct(d.Sku, d.Product!.Name, d.Product.CostPrice))
                   .FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException(
                   "This product is not stocked as a finished good, so its stock cannot be adjusted here.");

        /// <summary>
        /// Stock is branch-scoped, so an adjustment only makes sense on a branch that actually
        /// carries the product. Without this an operator on the wrong branch would silently
        /// create a stock position nobody sells from.
        /// </summary>
        private async Task EnsureProductBelongsToBranchAsync(
            Guid productId,
            Guid branchId,
            string sku,
            CancellationToken ct)
        {
            var assigned = await _context.Set<Models.BranchProduct>()
                .AsNoTracking()
                .AnyAsync(bp => bp.ProductId == productId && bp.BranchId == branchId, ct);

            if (!assigned)
                throw new ValidationException($"{sku} is not assigned to the active branch.");
        }

        private static RetailStockAdjustmentResultDto BuildResult(
            Guid productId,
            TrackedProduct product,
            Guid branchId,
            decimal previous,
            decimal updated,
            decimal delta,
            string reason,
            DateTime adjustedAt,
            Guid? movementId)
            => new()
            {
                ProductId = productId,
                Sku = product.Sku,
                ProductName = product.Name,
                BranchId = branchId,
                PreviousStock = previous,
                NewStock = updated,
                Delta = delta,
                Reason = reason,
                AdjustedAtUtc = adjustedAt,
                MovementId = movementId
            };

        private sealed record TrackedProduct(string Sku, string Name, decimal? CostPrice);
    }
}
