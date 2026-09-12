using Microsoft.EntityFrameworkCore;
using Npgsql;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Retail.Domain;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <inheritdoc cref="IRetailStockLedger"/>
    public sealed class RetailStockLedger : IRetailStockLedger
    {
        private const string UniqueViolation = "23505";
        private const int MaxMovementPageSize = 500;

        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly ILogger<RetailStockLedger> _logger;

        public RetailStockLedger(
            PosDbContext context,
            ITenantResolver tenantResolver,
            ILogger<RetailStockLedger> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<RetailStockMovement?> PostAsync(RetailStockPosting posting, CancellationToken ct = default)
        {
            var posted = await PostManyAsync(new[] { posting }, ct);
            return posted.Count == 0 ? null : posted[0];
        }

        public async Task<IReadOnlyList<RetailStockMovement>> PostManyAsync(
            IReadOnlyList<RetailStockPosting> postings,
            CancellationToken ct = default)
        {
            var candidates = postings.Where(p => Math.Abs(p.Quantity) > 0m).ToList();
            if (candidates.Count == 0)
                return Array.Empty<RetailStockMovement>();

            var existing = await LoadExistingAsync(candidates, ct);
            var result = new List<RetailStockMovement>(candidates.Count);
            var added = new List<RetailStockMovement>();

            foreach (var posting in candidates)
            {
                if (posting.SourceLineId.HasValue
                    && existing.TryGetValue((posting.MovementType, posting.SourceLineId.Value), out var already))
                {
                    result.Add(already);
                    continue;
                }

                var movement = Build(posting);
                added.Add(movement);
                result.Add(movement);
            }

            if (added.Count == 0)
                return result;

            _context.RetailStockMovements.AddRange(added);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsDuplicate(ex))
            {
                // Two callers posted the same source line at once. Exactly-once is guaranteed by
                // the unique index, not by application ordering, so the loser discards its rows
                // and adopts whatever is already recorded.
                foreach (var movement in added)
                    _context.Entry(movement).State = EntityState.Detached;

                _logger.LogWarning(
                    "Retail stock posting collided on a source line; the recorded movement stands. {Reason}",
                    ex.GetBaseException().Message);

                var reloaded = await LoadExistingAsync(candidates, ct);
                return candidates
                    .Where(p => p.SourceLineId.HasValue)
                    .Select(p => reloaded.TryGetValue((p.MovementType, p.SourceLineId!.Value), out var m) ? m : null)
                    .OfType<RetailStockMovement>()
                    .ToList();
            }

            foreach (var movement in added)
            {
                _logger.LogInformation(
                    "Retail stock {MovementType} posted for product {ProductId} in branch {BranchId}: {Quantity} units, reference {Reference}",
                    movement.MovementType,
                    movement.ProductId,
                    movement.BranchId,
                    movement.Quantity,
                    movement.SourceReference);
            }

            return result;
        }

        public async Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(
            Guid branchId,
            IReadOnlyCollection<Guid> productIds,
            CancellationToken ct = default)
        {
            if (productIds.Count == 0)
                return new Dictionary<Guid, decimal>();

            // Tracked means the product has a finished-goods stock record, NOT that it happens
            // to have moved on this branch. A tracked product with no movements here holds none
            // in stock, which is a different answer from "this product is not stocked as a
            // finished good" - and returning the latter lets a caller treat it as unlimited.
            var trackedIds = await _context.RetailProductDetails
                .AsNoTracking()
                .Where(detail => productIds.Contains(detail.ProductId))
                .Select(detail => detail.ProductId)
                .ToListAsync(ct);

            if (trackedIds.Count == 0)
                return new Dictionary<Guid, decimal>();

            var rows = await _context.RetailStockMovements
                .AsNoTracking()
                .Where(m => m.BranchId == branchId && trackedIds.Contains(m.ProductId))
                .GroupBy(m => m.ProductId)
                .Select(g => new { ProductId = g.Key, OnHand = g.Sum(m => m.Quantity) })
                .ToListAsync(ct);

            var positions = rows.ToDictionary(r => r.ProductId, r => r.OnHand);
            return trackedIds.ToDictionary(id => id, id => positions.GetValueOrDefault(id));
        }

        public async Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(Guid branchId, CancellationToken ct = default)
        {
            var rows = await _context.RetailStockMovements
                .AsNoTracking()
                .Where(m => m.BranchId == branchId)
                .GroupBy(m => m.ProductId)
                .Select(g => new { ProductId = g.Key, OnHand = g.Sum(m => m.Quantity) })
                .ToListAsync(ct);

            return rows.ToDictionary(r => r.ProductId, r => r.OnHand);
        }

        public async Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAllBranchesAsync(
            IReadOnlyCollection<Guid> productIds,
            CancellationToken ct = default)
        {
            if (productIds.Count == 0)
                return new Dictionary<Guid, decimal>();

            var rows = await _context.RetailStockMovements
                .AsNoTracking()
                .Where(m => productIds.Contains(m.ProductId))
                .GroupBy(m => m.ProductId)
                .Select(g => new { ProductId = g.Key, OnHand = g.Sum(m => m.Quantity) })
                .ToListAsync(ct);

            return rows.ToDictionary(r => r.ProductId, r => r.OnHand);
        }

        public async Task<IReadOnlyDictionary<Guid, RetailStockPosition>> GetPositionsAsync(
            IReadOnlyCollection<Guid> productIds,
            Guid? branchId,
            CancellationToken ct = default)
        {
            if (productIds.Count == 0)
                return new Dictionary<Guid, RetailStockPosition>();

            var query = _context.RetailStockMovements
                .AsNoTracking()
                .Where(m => productIds.Contains(m.ProductId));

            if (branchId.HasValue)
                query = query.Where(m => m.BranchId == branchId.Value);

            var rows = await query
                .GroupBy(m => m.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    TotalIn = g.Where(m => m.Quantity > 0m).Sum(m => (decimal?)m.Quantity) ?? 0m,
                    TotalOut = g.Where(m => m.Quantity < 0m).Sum(m => (decimal?)m.Quantity) ?? 0m,
                    OnHand = g.Sum(m => m.Quantity)
                })
                .ToListAsync(ct);

            return rows.ToDictionary(
                r => r.ProductId,
                r => new RetailStockPosition(r.TotalIn, Math.Abs(r.TotalOut), r.OnHand));
        }

        public async Task<IReadOnlyList<RetailStockMovement>> GetMovementsAsync(
            Guid productId,
            Guid? branchId,
            int limit,
            CancellationToken ct = default)
        {
            var query = _context.RetailStockMovements
                .AsNoTracking()
                .Where(m => m.ProductId == productId);

            if (branchId.HasValue)
                query = query.Where(m => m.BranchId == branchId.Value);

            return await query
                .OrderByDescending(m => m.OccurredAtUtc)
                .ThenByDescending(m => m.CreatedAt)
                .Take(Math.Clamp(limit, 1, MaxMovementPageSize))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<RetailStockMovement>> GetBySourceDocumentAsync(
            RetailStockSourceDocument documentType,
            Guid documentId,
            CancellationToken ct = default)
            => await _context.RetailStockMovements
                .AsNoTracking()
                .Where(m => m.SourceDocumentType == documentType && m.SourceDocumentId == documentId)
                .OrderBy(m => m.OccurredAtUtc)
                .ToListAsync(ct);

        private async Task<Dictionary<(RetailStockMovementType Type, Guid LineId), RetailStockMovement>> LoadExistingAsync(
            IReadOnlyList<RetailStockPosting> postings,
            CancellationToken ct)
        {
            var lineIds = postings
                .Where(p => p.SourceLineId.HasValue)
                .Select(p => p.SourceLineId!.Value)
                .Distinct()
                .ToList();

            if (lineIds.Count == 0)
                return new Dictionary<(RetailStockMovementType, Guid), RetailStockMovement>();

            var rows = await _context.RetailStockMovements
                .Where(m => m.SourceLineId != null && lineIds.Contains(m.SourceLineId!.Value))
                .ToListAsync(ct);

            return rows
                .GroupBy(m => (m.MovementType, m.SourceLineId!.Value))
                .ToDictionary(g => g.Key, g => g.First());
        }

        private RetailStockMovement Build(RetailStockPosting posting)
        {
            var signed = RetailStockMovement.SignedQuantity(posting.MovementType, posting.Quantity);
            return new RetailStockMovement
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                ProductId = posting.ProductId,
                BranchId = posting.BranchId,
                MovementType = posting.MovementType,
                Quantity = signed,
                UnitCost = posting.UnitCost,
                TotalCost = posting.UnitCost.HasValue
                    ? decimal.Round(signed * posting.UnitCost.Value, 2, MidpointRounding.AwayFromZero)
                    : null,
                SourceDocumentType = posting.SourceDocumentType,
                SourceDocumentId = posting.SourceDocumentId,
                SourceLineId = posting.SourceLineId,
                SourceReference = posting.SourceReference,
                OccurredAtUtc = DateTime.SpecifyKind(posting.OccurredAtUtc, DateTimeKind.Utc),
                PerformedByUserId = posting.PerformedByUserId,
                PerformedBySystem = posting.PerformedBySystem,
                ReversesMovementId = posting.ReversesMovementId,
                Notes = posting.Notes
            };
        }

        private static bool IsDuplicate(DbUpdateException ex)
            => ex.GetBaseException() is PostgresException { SqlState: UniqueViolation };
    }
}
